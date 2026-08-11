using System.Net;
using Microsoft.Extensions.Options;
using Shouldly;
using Yolla.Application.Common;
using Yolla.Application.Routing;
using Yolla.Domain.Enums;
using Yolla.Infrastructure.Routing;

namespace Yolla.Application.Tests.Routing;

public class OsrmClientTests
{
    private static readonly GeoPoint Istanbul = new(41.0082, 28.9784);
    private static readonly GeoPoint Antalya = new(36.8969, 30.7133);

    [Fact]
    public async Task Koordinatlar_boylam_enlem_sirasinda_gonderilir()
    {
        // OSRM enlem,boylam değil boylam,enlem bekler. Ters yazılırsa Türkiye yerine
        // okyanusun ortasında rota aranır ve sonuç sessizce yanlış olur.
        var handler = new RecordingHandler(TripResponse());
        var client = CreateClient(handler);

        await client.OptimizeTripAsync([Istanbul, Antalya], TravelMode.Foot);

        handler.LastUrl.ShouldContain("28.9784,41.0082");
        handler.LastUrl.ShouldContain("30.7133,36.8969");
    }

    [Fact]
    public async Task Yurume_ve_arac_farkli_sunuculara_gider()
    {
        // İki profil ayrı OSRM örneğinde çalışıyor
        var handler = new RecordingHandler(TripResponse());
        var client = CreateClient(handler);

        await client.OptimizeTripAsync([Istanbul, Antalya], TravelMode.Foot);
        handler.LastUrl.ShouldStartWith("http://foot-sunucu");
        handler.LastUrl.ShouldContain("/foot/");

        await client.OptimizeTripAsync([Istanbul, Antalya], TravelMode.Car);
        handler.LastUrl.ShouldStartWith("http://car-sunucu");
        handler.LastUrl.ShouldContain("/driving/");
    }

    [Fact]
    public async Task Durak_sirasi_cozulur()
    {
        // waypoint_index: gönderilen 3 noktanın gezilme sırası 0 -> 2 -> 1
        const string json = """
            {
              "code": "Ok",
              "trips": [{ "distance": 1500.5, "duration": 900.2, "geometry": "abc123" }],
              "waypoints": [
                { "waypoint_index": 0 },
                { "waypoint_index": 2 },
                { "waypoint_index": 1 }
              ]
            }
            """;

        var client = CreateClient(new RecordingHandler(json));

        var result = await client.OptimizeTripAsync([Istanbul, Antalya, new GeoPoint(39, 35)], TravelMode.Car);

        result.DistanceMeters.ShouldBe(1500.5);
        result.DurationSeconds.ShouldBe(900.2);
        result.Geometry.ShouldBe("abc123");
        // Sıra: önce 0. nokta, sonra 2. nokta, en son 1. nokta
        result.WaypointOrder.ShouldBe([0, 2, 1]);
    }

    [Fact]
    public async Task Gezgin_satici_cozumu_istenir()
    {
        // roundtrip=false ve source=first olmadan OSRM rotayı halka yapar ve
        // başlangıç noktasını kendi seçer
        var handler = new RecordingHandler(TripResponse());
        var client = CreateClient(handler);

        await client.OptimizeTripAsync([Istanbul, Antalya], TravelMode.Foot);

        handler.LastUrl.ShouldContain("/trip/");
        handler.LastUrl.ShouldContain("source=first");
        handler.LastUrl.ShouldContain("roundtrip=false");
        handler.LastUrl.ShouldContain("destination=last");
    }

    [Fact]
    public async Task Halka_rota_istendiginde_varis_sabitlenmez()
    {
        var handler = new RecordingHandler(TripResponse());
        var client = CreateClient(handler);

        await client.OptimizeTripAsync([Istanbul, Antalya], TravelMode.Foot, roundTrip: true);

        handler.LastUrl.ShouldContain("roundtrip=true");
        handler.LastUrl.ShouldNotContain("destination=last");
    }

    [Fact]
    public async Task Rota_bulunamadiginda_anlamli_hata_verilir()
    {
        var client = CreateClient(new RecordingHandler("""{"code":"NoRoute","message":"..."}"""));

        var exception = await Should.ThrowAsync<UpstreamServiceException>(
            () => client.GetRouteAsync([Istanbul, Antalya], TravelMode.Car));

        exception.Message.ShouldContain("karayolu bağlantısı bulunamadı");
    }

    [Fact]
    public async Task Yola_baglanamayan_nokta_bildirilir()
    {
        var client = CreateClient(new RecordingHandler("""{"code":"NoSegment"}"""));

        var exception = await Should.ThrowAsync<UpstreamServiceException>(
            () => client.GetRouteAsync([Istanbul, Antalya], TravelMode.Car));

        exception.Message.ShouldContain("yola bağlanamıyor");
    }

    [Fact]
    public async Task Servis_erisilemezse_upstream_hatasi_verilir()
    {
        var client = CreateClient(new RecordingHandler(string.Empty, HttpStatusCode.ServiceUnavailable));

        var exception = await Should.ThrowAsync<UpstreamServiceException>(
            () => client.GetRouteAsync([Istanbul, Antalya], TravelMode.Car));

        exception.Service.ShouldBe("OSRM");
    }

    [Fact]
    public async Task Tek_nokta_ile_rota_istenemez()
    {
        var client = CreateClient(new RecordingHandler(TripResponse()));

        await Should.ThrowAsync<RequestValidationException>(
            () => client.OptimizeTripAsync([Istanbul], TravelMode.Foot));
    }

    [Fact]
    public async Task Cok_fazla_durak_reddedilir()
    {
        // Gezgin satıcı çözümünün maliyeti durak sayısıyla hızla artıyor
        var client = CreateClient(new RecordingHandler(TripResponse()));

        var points = Enumerable.Range(0, 30)
            .Select(i => new GeoPoint(39 + i * 0.01, 35 + i * 0.01))
            .ToList();

        var exception = await Should.ThrowAsync<RequestValidationException>(
            () => client.OptimizeTripAsync(points, TravelMode.Car));

        exception.Errors.Values.SelectMany(x => x)
            .ShouldContain(x => x.Contains("en fazla", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(91.0, 30.0)]
    [InlineData(41.0, 181.0)]
    [InlineData(-91.0, 30.0)]
    public async Task Gecersiz_koordinat_reddedilir(double latitude, double longitude)
    {
        var client = CreateClient(new RecordingHandler(TripResponse()));

        await Should.ThrowAsync<RequestValidationException>(
            () => client.GetRouteAsync([Istanbul, new GeoPoint(latitude, longitude)], TravelMode.Car));
    }

    [Fact]
    public async Task Koridor_icin_geojson_cizgi_alinir()
    {
        // PostGIS koridor sorgusu GeoJSON bekliyor, polyline değil
        const string json = """
            {
              "code": "Ok",
              "routes": [{
                "distance": 100,
                "duration": 200,
                "geometry": { "type": "LineString", "coordinates": [[28.9,41.0],[30.7,36.9]] }
              }]
            }
            """;

        var handler = new RecordingHandler(json);
        var client = CreateClient(handler);

        var geoJson = await client.GetRouteGeoJsonAsync([Istanbul, Antalya], TravelMode.Car);

        handler.LastUrl.ShouldContain("geometries=geojson");
        geoJson.ShouldContain("LineString");
    }

    private static OsrmClient CreateClient(HttpMessageHandler handler)
    {
        var options = Options.Create(new OsrmOptions
        {
            CarBaseUrl = "http://car-sunucu:5000",
            FootBaseUrl = "http://foot-sunucu:5001",
            MaxWaypoints = 25
        });

        return new OsrmClient(new HttpClient(handler), options);
    }

    private static string TripResponse() => """
        {
          "code": "Ok",
          "trips": [{ "distance": 1000, "duration": 600, "geometry": "polyline" }],
          "waypoints": [{ "waypoint_index": 0 }, { "waypoint_index": 1 }]
        }
        """;

    /// <summary>İsteği kaydeden ve sabit yanıt döndüren sahte aktarım katmanı.</summary>
    private sealed class RecordingHandler(string response, HttpStatusCode statusCode = HttpStatusCode.OK)
        : HttpMessageHandler
    {
        public string LastUrl { get; private set; } = string.Empty;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastUrl = Uri.UnescapeDataString(request.RequestUri!.ToString());

            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(response)
            });
        }
    }
}
