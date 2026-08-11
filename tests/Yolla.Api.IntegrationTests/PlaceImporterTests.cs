using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Shouldly;
using Yolla.Domain.Entities;
using Yolla.Domain.Enums;
using Yolla.Harvester.Import;

namespace Yolla.Api.IntegrationTests;

[Collection(PostgisCollection.Name)]
public class PlaceImporterTests(PostgisFixture fixture)
{
    private static readonly GeometryFactory Factory = new(new PrecisionModel(), 4326);

    // Her testin kendi coğrafi bölgesi var. Aynı kareyi paylaşsalardı bir testin noktası
    // diğerinin şehir sınırına da düşer ve içe aktarma yanlış şehre bağlardı.
    private const double RegionWidth = 2.0;
    private const double RegionGap = 10.0;

    [Fact]
    public async Task Yer_sinir_poligonuna_gore_sehre_baglanir()
    {
        var cityId = await EnsureTestCityAsync("Baglanma", region: 0);
        var importer = new PlaceImporter(fixture.DataSource);

        var result = await importer.ImportAsync([Row(1001, "Test Müzesi", region: 0)], "TR");

        result.Inserted.ShouldBe(1);
        result.WithoutCity.ShouldBe(0);

        await using var context = fixture.CreateDbContext();
        var place = await context.Places.SingleAsync(x => x.OsmId == 1001);

        place.CityId.ShouldBe(cityId);
        place.Name.ShouldBe("Test Müzesi");
    }

    [Fact]
    public async Task Sinir_disindaki_yer_ice_aktarilmaz()
    {
        await EnsureTestCityAsync("SinirDisi", region: 1);
        var importer = new PlaceImporter(fixture.DataSource);

        // Hiçbir test bölgesine düşmeyen bir koordinat
        var result = await importer.ImportAsync(
            [RowAt(1002, "Denizdeki Yer", longitude: 179, latitude: 85)], "TR");

        result.Inserted.ShouldBe(0);
        result.WithoutCity.ShouldBe(1);
    }

    [Fact]
    public async Task Ayni_veri_tekrar_aktarilinca_kayit_cogalmaz()
    {
        await EnsureTestCityAsync("Idempotent", region: 2);
        var importer = new PlaceImporter(fixture.DataSource);

        var first = await importer.ImportAsync([Row(1003, "Tekrar Testi", region: 2)], "TR");
        var second = await importer.ImportAsync([Row(1003, "Tekrar Testi", region: 2)], "TR");

        first.Inserted.ShouldBe(1);
        second.Inserted.ShouldBe(0);
        second.Updated.ShouldBe(1);

        await using var context = fixture.CreateDbContext();
        (await context.Places.CountAsync(x => x.OsmId == 1003)).ShouldBe(1);
    }

    [Fact]
    public async Task Yeniden_ice_aktarma_zenginlestirme_verisini_ezmez()
    {
        // Bu test gerçek bir hatadan doğdu: içe aktarma, zenginleştirmenin eklediği
        // Wikipedia özetlerini OSM'deki boş description ile eziyordu ve kart için
        // hazır kayıt sayısı 1.890'dan 364'e düşmüştü.
        await EnsureTestCityAsync("Zenginlestirme", region: 3);
        var importer = new PlaceImporter(fixture.DataSource);

        await importer.ImportAsync([Row(1004, "Ayasofya", region: 3)], "TR");

        // Zenginleştirme adımını taklit et
        await using (var context = fixture.CreateDbContext())
        {
            var place = await context.Places.SingleAsync(x => x.OsmId == 1004);
            place.PhotoUrl = "https://upload.wikimedia.org/foto.jpg";
            place.PhotoAuthor = "Fotoğrafçı";
            place.PhotoLicense = "CC BY-SA 4.0";
            place.DescriptionTr = "Wikipedia'dan gelen uzun ve değerli açıklama.";
            place.QualityScore = 90;
            await context.SaveChangesAsync();
        }

        // Aynı OSM verisi yeniden içe aktarılıyor (açıklama etiketi yok)
        await importer.ImportAsync([Row(1004, "Ayasofya", region: 3)], "TR");

        await using var verification = fixture.CreateDbContext();
        var updated = await verification.Places.SingleAsync(x => x.OsmId == 1004);

        updated.DescriptionTr.ShouldBe("Wikipedia'dan gelen uzun ve değerli açıklama.");
        updated.PhotoUrl.ShouldBe("https://upload.wikimedia.org/foto.jpg");
        updated.PhotoLicense.ShouldBe("CC BY-SA 4.0");
        updated.QualityScore.ShouldBe((short)90);
    }

    [Fact]
    public async Task Kalite_puani_yeniden_aktarmada_dusurulmez()
    {
        await EnsureTestCityAsync("Puan", region: 4);
        var importer = new PlaceImporter(fixture.DataSource);

        await importer.ImportAsync([Row(1005, "Puan Testi", region: 4, qualityScore: 20)], "TR");

        await using (var context = fixture.CreateDbContext())
        {
            var place = await context.Places.SingleAsync(x => x.OsmId == 1005);
            place.QualityScore = 85;
            await context.SaveChangesAsync();
        }

        await importer.ImportAsync([Row(1005, "Puan Testi", region: 4, qualityScore: 20)], "TR");

        await using var verification = fixture.CreateDbContext();
        (await verification.Places.SingleAsync(x => x.OsmId == 1005)).QualityScore
            .ShouldBeGreaterThanOrEqualTo((short)85);
    }

    [Fact]
    public async Task Osm_etiketi_guncellendiginde_yeni_deger_yazilir()
    {
        await EnsureTestCityAsync("Guncelleme", region: 5);
        var importer = new PlaceImporter(fixture.DataSource);

        await importer.ImportAsync([Row(1006, "Eski Ad", region: 5)], "TR");
        await importer.ImportAsync([Row(1006, "Yeni Ad", region: 5)], "TR");

        await using var context = fixture.CreateDbContext();
        (await context.Places.SingleAsync(x => x.OsmId == 1006)).Name.ShouldBe("Yeni Ad");
    }

    [Fact]
    public async Task Kategori_agirligi_kalite_puanina_eklenir()
    {
        await EnsureTestCityAsync("Agirlik", region: 6);
        var importer = new PlaceImporter(fixture.DataSource);

        // museum kategorisinin ağırlığı 10 (CategorySeed)
        await importer.ImportAsync([Row(1007, "Ağırlık Testi", region: 6, qualityScore: 30)], "TR");

        await using var context = fixture.CreateDbContext();
        (await context.Places.SingleAsync(x => x.OsmId == 1007)).QualityScore.ShouldBe((short)40);
    }

    private static PlaceImportRow Row(
        long osmId,
        string name,
        int region,
        short qualityScore = 10) => new()
    {
        OsmType = OsmElementType.Node,
        OsmId = osmId,
        Name = name,
        Slug = name.ToLowerInvariant().Replace(' ', '-'),
        CategoryKey = "museum",
        Location = Factory.CreatePoint(new Coordinate(RegionOrigin(region) + 1, 1)),
        QualityScore = qualityScore
    };

    private static PlaceImportRow RowAt(long osmId, string name, double longitude, double latitude) => new()
    {
        OsmType = OsmElementType.Node,
        OsmId = osmId,
        Name = name,
        Slug = name.ToLowerInvariant().Replace(' ', '-'),
        CategoryKey = "museum",
        Location = Factory.CreatePoint(new Coordinate(longitude, latitude)),
        QualityScore = 10
    };

    private static double RegionOrigin(int region) => region * RegionGap;

    private async Task<int> EnsureTestCityAsync(string suffix, int region)
    {
        await using var context = fixture.CreateDbContext();

        var slug = $"test-{suffix.ToLowerInvariant()}";
        var existing = await context.Cities.FirstOrDefaultAsync(x => x.Slug == slug);

        if (existing is not null)
        {
            return existing.Id;
        }

        var origin = RegionOrigin(region);

        var ring = Factory.CreateLinearRing(
        [
            new Coordinate(origin, 0),
            new Coordinate(origin, RegionWidth),
            new Coordinate(origin + RegionWidth, RegionWidth),
            new Coordinate(origin + RegionWidth, 0),
            new Coordinate(origin, 0)
        ]);

        var city = new City
        {
            CountryId = 1,
            Name = $"Test {suffix}",
            NameNormalized = $"test {suffix.ToLowerInvariant()}",
            Slug = slug,
            Center = Factory.CreatePoint(new Coordinate(origin + 1, 1)),
            Boundary = Factory.CreatePolygon(ring),
            IsActive = true
        };

        context.Cities.Add(city);
        await context.SaveChangesAsync();

        return city.Id;
    }
}
