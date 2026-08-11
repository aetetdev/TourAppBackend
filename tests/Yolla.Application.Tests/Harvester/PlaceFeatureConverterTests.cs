using NetTopologySuite.Geometries;
using Shouldly;
using Yolla.Application.Places;
using Yolla.Domain.Enums;
using Yolla.Harvester.Import;
using Yolla.Harvester.Osm;

namespace Yolla.Application.Tests.Harvester;

public class PlaceFeatureConverterTests
{
    [Fact]
    public void Turistik_yer_iceri_aktarilabilir_satira_cevrilir()
    {
        var feature = Feature(
            ("tourism", "museum"),
            ("name", "Ayasofya"),
            ("name:en", "Hagia Sophia"),
            ("wikidata", "Q12506"),
            ("wikipedia", "tr:Ayasofya"),
            ("website", "https://ayasofyacamii.gov.tr"),
            ("opening_hours", "24/7"));

        var result = PlaceFeatureConverter.Convert(feature);

        result.IsConverted.ShouldBeTrue();
        result.SkipReason.ShouldBe(PlaceSkipReason.None);

        var row = result.Row!;
        row.Name.ShouldBe("Ayasofya");
        row.NameEn.ShouldBe("Hagia Sophia");
        row.Slug.ShouldBe("ayasofya");
        row.CategoryKey.ShouldBe("museum");
        row.WikidataId.ShouldBe("Q12506");
        row.WikipediaTitle.ShouldBe("Ayasofya");
        row.OsmType.ShouldBe(OsmElementType.Node);
        row.QualityScore.ShouldBeGreaterThan(PlaceQualityScorer.FeedThreshold);
    }

    [Fact]
    public void Adsiz_kayit_elenir()
    {
        var result = PlaceFeatureConverter.Convert(Feature(("tourism", "museum")));

        result.IsConverted.ShouldBeFalse();
        result.SkipReason.ShouldBe(PlaceSkipReason.NoName);
    }

    [Fact]
    public void Kategorisi_cozulemeyen_kayit_elenir()
    {
        var result = PlaceFeatureConverter.Convert(Feature(("amenity", "restaurant"), ("name", "Lokanta")));

        result.IsConverted.ShouldBeFalse();
        result.SkipReason.ShouldBe(PlaceSkipReason.NoCategory);
    }

    [Fact]
    public void Gizli_kategori_varsayilan_olarak_elenir()
    {
        var feature = Feature(("tourism", "hotel"), ("name", "Otel Test"));

        PlaceFeatureConverter.Convert(feature).SkipReason.ShouldBe(PlaceSkipReason.HiddenCategory);

        // İstenirse aktarılabilir: veri olarak tutulur, kullanıcıya gösterilmez
        var included = PlaceFeatureConverter.Convert(feature, includeHiddenCategories: true);
        included.IsConverted.ShouldBeTrue();
        included.Row!.CategoryKey.ShouldBe("accommodation");
    }

    [Fact]
    public void Adi_sadece_noktalamadan_olusan_kayit_elenir()
    {
        var result = PlaceFeatureConverter.Convert(Feature(("tourism", "museum"), ("name", "!!!")));

        result.SkipReason.ShouldBe(PlaceSkipReason.NoSlug);
    }

    [Fact]
    public void Slug_ayirt_edici_sonek_alabilir()
    {
        var feature = Feature(("historic", "castle"), ("name", "Kale"));

        PlaceFeatureConverter.Convert(feature, slugSuffix: "Çanakkale").Row!.Slug
            .ShouldBe("kale-canakkale");
    }

    [Fact]
    public void Fotograf_henuz_yokken_puan_hesaplanir()
    {
        // Zenginleştirme adımı fotoğrafı ekleyip puanı yeniden hesaplayacak
        var row = PlaceFeatureConverter.Convert(Feature(
            ("historic", "castle"),
            ("name", "Rumeli Hisarı"),
            ("wikidata", "Q1140215"))).Row!;

        row.QualityScore.ShouldBe((short)25);
    }

    [Fact]
    public void Null_kayit_hata_firlatir()
    {
        Should.Throw<ArgumentNullException>(() => PlaceFeatureConverter.Convert(null!));
    }

    // --- Adres birleştirme ---

    [Fact]
    public void Adres_bilesenleri_birlestirilir()
    {
        var feature = Feature(
            ("tourism", "museum"),
            ("name", "Test Müzesi"),
            ("addr:street", "Sultanahmet Meydanı"),
            ("addr:housenumber", "1"),
            ("addr:neighbourhood", "Sultanahmet"),
            ("addr:city", "İstanbul"));

        PlaceFeatureConverter.Convert(feature).Row!.Address
            .ShouldBe("Sultanahmet Meydanı No:1, Sultanahmet, İstanbul");
    }

    [Fact]
    public void Adres_bilgisi_yoksa_null_kalir()
    {
        PlaceFeatureConverter.Convert(Feature(("tourism", "museum"), ("name", "Test Müzesi")))
            .Row!.Address.ShouldBeNull();
    }

    [Fact]
    public void Kapi_numarasi_tek_basina_adres_sayilmaz()
    {
        var feature = Feature(("tourism", "museum"), ("name", "Test"), ("addr:housenumber", "5"));

        PlaceFeatureConverter.Convert(feature).Row!.Address.ShouldBeNull();
    }

    // --- Wikidata kimliği ---

    [Theory]
    [InlineData("Q12506", "Q12506")]
    [InlineData("q12506", "Q12506")]
    [InlineData("  Q42  ", "Q42")]
    public void Gecerli_wikidata_kimligi_kabul_edilir(string input, string expected)
    {
        PlaceFeatureConverter.CleanWikidataId(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData("12506")]
    [InlineData("Q")]
    [InlineData("QABC")]
    [InlineData("Q123X")]
    [InlineData("")]
    [InlineData(null)]
    public void Gecersiz_wikidata_kimligi_reddedilir(string? input)
    {
        PlaceFeatureConverter.CleanWikidataId(input).ShouldBeNull();
    }

    // --- Wikipedia başlığı ---

    [Theory]
    [InlineData("tr:Ayasofya", "Ayasofya")]
    [InlineData("en:Hagia Sophia", "Hagia Sophia")]
    [InlineData("Ayasofya", "Ayasofya")]
    public void Wikipedia_basligindan_dil_on_eki_ayrilir(string input, string expected)
    {
        PlaceFeatureConverter.CleanWikipediaTitle(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData("https://tr.wikipedia.org/wiki/Ayasofya")]
    [InlineData("")]
    [InlineData(null)]
    public void Gecersiz_wikipedia_degeri_reddedilir(string? input)
    {
        PlaceFeatureConverter.CleanWikipediaTitle(input).ShouldBeNull();
    }

    // --- Web adresi ---

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://example.com/muze")]
    public void Http_adresleri_kabul_edilir(string input)
    {
        PlaceFeatureConverter.CleanWebsite(input).ShouldBe(input);
    }

    [Theory]
    [InlineData("example.com")]
    [InlineData("ftp://example.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("")]
    [InlineData(null)]
    public void Gecersiz_web_adresi_reddedilir(string? input)
    {
        PlaceFeatureConverter.CleanWebsite(input).ShouldBeNull();
    }

    [Fact]
    public void Contact_website_etiketi_de_kullanilir()
    {
        var feature = Feature(
            ("tourism", "museum"),
            ("name", "Test Müzesi"),
            ("contact:website", "https://muze.gov.tr"));

        PlaceFeatureConverter.Convert(feature).Row!.Website.ShouldBe("https://muze.gov.tr");
    }

    // --- Commons referansı ---

    [Theory]
    [InlineData("Category:Underground City of Kaymaklı", "Category:Underground City of Kaymaklı")]
    [InlineData("File:Duden.jpg", "File:Duden.jpg")]
    [InlineData("Category:Cape_Helles", "Category:Cape Helles")]
    public void Osm_commons_etiketi_okunur(string tagValue, string expected)
    {
        var feature = Feature(("historic", "castle"), ("name", "Test"), ("wikimedia_commons", tagValue));

        PlaceFeatureConverter.Convert(feature).Row!.CommonsRef.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Ayasofya")]
    [InlineData("https://commons.wikimedia.org/wiki/File:X.jpg")]
    [InlineData("")]
    [InlineData(null)]
    public void Gecersiz_commons_referansi_reddedilir(string? value)
    {
        PlaceFeatureConverter.CleanCommonsRef(value).ShouldBeNull();
    }

    // --- Uzunluk sınırları ---

    [Fact]
    public void Asiri_uzun_ad_veritabani_sinirina_kirpilir()
    {
        var longName = new string('a', 400);

        var row = PlaceFeatureConverter.Convert(Feature(("tourism", "museum"), ("name", longName))).Row!;

        row.Name.Length.ShouldBe(250);
        row.Slug.Length.ShouldBeLessThanOrEqualTo(280);
    }

    private static OsmFeature Feature(params (string Key, string Value)[] tags) => new()
    {
        ElementType = OsmElementType.Node,
        OsmId = 1,
        Tags = tags.ToDictionary(t => t.Key, t => t.Value, StringComparer.OrdinalIgnoreCase),
        Location = new GeometryFactory(new PrecisionModel(), 4326)
            .CreatePoint(new Coordinate(28.98, 41.01))
    };
}
