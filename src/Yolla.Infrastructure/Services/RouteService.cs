using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using Yolla.Application.Common;
using Yolla.Application.Discovery;
using Yolla.Application.Places;
using Yolla.Application.Routing;
using Yolla.Infrastructure.Persistence;

namespace Yolla.Infrastructure.Services;

/// <inheritdoc cref="IRouteService"/>
public sealed class RouteService(YollaDbContext context, IRoutingClient routingClient) : IRouteService
{
    private const int MinBufferKm = 1;
    private const int MaxBufferKm = 50;
    private const int MaxTake = 50;
    private const int MaxEndpointExclusionKm = 100;

    /// <summary>Rotanın kaç dilime bölüneceği; kartların yol boyunca dağılması için.</summary>
    private const int SegmentCount = 24;

    /// <summary>Her dilimden alınacak en fazla yer sayısı.</summary>
    private const int PerSegmentLimit = 3;

    /// <summary>Gezme süresi bilinmeyen yerler için varsayılan (dakika).</summary>
    private const int DefaultVisitMinutes = 45;

    public async Task<OptimizedRouteDto> OptimizeAsync(
        RouteOptimizeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.PlaceIds is null || request.PlaceIds.Count == 0)
        {
            throw RequestValidationException.Single(
                nameof(request.PlaceIds), "En az bir yer seçilmeli.");
        }

        var isEnglish = request.Language.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        var places = await LoadPlacesAsync(request.PlaceIds, isEnglish, cancellationToken);

        if (places.Count == 0)
        {
            throw new NotFoundException("Yer", string.Join(", ", request.PlaceIds));
        }

        // Başlangıç noktası verilmişse ilk sıraya konur; OSRM ilk noktayı başlangıç sayar
        var points = new List<GeoPoint>(places.Count + 1);
        var hasStartPoint = request.StartPoint is not null;

        if (request.StartPoint is { } start)
        {
            points.Add(start);
        }

        points.AddRange(places.Select(x => new GeoPoint(x.Latitude, x.Longitude)));

        if (points.Count < 2)
        {
            throw RequestValidationException.Single(
                nameof(request.PlaceIds),
                "Rota için en az iki nokta gerekli. Tek yer seçildiyse başlangıç noktası da gönderin.");
        }

        var route = await routingClient.OptimizeTripAsync(
            points, request.TravelMode, request.RoundTrip, cancellationToken);

        var stops = BuildStops(route.WaypointOrder, places, hasStartPoint);

        return new OptimizedRouteDto
        {
            DistanceMeters = route.DistanceMeters,
            TravelDurationSeconds = route.DurationSeconds,
            VisitDurationMinutes = stops.Sum(x => x.Place.AverageVisitMinutes ?? DefaultVisitMinutes),
            Geometry = route.Geometry,
            Stops = stops
        };
    }

    public async Task<CorridorFeedDto> GetCorridorFeedAsync(
        CorridorFeedRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var bufferKm = Math.Clamp(request.BufferKm, MinBufferKm, MaxBufferKm);
        var take = Math.Clamp(request.Take, 1, MaxTake);

        // Önce ana rota çıkarılır: koridor bu çizginin çevresi demek
        var points = new List<GeoPoint> { request.Start, request.End };

        var geoJson = await routingClient.GetRouteGeoJsonAsync(
            points, Domain.Enums.TravelMode.Car, cancellationToken);

        var route = await routingClient.GetRouteAsync(
            points, Domain.Enums.TravelMode.Car, cancellationToken);

        var cards = await QueryCorridorAsync(request, geoJson, bufferKm, take, cancellationToken);

        var hasMore = cards.Count > take;

        if (hasMore)
        {
            cards.RemoveAt(cards.Count - 1);
        }

        string? nextCursor = null;

        if (hasMore && cards.Count > 0)
        {
            var last = cards[^1];
            nextCursor = new CorridorCursor(last.RouteProgress ?? 0, last.Id).Encode();
        }

        return new CorridorFeedDto
        {
            Cards = new CursorPageOfCards
            {
                Items = FeedDiversifier.Diversify(cards),
                NextCursor = nextCursor
            },
            RouteDistanceMeters = route.DistanceMeters,
            RouteDurationSeconds = route.DurationSeconds,
            RouteGeometry = route.Geometry
        };
    }

    public async Task<CorridorCitiesDto> GetCorridorCitiesAsync(
        CorridorCitiesRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var bufferKm = Math.Clamp(request.BufferKm, MinBufferKm, MaxBufferKm);

        var points = new List<GeoPoint> { request.Start, request.End };

        var geoJson = await routingClient.GetRouteGeoJsonAsync(
            points, Domain.Enums.TravelMode.Car, cancellationToken);

        var route = await routingClient.GetRouteAsync(
            points, Domain.Enums.TravelMode.Car, cancellationToken);

        var cities = await QueryCorridorCitiesAsync(
            request, geoJson, bufferKm, cancellationToken);

        return new CorridorCitiesDto
        {
            Cities = cities,
            RouteDistanceMeters = route.DistanceMeters,
            RouteDurationSeconds = route.DurationSeconds,
            RouteGeometry = route.Geometry
        };
    }

    /// <summary>
    /// Koridora giren yerleri şehre göre toplar.
    /// </summary>
    /// <remarks>
    /// Eleme koşulları kart sorgusuyla **birebir aynı** tutuluyor: kullanıcıya
    /// "bu şehirde 12 yer var" denip sonra kart akışında farklı bir sayı
    /// çıkmamalı. Sıralama <c>ST_LineLocatePoint</c> ile yol üzerindeki
    /// konuma göre; şehirler geçilecekleri sırayla listeleniyor.
    /// </remarks>
    private async Task<List<CorridorCityDto>> QueryCorridorCitiesAsync(
        CorridorCitiesRequest request,
        string routeGeoJson,
        int bufferKm,
        CancellationToken cancellationToken)
    {
        var categoryFilter = request.CategoryKeys is { Count: > 0 }
            ? "AND c.key = ANY(@categories)"
            : string.Empty;

        var sql = $"""
            WITH route AS (
                SELECT
                    ST_SetSRID(ST_GeomFromGeoJSON(@geojson), 4326) AS line,
                    ST_SetSRID(ST_MakePoint(@start_lon, @start_lat), 4326)::geography AS start_point,
                    ST_SetSRID(ST_MakePoint(@end_lon, @end_lat), 4326)::geography AS end_point
            ),
            candidates AS (
                SELECT
                    p.city_id,
                    ci.name AS city_name,
                    ST_LineLocatePoint(r.line, p.location::geometry) AS progress
                FROM places p
                CROSS JOIN route r
                JOIN categories c ON c.id = p.category_id
                JOIN cities ci    ON ci.id = p.city_id
                WHERE ST_DWithin(p.location, r.line::geography, @buffer_meters)
                  AND p.is_active
                  AND c.is_visible
                  AND p.photo_url IS NOT NULL
                  AND p.photo_author IS NOT NULL
                  AND p.photo_license IS NOT NULL
                  AND p.quality_score >= @min_score
                  AND ST_Distance(p.location, r.start_point) > @endpoint_meters
                  AND ST_Distance(p.location, r.end_point)   > @endpoint_meters
                  {categoryFilter}
            )
            SELECT
                city_id,
                city_name,
                COUNT(*)      AS place_count,
                MIN(progress) AS progress
            FROM candidates
            GROUP BY city_id, city_name
            ORDER BY progress
            """;

        await using var command = context.Database.GetDbConnection().CreateCommand();

        command.CommandText = sql;
        command.CommandTimeout = 60;

        AddParameter(command, "geojson", NpgsqlDbType.Text, routeGeoJson);
        AddParameter(command, "buffer_meters", NpgsqlDbType.Double, (double)bufferKm * 1000);
        AddParameter(command, "min_score", NpgsqlDbType.Smallint, PlaceQualityScorer.FeedThreshold);
        AddParameter(command, "start_lat", NpgsqlDbType.Double, request.Start.Latitude);
        AddParameter(command, "start_lon", NpgsqlDbType.Double, request.Start.Longitude);
        AddParameter(command, "end_lat", NpgsqlDbType.Double, request.End.Latitude);
        AddParameter(command, "end_lon", NpgsqlDbType.Double, request.End.Longitude);
        AddParameter(command, "endpoint_meters", NpgsqlDbType.Double,
            (double)Math.Clamp(request.ExcludeEndpointsKm, 0, MaxEndpointExclusionKm) * 1000);

        if (request.CategoryKeys is { Count: > 0 })
        {
            AddParameter(command, "categories", NpgsqlDbType.Array | NpgsqlDbType.Text,
                request.CategoryKeys.ToArray());
        }

        await context.Database.OpenConnectionAsync(cancellationToken);

        var cities = new List<CorridorCityDto>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            cities.Add(new CorridorCityDto
            {
                CityId = reader.GetInt32(reader.GetOrdinal("city_id")),
                Name = reader.GetString(reader.GetOrdinal("city_name")),
                PlaceCount = (int)reader.GetInt64(reader.GetOrdinal("place_count")),
                Progress = reader.GetDouble(reader.GetOrdinal("progress"))
            });
        }

        return cities;
    }

    /// <summary>
    /// Rota çizgisinin çevresindeki yerleri, yol boyunca ilerleme sırasına göre getirir.
    /// </summary>
    /// <remarks>
    /// Sorgunun iki kritik parçası var:
    /// <c>ST_DWithin</c> koridoru belirler ve GIST indeksini kullanır;
    /// <c>ST_LineLocatePoint</c> her yerin yolun neresine denk geldiğini (0-1) söyler.
    /// İkincisi olmadan kullanıcı İstanbul'dan çıkarken Antalya'daki yerin kartını görürdü.
    ///
    /// Ham SQL kullanılıyor çünkü ST_LineLocatePoint'in EF Core karşılığı yok.
    /// </remarks>
    private async Task<List<PlaceCardDto>> QueryCorridorAsync(
        CorridorFeedRequest request,
        string routeGeoJson,
        int bufferKm,
        int take,
        CancellationToken cancellationToken)
    {
        var hasCursor = CorridorCursor.TryDecode(request.Cursor, out var cursor);
        var isEnglish = request.Language.StartsWith("en", StringComparison.OrdinalIgnoreCase);

        var categoryFilter = request.CategoryKeys is { Count: > 0 }
            ? "AND c.key = ANY(@categories)"
            : string.Empty;

        var deviceFilter = request.DeviceId is not null
            ? "AND NOT EXISTS (SELECT 1 FROM swipes s WHERE s.device_id = @device_id AND s.place_id = p.id)"
            : string.Empty;

        var cursorFilter = hasCursor
            ? "AND (t.progress > @cursor_progress OR (t.progress = @cursor_progress AND t.id > @cursor_id))"
            : string.Empty;

        // Kullanıcı koridordaki şehirlerden seçim yaptıysa yalnızca onlar.
        var cityFilter = request.CityIds is { Count: > 0 }
            ? "AND p.city_id = ANY(@city_ids)"
            : string.Empty;

        var sql = $"""
            WITH route AS (
                SELECT
                    ST_SetSRID(ST_GeomFromGeoJSON(@geojson), 4326) AS line,
                    ST_SetSRID(ST_MakePoint(@start_lon, @start_lat), 4326)::geography AS start_point,
                    ST_SetSRID(ST_MakePoint(@end_lon, @end_lat), 4326)::geography AS end_point
            ),
            candidates AS (
                SELECT
                    p.id,
                    p.name,
                    p.slug,
                    c.key            AS category_key,
                    c.name_tr        AS category_name_tr,
                    c.name_en        AS category_name_en,
                    c.icon           AS category_icon,
                    p.photo_url,
                    p.photo_author,
                    p.photo_license,
                    p.photo_source,
                    p.description_tr,
                    p.description_en,
                    p.city_id,
                    ci.name          AS city_name,
                    d.name           AS district_name,
                    ST_Y(p.location::geometry) AS latitude,
                    ST_X(p.location::geometry) AS longitude,
                    p.avg_visit_minutes,
                    p.quality_score,
                    ST_LineLocatePoint(r.line, p.location::geometry) AS progress,
                    ST_Distance(p.location, r.line::geography)       AS detour_meters
                FROM places p
                CROSS JOIN route r
                JOIN categories c ON c.id = p.category_id
                JOIN cities ci    ON ci.id = p.city_id
                LEFT JOIN districts d ON d.id = p.district_id
                WHERE ST_DWithin(p.location, r.line::geography, @buffer_meters)
                  AND p.is_active
                  AND c.is_visible
                  AND p.photo_url IS NOT NULL
                  AND p.photo_author IS NOT NULL
                  AND p.photo_license IS NOT NULL
                  AND p.quality_score >= @min_score
                  -- Kullanıcının çıktığı ve vardığı şehrin merkezi hariç: oraları zaten biliyor
                  AND ST_Distance(p.location, r.start_point) > @endpoint_meters
                  AND ST_Distance(p.location, r.end_point)   > @endpoint_meters
                  {categoryFilter}
                  {deviceFilter}
                  {cityFilter}
            ),
            -- Yol boyunca dengeli dağıtım: rota eşit dilimlere bölünüp her dilimden
            -- en kaliteli birkaç yer alınıyor. Yalnızca puana göre sıralansaydı tek bir
            -- yoğun bölge (örneğin bir şehrin tarihi merkezi) tüm sayfayı doldururdu.
            ranked AS (
                SELECT
                    t.*,
                    ROW_NUMBER() OVER (
                        PARTITION BY width_bucket(t.progress, 0, 1, @segment_count)
                        ORDER BY t.quality_score DESC, t.detour_meters
                    ) AS rank_in_segment
                FROM candidates t
            )
            SELECT * FROM ranked t
            WHERE t.rank_in_segment <= @per_segment
            {cursorFilter}
            ORDER BY t.progress, t.id
            LIMIT @take
            """;

        await using var command = context.Database.GetDbConnection().CreateCommand();

        command.CommandText = sql;
        command.CommandTimeout = 60;

        AddParameter(command, "geojson", NpgsqlDbType.Text, routeGeoJson);
        AddParameter(command, "buffer_meters", NpgsqlDbType.Double, (double)bufferKm * 1000);
        AddParameter(command, "min_score", NpgsqlDbType.Smallint, PlaceQualityScorer.FeedThreshold);
        AddParameter(command, "start_lat", NpgsqlDbType.Double, request.Start.Latitude);
        AddParameter(command, "start_lon", NpgsqlDbType.Double, request.Start.Longitude);
        AddParameter(command, "end_lat", NpgsqlDbType.Double, request.End.Latitude);
        AddParameter(command, "end_lon", NpgsqlDbType.Double, request.End.Longitude);
        AddParameter(command, "endpoint_meters", NpgsqlDbType.Double,
            (double)Math.Clamp(request.ExcludeEndpointsKm, 0, MaxEndpointExclusionKm) * 1000);
        AddParameter(command, "segment_count", NpgsqlDbType.Integer, SegmentCount);
        AddParameter(command, "per_segment", NpgsqlDbType.Integer, PerSegmentLimit);
        // Bir fazlası çekiliyor: devamı var mı anlamak için
        AddParameter(command, "take", NpgsqlDbType.Integer, take + 1);

        if (request.CategoryKeys is { Count: > 0 })
        {
            AddParameter(command, "categories", NpgsqlDbType.Array | NpgsqlDbType.Text,
                request.CategoryKeys.ToArray());
        }

        if (request.DeviceId is { } deviceId)
        {
            AddParameter(command, "device_id", NpgsqlDbType.Integer, deviceId);
        }

        if (request.CityIds is { Count: > 0 })
        {
            AddParameter(command, "city_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer,
                request.CityIds.ToArray());
        }

        if (hasCursor)
        {
            AddParameter(command, "cursor_progress", NpgsqlDbType.Double, cursor.Progress);
            AddParameter(command, "cursor_id", NpgsqlDbType.Integer, cursor.PlaceId);
        }

        await context.Database.OpenConnectionAsync(cancellationToken);

        var cards = new List<PlaceCardDto>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            cards.Add(ReadCard(reader, isEnglish));
        }

        return cards;
    }

    private static PlaceCardDto ReadCard(System.Data.Common.DbDataReader reader, bool isEnglish)
    {
        string? GetNullableString(string column)
        {
            var index = reader.GetOrdinal(column);
            return reader.IsDBNull(index) ? null : reader.GetString(index);
        }

        var author = GetNullableString("photo_author");
        var license = GetNullableString("photo_license");

        return new PlaceCardDto
        {
            Id = reader.GetInt32(reader.GetOrdinal("id")),
            CityId = reader.GetInt32(reader.GetOrdinal("city_id")),
            Name = reader.GetString(reader.GetOrdinal("name")),
            Slug = reader.GetString(reader.GetOrdinal("slug")),
            CategoryKey = reader.GetString(reader.GetOrdinal("category_key")),
            CategoryName = reader.GetString(reader.GetOrdinal(
                isEnglish ? "category_name_en" : "category_name_tr")),
            CategoryIcon = GetNullableString("category_icon"),
            PhotoUrl = reader.GetString(reader.GetOrdinal("photo_url")),
            PhotoAttribution = Attribution.ForPhoto(author, license)!,
            PhotoSource = GetNullableString("photo_source"),
            Description = isEnglish
                ? GetNullableString("description_en") ?? GetNullableString("description_tr")
                : GetNullableString("description_tr"),
            CityName = reader.GetString(reader.GetOrdinal("city_name")),
            DistrictName = GetNullableString("district_name"),
            Latitude = reader.GetDouble(reader.GetOrdinal("latitude")),
            Longitude = reader.GetDouble(reader.GetOrdinal("longitude")),
            AverageVisitMinutes = reader.IsDBNull(reader.GetOrdinal("avg_visit_minutes"))
                ? null
                : reader.GetInt16(reader.GetOrdinal("avg_visit_minutes")),
            QualityScore = reader.GetInt16(reader.GetOrdinal("quality_score")),
            RouteProgress = reader.GetDouble(reader.GetOrdinal("progress")),
            DetourMeters = reader.GetDouble(reader.GetOrdinal("detour_meters"))
        };
    }

    private static void AddParameter(
        System.Data.Common.DbCommand command,
        string name,
        NpgsqlDbType type,
        object value)
    {
        var parameter = new NpgsqlParameter(name, type) { Value = value };

        command.Parameters.Add(parameter);
    }

    private static List<RouteStopDto> BuildStops(
        IReadOnlyList<int> waypointOrder,
        IReadOnlyList<PlaceCardDto> places,
        bool hasStartPoint)
    {
        var stops = new List<RouteStopDto>(places.Count);
        var order = 1;

        // Sıra bilgisi gelmediyse gönderim sırası korunur
        var sequence = waypointOrder.Count > 0
            ? waypointOrder
            : Enumerable.Range(0, places.Count + (hasStartPoint ? 1 : 0)).ToList();

        foreach (var index in sequence)
        {
            // Başlangıç noktası bir yer değil, durak listesinde yer almaz
            var placeIndex = hasStartPoint ? index - 1 : index;

            if (placeIndex < 0 || placeIndex >= places.Count)
            {
                continue;
            }

            stops.Add(new RouteStopDto
            {
                Order = order++,
                Place = places[placeIndex]
            });
        }

        return stops;
    }

    private async Task<List<PlaceCardDto>> LoadPlacesAsync(
        IReadOnlyList<int> placeIds,
        bool isEnglish,
        CancellationToken cancellationToken)
    {
        var rows = await context.Places
            .AsNoTracking()
            .Where(x => placeIds.Contains(x.Id) && x.IsActive)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Slug,
                CategoryKey = x.Category.Key,
                CategoryNameTr = x.Category.NameTr,
                CategoryNameEn = x.Category.NameEn,
                CategoryIcon = x.Category.Icon,
                x.PhotoUrl,
                x.PhotoAuthor,
                x.PhotoLicense,
                x.PhotoSource,
                x.DescriptionTr,
                x.DescriptionEn,
                x.CityId,
                CityName = x.City.Name,
                DistrictName = x.District != null ? x.District.Name : null,
                x.Location,
                x.AvgVisitMinutes,
                x.QualityScore
            })
            .ToListAsync(cancellationToken);

        // İstenen sıra korunur: kullanıcının gönderdiği liste anlamlı olabilir
        var byId = rows.ToDictionary(x => x.Id);

        return placeIds
            .Where(byId.ContainsKey)
            .Select(id => byId[id])
            .Select(x => new PlaceCardDto
            {
                Id = x.Id,
                CityId = x.CityId,
                Name = x.Name,
                Slug = x.Slug,
                CategoryKey = x.CategoryKey,
                CategoryName = isEnglish ? x.CategoryNameEn : x.CategoryNameTr,
                CategoryIcon = x.CategoryIcon,
                PhotoUrl = x.PhotoUrl ?? string.Empty,
                PhotoAttribution = Attribution.ForPhoto(x.PhotoAuthor, x.PhotoLicense) ?? string.Empty,
                PhotoSource = x.PhotoSource,
                Description = isEnglish ? x.DescriptionEn ?? x.DescriptionTr : x.DescriptionTr,
                CityName = x.CityName,
                DistrictName = x.DistrictName,
                Latitude = x.Location.Y,
                Longitude = x.Location.X,
                AverageVisitMinutes = x.AvgVisitMinutes,
                QualityScore = x.QualityScore
            })
            .ToList();
    }
}
