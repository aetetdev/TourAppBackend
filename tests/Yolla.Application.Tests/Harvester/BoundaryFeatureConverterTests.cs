using NetTopologySuite.Geometries;
using Shouldly;
using Yolla.Domain.Enums;
using Yolla.Harvester.Import;
using Yolla.Harvester.Osm;

namespace Yolla.Application.Tests.Harvester;

public class BoundaryFeatureConverterTests
{
    private static readonly GeometryFactory Factory = new(new PrecisionModel(), 4326);

    [Fact]
    public void Il_siniri_cevrilir()
    {
        var feature = Boundary(
            osmId: 223474,
            ("name", "Çanakkale"),
            ("name:en", "Canakkale"),
            ("admin_level", "4"),
            ("ISO3166-2", "TR-17"));

        var row = BoundaryFeatureConverter.Convert(feature, expectedAdminLevel: 4);

        row.ShouldNotBeNull();
        row.OsmRelationId.ShouldBe(223474);
        row.Name.ShouldBe("Çanakkale");
        row.NameEn.ShouldBe("Canakkale");
        row.NameNormalized.ShouldBe("canakkale");
        row.Slug.ShouldBe("canakkale");
        row.Boundary.ShouldNotBeNull();
    }

    [Fact]
    public void Komsu_ulkenin_idari_birimi_elenir()
    {
        // Türkiye extract'i sınır bölgelerinde Yunanistan ve Bulgaristan kayıtları da içerir
        var feature = Boundary(1, ("name", "Evros"), ("admin_level", "4"), ("ISO3166-2", "GR-A"));

        BoundaryFeatureConverter.Convert(feature, expectedAdminLevel: 4).ShouldBeNull();
    }

    [Fact]
    public void Iso_kodu_olmayan_kayit_kabul_edilir()
    {
        // Türkiye'de bazı ilçelerde bu etiket bulunmuyor; eleme yapılmamalı
        var feature = Boundary(2, ("name", "Ayvacık"), ("admin_level", "6"));

        BoundaryFeatureConverter.Convert(feature, expectedAdminLevel: 6).ShouldNotBeNull();
    }

    [Fact]
    public void Yanlis_idari_seviye_elenir()
    {
        var feature = Boundary(3, ("name", "Test"), ("admin_level", "8"));

        BoundaryFeatureConverter.Convert(feature, expectedAdminLevel: 4).ShouldBeNull();
    }

    [Fact]
    public void Idari_seviye_etiketi_yoksa_elenir()
    {
        // osmium çıktısında il dosyasına mahalleler ve adalar da karışıyor;
        // bunların admin_level etiketi yok. Tek güvenilir ayraç bu etiket.
        var feature = Boundary(4, ("name", "Sıçan Adası"));

        BoundaryFeatureConverter.Convert(feature, expectedAdminLevel: 4).ShouldBeNull();
    }

    [Fact]
    public void Belediye_ve_mahalle_sinirlari_ilce_sayilmaz()
    {
        // İlçe dosyasındaki 13 bin kaydın 12 bini admin_level=8 (belediye/mahalle)
        var feature = Boundary(5, ("name", "Cumhuriyet Mahallesi"), ("admin_level", "8"));

        BoundaryFeatureConverter.Convert(feature, expectedAdminLevel: 6).ShouldBeNull();
    }

    [Fact]
    public void Alan_geometrisi_olmayan_kayit_elenir()
    {
        var feature = new OsmFeature
        {
            ElementType = OsmElementType.Relation,
            OsmId = 5,
            Tags = new Dictionary<string, string> { ["name"] = "Test", ["admin_level"] = "4" },
            Location = Factory.CreatePoint(new Coordinate(30, 40)),
            Area = null
        };

        BoundaryFeatureConverter.Convert(feature, expectedAdminLevel: 4).ShouldBeNull();
    }

    [Fact]
    public void Adsiz_sinir_elenir()
    {
        BoundaryFeatureConverter.Convert(Boundary(6, ("admin_level", "4")), expectedAdminLevel: 4)
            .ShouldBeNull();
    }

    [Fact]
    public void Farkli_ulke_on_eki_verilebilir()
    {
        var feature = Boundary(7, ("name", "Attiki"), ("admin_level", "4"), ("ISO3166-2", "GR-I"));

        BoundaryFeatureConverter.Convert(feature, 4, countryCodePrefix: "GR-").ShouldNotBeNull();
    }

    [Fact]
    public void Null_kayit_hata_firlatir()
    {
        Should.Throw<ArgumentNullException>(() => BoundaryFeatureConverter.Convert(null!, 4));
    }

    private static OsmFeature Boundary(long osmId, params (string Key, string Value)[] tags)
    {
        var ring = Factory.CreateLinearRing(
        [
            new Coordinate(0, 0),
            new Coordinate(0, 2),
            new Coordinate(2, 2),
            new Coordinate(2, 0),
            new Coordinate(0, 0)
        ]);

        var polygon = Factory.CreatePolygon(ring);

        return new OsmFeature
        {
            ElementType = OsmElementType.Relation,
            OsmId = osmId,
            Tags = tags.ToDictionary(t => t.Key, t => t.Value, StringComparer.OrdinalIgnoreCase),
            Location = Factory.CreatePoint(polygon.Centroid.Coordinate),
            Area = polygon
        };
    }
}
