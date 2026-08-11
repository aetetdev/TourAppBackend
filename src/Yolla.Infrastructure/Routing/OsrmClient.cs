using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Yolla.Application.Common;
using Yolla.Application.Routing;
using Yolla.Domain.Enums;

namespace Yolla.Infrastructure.Routing;

/// <inheritdoc cref="IRoutingClient"/>
public sealed class OsrmClient(HttpClient httpClient, IOptions<OsrmOptions> options) : IRoutingClient
{
    private readonly OsrmOptions _options = options.Value;

    public async Task<RouteResult> OptimizeTripAsync(
        IReadOnlyList<GeoPoint> points,
        TravelMode travelMode,
        bool roundTrip = false,
        CancellationToken cancellationToken = default)
    {
        ValidatePoints(points, minimum: 2);

        // source=first: ilk nokta başlangıç kabul edilir (kullanıcının bulunduğu yer)
        // roundtrip=false: gezi son durakta biter, başlangıca dönmez
        var query = $"?source=first&roundtrip={roundTrip.ToString().ToLowerInvariant()}"
                    + "&overview=full&geometries=polyline";

        if (!roundTrip)
        {
            query += "&destination=last";
        }

        var url = BuildUrl(travelMode, "trip", points, query);
        var json = await SendAsync(url, cancellationToken);

        return ParseTrip(json);
    }

    public async Task<RouteResult> GetRouteAsync(
        IReadOnlyList<GeoPoint> points,
        TravelMode travelMode,
        CancellationToken cancellationToken = default)
    {
        ValidatePoints(points, minimum: 2);

        var url = BuildUrl(travelMode, "route", points, "?overview=full&geometries=polyline");
        var json = await SendAsync(url, cancellationToken);

        return ParseRoute(json, points.Count);
    }

    public async Task<string> GetRouteGeoJsonAsync(
        IReadOnlyList<GeoPoint> points,
        TravelMode travelMode,
        CancellationToken cancellationToken = default)
    {
        ValidatePoints(points, minimum: 2);

        var url = BuildUrl(travelMode, "route", points, "?overview=full&geometries=geojson");
        var json = await SendAsync(url, cancellationToken);

        using var document = JsonDocument.Parse(json);

        var geometry = FirstRoute(document.RootElement).GetProperty("geometry");

        return geometry.GetRawText();
    }

    private string BuildUrl(TravelMode travelMode, string service, IReadOnlyList<GeoPoint> points, string query)
    {
        var baseUrl = travelMode == TravelMode.Foot ? _options.FootBaseUrl : _options.CarBaseUrl;
        var profile = travelMode == TravelMode.Foot ? "foot" : "driving";

        // OSRM koordinatları boylam,enlem sırasında bekler - enlem,boylam değil.
        // Ters yazılırsa rota Türkiye yerine okyanusun ortasında aranır.
        var coordinates = string.Join(';', points.Select(p =>
            FormattableString.Invariant($"{p.Longitude},{p.Latitude}")));

        return $"{baseUrl.TrimEnd('/')}/{service}/v1/{profile}/{coordinates}{query}";
    }

    private async Task<string> SendAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync(url, cancellationToken);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new UpstreamServiceException("OSRM",
                    $"Rota motoru {(int)response.StatusCode} döndürdü.");
            }

            return body;
        }
        catch (HttpRequestException exception)
        {
            throw new UpstreamServiceException("OSRM",
                "Rota motoruna ulaşılamadı. Servis çalışıyor mu?", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UpstreamServiceException("OSRM", "Rota hesaplama zaman aşımına uğradı.", exception);
        }
    }

    private static RouteResult ParseTrip(string json)
    {
        using var document = JsonDocument.Parse(json);

        EnsureOk(document.RootElement);

        if (!document.RootElement.TryGetProperty("trips", out var trips)
            || trips.ValueKind != JsonValueKind.Array
            || trips.GetArrayLength() == 0)
        {
            throw new UpstreamServiceException("OSRM", "Rota motoru geçerli bir gezi döndürmedi.");
        }

        var trip = trips[0];

        // waypoints dizisi gönderim sırasında; her birinin waypoint_index alanı
        // o durağa kaçıncı sırada uğranacağını söyler
        var order = ReadWaypointOrder(document.RootElement);

        return new RouteResult
        {
            DistanceMeters = trip.GetProperty("distance").GetDouble(),
            DurationSeconds = trip.GetProperty("duration").GetDouble(),
            Geometry = trip.GetProperty("geometry").GetString() ?? string.Empty,
            WaypointOrder = order
        };
    }

    private static RouteResult ParseRoute(string json, int pointCount)
    {
        using var document = JsonDocument.Parse(json);

        var route = FirstRoute(document.RootElement);

        return new RouteResult
        {
            DistanceMeters = route.GetProperty("distance").GetDouble(),
            DurationSeconds = route.GetProperty("duration").GetDouble(),
            Geometry = route.TryGetProperty("geometry", out var geometry) && geometry.ValueKind == JsonValueKind.String
                ? geometry.GetString() ?? string.Empty
                : string.Empty,
            // Sıra optimizasyonu yapılmadı; noktalar gönderildiği sırada
            WaypointOrder = Enumerable.Range(0, pointCount).ToList()
        };
    }

    private static JsonElement FirstRoute(JsonElement root)
    {
        EnsureOk(root);

        if (!root.TryGetProperty("routes", out var routes)
            || routes.ValueKind != JsonValueKind.Array
            || routes.GetArrayLength() == 0)
        {
            throw new UpstreamServiceException("OSRM", "Rota motoru geçerli bir rota döndürmedi.");
        }

        return routes[0];
    }

    private static void EnsureOk(JsonElement root)
    {
        var code = root.TryGetProperty("code", out var codeElement) ? codeElement.GetString() : null;

        if (code is "Ok")
        {
            return;
        }

        // NoRoute: noktalar arasında yol yok (ada, deniz ortası, erişilemez bölge)
        var message = code switch
        {
            "NoRoute" => "Seçilen noktalar arasında karayolu bağlantısı bulunamadı.",
            "NoSegment" => "Seçilen noktalardan biri yola bağlanamıyor.",
            "TooBig" => "Çok fazla durak gönderildi.",
            _ => $"Rota motoru hata döndürdü: {code}"
        };

        throw new UpstreamServiceException("OSRM", message);
    }

    private static List<int> ReadWaypointOrder(JsonElement root)
    {
        if (!root.TryGetProperty("waypoints", out var waypoints)
            || waypoints.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        // waypoint_index -> gönderim sırasındaki dizin
        var indexed = new List<(int TripIndex, int InputIndex)>();
        var inputIndex = 0;

        foreach (var waypoint in waypoints.EnumerateArray())
        {
            if (waypoint.TryGetProperty("waypoint_index", out var tripIndex))
            {
                indexed.Add((tripIndex.GetInt32(), inputIndex));
            }

            inputIndex++;
        }

        return indexed
            .OrderBy(x => x.TripIndex)
            .Select(x => x.InputIndex)
            .ToList();
    }

    private void ValidatePoints(IReadOnlyList<GeoPoint> points, int minimum)
    {
        ArgumentNullException.ThrowIfNull(points);

        if (points.Count < minimum)
        {
            throw RequestValidationException.Single(
                nameof(points), $"Rota için en az {minimum} nokta gerekli.");
        }

        if (points.Count > _options.MaxWaypoints)
        {
            throw RequestValidationException.Single(
                nameof(points), $"En fazla {_options.MaxWaypoints} durak gönderilebilir.");
        }

        foreach (var point in points)
        {
            if (point.Latitude is < -90 or > 90 || point.Longitude is < -180 or > 180)
            {
                throw RequestValidationException.Single(
                    nameof(points),
                    FormattableString.Invariant($"Geçersiz koordinat: {point.Latitude},{point.Longitude}"));
            }
        }
    }
}
