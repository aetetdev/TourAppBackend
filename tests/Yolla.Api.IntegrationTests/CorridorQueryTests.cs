using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Shouldly;
using Yolla.Application.Routing;
using Yolla.Domain.Entities;
using Yolla.Domain.Enums;
using Yolla.Infrastructure.Services;

namespace Yolla.Api.IntegrationTests;

/// <summary>
/// Koridor sorgusunu gerçek PostGIS'e karşı doğrular.
/// </summary>
/// <remarks>
/// Rota motoru sahteleniyor: sorgunun girdisi zaten bir GeoJSON çizgisi, motorun kendisi
/// değil. Böylece 4 GB'lık yol grafiğini yüklemeden PostGIS mantığı test edilebiliyor.
///
/// Test rotası doğu-batı doğrultusunda düz bir çizgi; yerler bu çizgi boyunca ve ondan
/// belirli uzaklıklara yerleştiriliyor.
/// </remarks>
[Collection(PostgisCollection.Name)]
public class CorridorQueryTests(PostgisFixture fixture) : IAsyncLifetime
{
    private static readonly GeometryFactory Factory = new(new PrecisionModel(), 4326);

    // Test bölgesi: enlem 10, boylam 30'dan 34'e uzanan düz bir yol
    private const double RouteLatitude = 10.0;
    private const double StartLongitude = 30.0;
    private const double EndLongitude = 34.0;

    private const string RouteGeoJson =
        """{"type":"LineString","coordinates":[[30.0,10.0],[31.0,10.0],[32.0,10.0],[33.0,10.0],[34.0,10.0]]}""";

    private RouteService _service = null!;

    public async Task InitializeAsync()
    {
        await SeedAsync();

        _service = new RouteService(fixture.CreateDbContext(), new FakeRoutingClient(RouteGeoJson));
    }

    [Fact]
    public async Task Koridordaki_yerler_yol_sirasina_gore_gelir()
    {
        var feed = await GetCorridorAsync();

        feed.Cards.Items.ShouldNotBeEmpty();

        var progressValues = feed.Cards.Items.Select(x => x.RouteProgress ?? 0).ToList();

        // Çeşitlendirme eşit değerdekilerin yerini değiştirebilir; genel eğilim artan olmalı
        progressValues.First().ShouldBeLessThanOrEqualTo(progressValues.Last());

        for (var i = 1; i < progressValues.Count; i++)
        {
            (progressValues[i] - progressValues[i - 1]).ShouldBeGreaterThan(-0.2,
                "kartlar yol boyunca ilerlemeli, geriye sıçramamalı");
        }
    }

    [Fact]
    public async Task Yoldan_uzaktaki_yer_koridora_girmez()
    {
        var feed = await GetCorridorAsync(bufferKm: 15);

        // "Uzak Yer" yoldan 100 km ötede
        feed.Cards.Items.ShouldNotContain(x => x.Name == "Uzak Yer");
    }

    [Fact]
    public async Task Baslangic_ve_varis_cevresi_haric_tutulur()
    {
        // Kullanıcı çıktığı şehrin merkezini zaten biliyor; ilk sayfayı o yerler doldurmamalı
        var feed = await GetCorridorAsync(excludeEndpointsKm: 50);

        feed.Cards.Items.ShouldNotContain(x => x.Name == "Baslangic Yakini");
        feed.Cards.Items.ShouldNotContain(x => x.Name == "Varis Yakini");
    }

    [Fact]
    public async Task Haric_tutma_kapatilinca_uc_noktalar_da_gelir()
    {
        var feed = await GetCorridorAsync(excludeEndpointsKm: 0);

        feed.Cards.Items.ShouldContain(x => x.Name == "Baslangic Yakini");
    }

    [Fact]
    public async Task Tek_bolgedeki_yigilma_tum_sayfayi_doldurmaz()
    {
        // Gerçek veride İstanbul-Antalya sorgusu ilk sayfada yalnızca Topkapı çevresindeki
        // 15 yeri döndürüyordu. Dilimleme bunu engellemeli.
        var feed = await GetCorridorAsync(take: 10, excludeEndpointsKm: 0);

        var yiginBolgesi = feed.Cards.Items.Count(x => x.Name.StartsWith("Yigin", StringComparison.Ordinal));

        yiginBolgesi.ShouldBeLessThan(10, "tek bir yoğun bölge tüm sayfayı doldurmamalı");
    }

    [Fact]
    public async Task Fotografsiz_ve_atifsiz_yerler_gelmez()
    {
        var feed = await GetCorridorAsync(excludeEndpointsKm: 0);

        feed.Cards.Items.ShouldNotContain(x => x.Name == "Fotografsiz Koridor Yeri");
        feed.Cards.Items.ShouldNotContain(x => x.Name == "Atifsiz Koridor Yeri");
        feed.Cards.Items.ShouldAllBe(x => !string.IsNullOrWhiteSpace(x.PhotoAttribution));
    }

    [Fact]
    public async Task Kartlarda_yol_konumu_ve_sapma_mesafesi_dolu_gelir()
    {
        var feed = await GetCorridorAsync();

        feed.Cards.Items.ShouldAllBe(x => x.RouteProgress != null && x.DetourMeters != null);
        feed.Cards.Items.ShouldAllBe(x => x.RouteProgress >= 0 && x.RouteProgress <= 1);
    }

    [Fact]
    public async Task Imlec_ile_sonraki_sayfa_tekrar_etmez()
    {
        var first = await GetCorridorAsync(take: 3, excludeEndpointsKm: 0);

        first.Cards.HasMore.ShouldBeTrue();

        var second = await GetCorridorAsync(take: 3, excludeEndpointsKm: 0, cursor: first.Cards.NextCursor);

        var firstIds = first.Cards.Items.Select(x => x.Id).ToHashSet();

        second.Cards.Items.ShouldAllBe(x => !firstIds.Contains(x.Id));

        // İkinci sayfa yolda daha ileride olmalı
        var lastOfFirst = first.Cards.Items.Max(x => x.RouteProgress ?? 0);
        second.Cards.Items.ShouldAllBe(x => x.RouteProgress >= lastOfFirst);
    }

    [Fact]
    public async Task Kategori_suzgeci_koridorda_da_gecerli()
    {
        var feed = await GetCorridorAsync(categories: ["castle"], excludeEndpointsKm: 0);

        feed.Cards.Items.ShouldNotBeEmpty();
        feed.Cards.Items.ShouldAllBe(x => x.CategoryKey == "castle");
    }

    private async Task<CorridorFeedDto> GetCorridorAsync(
        int bufferKm = 15,
        int take = 20,
        int excludeEndpointsKm = 20,
        string? cursor = null,
        IReadOnlyList<string>? categories = null) =>
        await _service.GetCorridorFeedAsync(new CorridorFeedRequest
        {
            Start = new GeoPoint(RouteLatitude, StartLongitude),
            End = new GeoPoint(RouteLatitude, EndLongitude),
            BufferKm = bufferKm,
            Take = take,
            ExcludeEndpointsKm = excludeEndpointsKm,
            Cursor = cursor,
            CategoryKeys = categories
        });

    private async Task SeedAsync()
    {
        await using var context = fixture.CreateDbContext();

        const string slug = "test-koridor-sehri";

        if (await context.Cities.AnyAsync(x => x.Slug == slug))
        {
            return;
        }

        var ring = Factory.CreateLinearRing(
        [
            new Coordinate(29, 8), new Coordinate(29, 12),
            new Coordinate(35, 12), new Coordinate(35, 8), new Coordinate(29, 8)
        ]);

        var city = new City
        {
            CountryId = 1,
            Name = "Test Koridor Şehri",
            NameNormalized = "test koridor sehri",
            Slug = slug,
            Center = Factory.CreatePoint(new Coordinate(32, 10)),
            Boundary = Factory.CreatePolygon(ring),
            IsActive = true
        };

        context.Cities.Add(city);
        await context.SaveChangesAsync();

        var museum = await context.Categories.Where(x => x.Key == "museum").Select(x => x.Id).FirstAsync();
        var castle = await context.Categories.Where(x => x.Key == "castle").Select(x => x.Id).FirstAsync();

        var osmId = 70000;
        var places = new List<Place>();

        // Yol boyunca dağılmış yerler
        for (var i = 1; i <= 8; i++)
        {
            var longitude = StartLongitude + i * 0.4;
            places.Add(NewPlace(city.Id, museum, $"Yol Ustu Yer {i}", longitude, RouteLatitude, osmId++, 60));
        }

        // Tek noktada yığılma: dilimlemenin işe yaradığını görmek için
        for (var i = 1; i <= 12; i++)
        {
            places.Add(NewPlace(city.Id, museum, $"Yigin Yeri {i}",
                32.0 + i * 0.001, RouteLatitude, osmId++, (short)(90 - i)));
        }

        // Kale kategorisi: süzgeç testi
        places.Add(NewPlace(city.Id, castle, "Koridor Kalesi", 31.5, RouteLatitude, osmId++, 70));

        // Uç noktalara yakın olanlar
        places.Add(NewPlace(city.Id, museum, "Baslangic Yakini", 30.05, RouteLatitude, osmId++, 80));
        places.Add(NewPlace(city.Id, museum, "Varis Yakini", 33.95, RouteLatitude, osmId++, 80));

        // Koridor dışı: yoldan yaklaşık 110 km kuzeyde
        places.Add(NewPlace(city.Id, museum, "Uzak Yer", 32.0, RouteLatitude + 1.0, osmId++, 90));

        // Gösterilmemesi gerekenler
        var withoutPhoto = NewPlace(city.Id, museum, "Fotografsiz Koridor Yeri", 32.5, RouteLatitude, osmId++, 80);
        withoutPhoto.PhotoUrl = null;
        withoutPhoto.PhotoAuthor = null;
        withoutPhoto.PhotoLicense = null;
        places.Add(withoutPhoto);

        var withoutAttribution = NewPlace(city.Id, museum, "Atifsiz Koridor Yeri", 32.6, RouteLatitude, osmId++, 80);
        withoutAttribution.PhotoAuthor = null;
        withoutAttribution.PhotoLicense = null;
        places.Add(withoutAttribution);

        context.Places.AddRange(places);
        await context.SaveChangesAsync();
    }

    private static Place NewPlace(
        int cityId, int categoryId, string name,
        double longitude, double latitude, long osmId, short score) => new()
    {
        CountryId = 1,
        CityId = cityId,
        CategoryId = categoryId,
        OsmType = OsmElementType.Node,
        OsmId = osmId,
        Name = name,
        Slug = name.ToLowerInvariant().Replace(' ', '-'),
        Location = Factory.CreatePoint(new Coordinate(longitude, latitude)),
        PhotoUrl = "https://upload.wikimedia.org/test.jpg",
        PhotoAuthor = "Test Fotoğrafçı",
        PhotoLicense = "CC BY-SA 4.0",
        DescriptionTr = "Test açıklaması.",
        QualityScore = score,
        IsActive = true
    };

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>Sabit bir rota çizgisi döndüren sahte rota motoru.</summary>
    private sealed class FakeRoutingClient(string geoJson) : IRoutingClient
    {
        public Task<RouteResult> OptimizeTripAsync(
            IReadOnlyList<GeoPoint> points, TravelMode travelMode, bool roundTrip = false,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(FakeResult(points.Count));

        public Task<RouteResult> GetRouteAsync(
            IReadOnlyList<GeoPoint> points, TravelMode travelMode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(FakeResult(points.Count));

        public Task<string> GetRouteGeoJsonAsync(
            IReadOnlyList<GeoPoint> points, TravelMode travelMode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(geoJson);

        private static RouteResult FakeResult(int pointCount) => new()
        {
            DistanceMeters = 440_000,
            DurationSeconds = 18_000,
            Geometry = "sahte_polyline",
            WaypointOrder = Enumerable.Range(0, pointCount).ToList()
        };
    }
}
