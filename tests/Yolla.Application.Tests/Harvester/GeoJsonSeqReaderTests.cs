using NetTopologySuite.Geometries;
using Shouldly;
using Yolla.Domain.Enums;
using Yolla.Harvester.Osm;

namespace Yolla.Application.Tests.Harvester;

public class GeoJsonSeqReaderTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    [Fact]
    public void Nokta_geometrili_kayit_okunur()
    {
        var path = WriteLines(
            """{"type":"Feature","id":"n123","properties":{"tourism":"museum","name":"Ayasofya"},"geometry":{"type":"Point","coordinates":[28.9800,41.0086]}}""");

        var features = new GeoJsonSeqReader().Read(path).ToList();

        features.Count.ShouldBe(1);
        features[0].ElementType.ShouldBe(OsmElementType.Node);
        features[0].OsmId.ShouldBe(123);
        features[0].GetTag("name").ShouldBe("Ayasofya");
        features[0].Location.X.ShouldBe(28.9800, 0.0001);
        features[0].Location.Y.ShouldBe(41.0086, 0.0001);
        features[0].Location.SRID.ShouldBe(4326);
        features[0].Area.ShouldBeNull();
    }

    [Fact]
    public void Alan_geometrili_kayitta_merkez_nokta_hesaplanir()
    {
        // Kale, milli park gibi yerler alan olarak çizilir; kartta tek nokta göstermemiz gerekir
        var path = WriteLines(
            """{"type":"Feature","id":"w500","properties":{"historic":"castle"},"geometry":{"type":"Polygon","coordinates":[[[0,0],[0,2],[2,2],[2,0],[0,0]]]}}""");

        var feature = new GeoJsonSeqReader().Read(path).Single();

        feature.ElementType.ShouldBe(OsmElementType.Way);
        feature.Location.X.ShouldBe(1.0, 0.0001);
        feature.Location.Y.ShouldBe(1.0, 0.0001);
        feature.Area.ShouldNotBeNull();
        feature.Area.ShouldBeOfType<Polygon>();
    }

    [Fact]
    public void Coklu_alan_geometrisi_okunur()
    {
        // İl sınırları çoğunlukla MultiPolygon (adalar, eksklavlar)
        var path = WriteLines(
            """{"type":"Feature","id":"r700","properties":{"admin_level":"4","name":"İzmir"},"geometry":{"type":"MultiPolygon","coordinates":[[[[0,0],[0,2],[2,2],[2,0],[0,0]]],[[[10,10],[10,11],[11,11],[11,10],[10,10]]]]}}""");

        var feature = new GeoJsonSeqReader().Read(path).Single();

        feature.ElementType.ShouldBe(OsmElementType.Relation);
        feature.OsmId.ShouldBe(700);
        feature.GetTag("name").ShouldBe("İzmir");
        feature.Area.ShouldBeOfType<MultiPolygon>();
    }

    [Fact]
    public void Kayit_ayirici_karakteri_temizlenir()
    {
        // RFC 8142: osmium her satırın başına 0x1E koyar
        var path = WriteLines(
            "" + """{"type":"Feature","id":"n1","properties":{"tourism":"viewpoint"},"geometry":{"type":"Point","coordinates":[30,40]}}""");

        new GeoJsonSeqReader().Read(path).Count().ShouldBe(1);
    }

    [Fact]
    public void Bozuk_satir_tum_ice_aktarimi_durdurmaz()
    {
        var path = WriteLines(
            """{"type":"Feature","id":"n1","properties":{},"geometry":{"type":"Point","coordinates":[30,40]}}""",
            """{"type":"Feature","id":"n2","properties":{,,,""",
            """{"type":"Feature","id":"n3","properties":{},"geometry":{"type":"Point","coordinates":[31,41]}}""");

        var reader = new GeoJsonSeqReader();
        var features = reader.Read(path).ToList();

        features.Count.ShouldBe(2);
        reader.SkippedLineCount.ShouldBe(1);
    }

    [Fact]
    public void Gecersiz_koordinatli_kayit_atlanir()
    {
        var path = WriteLines(
            """{"type":"Feature","id":"n1","properties":{},"geometry":{"type":"Point","coordinates":[999,41]}}""",
            """{"type":"Feature","id":"n2","properties":{},"geometry":{"type":"Point","coordinates":[30,40]}}""");

        var reader = new GeoJsonSeqReader();

        reader.Read(path).Count().ShouldBe(1);
        reader.SkippedLineCount.ShouldBe(1);
    }

    [Fact]
    public void Geometrisi_olmayan_kayit_atlanir()
    {
        var path = WriteLines(
            """{"type":"Feature","id":"n1","properties":{"tourism":"museum"}}""");

        new GeoJsonSeqReader().Read(path).ShouldBeEmpty();
    }

    [Fact]
    public void Bos_satirlar_yok_sayilir()
    {
        var path = WriteLines(
            "",
            """{"type":"Feature","id":"n1","properties":{},"geometry":{"type":"Point","coordinates":[30,40]}}""",
            "   ");

        var reader = new GeoJsonSeqReader();

        reader.Read(path).Count().ShouldBe(1);
        reader.SkippedLineCount.ShouldBe(0);
    }

    [Fact]
    public void Sayisal_ve_mantiksal_etiket_degerleri_metne_cevrilir()
    {
        var path = WriteLines(
            """{"type":"Feature","id":"n1","properties":{"admin_level":4,"wikipedia":true},"geometry":{"type":"Point","coordinates":[30,40]}}""");

        var feature = new GeoJsonSeqReader().Read(path).Single();

        feature.GetTag("admin_level").ShouldBe("4");
        feature.GetTag("wikipedia").ShouldBe("yes");
    }

    [Fact]
    public void Etiket_aramasi_buyuk_kucuk_harf_duyarsizdir()
    {
        var path = WriteLines(
            """{"type":"Feature","id":"n1","properties":{"Name":"Test"},"geometry":{"type":"Point","coordinates":[30,40]}}""");

        new GeoJsonSeqReader().Read(path).Single().GetTag("name").ShouldBe("Test");
    }

    [Fact]
    public void Olmayan_dosya_anlamli_hata_verir()
    {
        var reader = new GeoJsonSeqReader();

        Should.Throw<FileNotFoundException>(() => reader.Read("yok-boyle-bir-dosya.geojsonl").ToList());
    }

    [Theory]
    [InlineData("n123", OsmElementType.Node, 123L)]
    [InlineData("w456", OsmElementType.Way, 456L)]
    [InlineData("r789", OsmElementType.Relation, 789L)]
    [InlineData("N1", OsmElementType.Node, 1L)]
    public void Osm_kimligi_cozulur(string value, OsmElementType expectedType, long expectedId)
    {
        GeoJsonSeqReader.TryParseOsmId(value, out var type, out var id).ShouldBeTrue();

        type.ShouldBe(expectedType);
        id.ShouldBe(expectedId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("x123")]
    [InlineData("n")]
    [InlineData("nabc")]
    [InlineData("n0")]
    [InlineData("n-5")]
    [InlineData(null)]
    public void Gecersiz_osm_kimligi_reddedilir(string? value)
    {
        GeoJsonSeqReader.TryParseOsmId(value, out _, out _).ShouldBeFalse();
    }

    private string WriteLines(params string[] lines)
    {
        var path = Path.Combine(Path.GetTempPath(), $"yolla-test-{Guid.NewGuid():N}.geojsonl");
        File.WriteAllLines(path, lines);
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles.Where(File.Exists))
        {
            File.Delete(file);
        }

        GC.SuppressFinalize(this);
    }
}
