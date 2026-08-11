using Shouldly;
using Yolla.Harvester.Enrichment;

namespace Yolla.Application.Tests.Harvester;

public class WikidataResponseParserTests
{
    [Fact]
    public void Fotograf_ve_makale_basliklari_cozulur()
    {
        const string json = """
            {"entities":{"Q12506":{
                "claims":{"P18":[{"mainsnak":{"datavalue":{"value":"Hagia Sophia Mars 2013.jpg"}}}]},
                "sitelinks":{"trwiki":{"title":"Ayasofya"},"enwiki":{"title":"Hagia Sophia"}}
            }}}
            """;

        var entity = WikidataResponseParser.Parse(json)["Q12506"];

        entity.ImageFileName.ShouldBe("Hagia Sophia Mars 2013.jpg");
        entity.TurkishWikipediaTitle.ShouldBe("Ayasofya");
        entity.EnglishWikipediaTitle.ShouldBe("Hagia Sophia");
    }

    [Fact]
    public void Fotografi_olmayan_kayit_cozulur()
    {
        const string json = """{"entities":{"Q1":{"claims":{},"sitelinks":{"trwiki":{"title":"Test"}}}}}""";

        var entity = WikidataResponseParser.Parse(json)["Q1"];

        entity.ImageFileName.ShouldBeNull();
        entity.TurkishWikipediaTitle.ShouldBe("Test");
    }

    [Fact]
    public void Silinmis_kayit_atlanir()
    {
        const string json = """{"entities":{"Q999":{"id":"Q999","missing":""}}}""";

        WikidataResponseParser.Parse(json).ShouldBeEmpty();
    }

    [Fact]
    public void Birden_fazla_gorselden_ilki_alinir()
    {
        const string json = """
            {"entities":{"Q1":{"claims":{"P18":[
                {"mainsnak":{"datavalue":{"value":"Birinci.jpg"}}},
                {"mainsnak":{"datavalue":{"value":"Ikinci.jpg"}}}
            ]}}}}
            """;

        WikidataResponseParser.Parse(json)["Q1"].ImageFileName.ShouldBe("Birinci.jpg");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bozuk json {")]
    [InlineData("{}")]
    [InlineData("""{"entities":null}""")]
    public void Gecersiz_yanit_bos_sonuc_dondurur(string? json)
    {
        WikidataResponseParser.Parse(json).ShouldBeEmpty();
    }
}

public class CommonsResponseParserTests
{
    [Fact]
    public void Fotograf_ve_atif_bilgisi_cozulur()
    {
        const string json = """
            {"query":{"pages":{"123":{
                "title":"File:Ayasofya.jpg",
                "imageinfo":[{
                    "url":"https://upload.wikimedia.org/wikipedia/commons/a/ab/Ayasofya.jpg",
                    "descriptionurl":"https://commons.wikimedia.org/wiki/File:Ayasofya.jpg",
                    "extmetadata":{
                        "Artist":{"value":"<a href=\"//commons.wikimedia.org/wiki/User:Foo\">Ali Veli</a>"},
                        "LicenseShortName":{"value":"CC BY-SA 4.0"}
                    }
                }]
            }}}}
            """;

        var photo = CommonsResponseParser.Parse(json)["File:Ayasofya.jpg"];

        photo.Url.ShouldBe("https://upload.wikimedia.org/wikipedia/commons/a/ab/Ayasofya.jpg");
        photo.Author.ShouldBe("Ali Veli");
        photo.License.ShouldBe("CC BY-SA 4.0");
        photo.DescriptionUrl.ShouldBe("https://commons.wikimedia.org/wiki/File:Ayasofya.jpg");
    }

    [Fact]
    public void Kisitli_lisansli_gorsel_kullanilmaz()
    {
        const string json = """
            {"query":{"pages":{"1":{
                "title":"File:Test.jpg",
                "imageinfo":[{"url":"https://example.com/t.jpg",
                    "extmetadata":{"LicenseShortName":{"value":"Fair use"}}}]
            }}}}
            """;

        CommonsResponseParser.Parse(json).ShouldBeEmpty();
    }

    [Fact]
    public void Adresi_olmayan_kayit_atlanir()
    {
        const string json = """{"query":{"pages":{"1":{"title":"File:Test.jpg","imageinfo":[{}]}}}}""";

        CommonsResponseParser.Parse(json).ShouldBeEmpty();
    }

    [Fact]
    public void Bulunamayan_dosya_atlanir()
    {
        const string json = """{"query":{"pages":{"-1":{"title":"File:Yok.jpg","missing":""}}}}""";

        CommonsResponseParser.Parse(json).ShouldBeEmpty();
    }

    [Fact]
    public void Lisans_bilgisi_eksik_olsa_da_gorsel_alinir()
    {
        // Atıf alanları boş kalır ama URL kullanılabilir; gösterimde lisans kontrolü yapılır
        const string json = """
            {"query":{"pages":{"1":{"title":"File:T.jpg",
                "imageinfo":[{"url":"https://example.com/t.jpg"}]}}}}
            """;

        var photo = CommonsResponseParser.Parse(json)["File:T.jpg"];

        photo.Url.ShouldBe("https://example.com/t.jpg");
        photo.Author.ShouldBeNull();
        photo.License.ShouldBeNull();
    }

    [Theory]
    [InlineData("<a href=\"x\">Ali Veli</a>", "Ali Veli")]
    [InlineData("<span class=\"x\">Ahmet</span> (fotoğraf)", "Ahmet (fotoğraf)")]
    [InlineData("Mehmet &amp; Ayşe", "Mehmet & Ayşe")]
    [InlineData("<div>Bir</div><div>İki</div>", "Bir İki")]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void Yazar_alanindaki_html_temizlenir(string? input, string? expected)
    {
        CommonsResponseParser.StripHtml(input).ShouldBe(expected);
    }

    [Fact]
    public void Adresteki_izleme_parametreleri_temizlenir()
    {
        const string json = """
            {"query":{"pages":{"1":{"title":"File:T.jpg","imageinfo":[{
                "url":"https://upload.wikimedia.org/commons/f/f3/T.jpg?utm_source=commons.wikimedia.org&utm_campaign=imageinfo"
            }]}}}}
            """;

        CommonsResponseParser.Parse(json)["File:T.jpg"].Url
            .ShouldBe("https://upload.wikimedia.org/commons/f/f3/T.jpg");
    }

    [Fact]
    public void Parametresiz_adres_degistirilmez()
    {
        CommonsResponseParser.StripTrackingParameters("https://example.com/a.jpg")
            .ShouldBe("https://example.com/a.jpg");
    }

    [Fact]
    public void Kategori_uyeleri_cozulur()
    {
        // OSM etiketi çoğunlukla dosyayı değil kategoriyi gösterir
        const string json = """
            {"query":{"categorymembers":[
                {"title":"File:Kaymakli 1.jpg"},
                {"title":"File:Kaymakli 2.png"},
                {"title":"File:Kaymakli sesli anlatim.ogg"},
                {"title":"Category:Alt kategori"}
            ]}}
            """;

        var files = CommonsResponseParser.ParseCategoryMembers(json);

        // Ses dosyası ve alt kategori elenmeli
        files.ShouldBe(["File:Kaymakli 1.jpg", "File:Kaymakli 2.png"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("bozuk {")]
    [InlineData("{}")]
    public void Gecersiz_kategori_yaniti_bos_liste_dondurur(string? json)
    {
        CommonsResponseParser.ParseCategoryMembers(json).ShouldBeEmpty();
    }

    [Fact]
    public void Wikidata_dosya_adi_commons_basligina_cevrilir()
    {
        CommonsClient.NormalizeFileTitle("Ayasofya.jpg").ShouldBe("File:Ayasofya.jpg");
        CommonsClient.NormalizeFileTitle("File:Ayasofya.jpg").ShouldBe("File:Ayasofya.jpg");
        CommonsClient.NormalizeFileTitle("Hagia_Sophia_2013.jpg").ShouldBe("File:Hagia Sophia 2013.jpg");
    }
}

public class WikipediaResponseParserTests
{
    [Fact]
    public void Makale_ozeti_cozulur()
    {
        const string json = """
            {"query":{"pages":{"123":{"title":"Ayasofya","extract":"Ayasofya, İstanbul'da bulunan tarihi yapı."}}}}
            """;

        WikipediaResponseParser.Parse(json)["Ayasofya"]
            .ShouldBe("Ayasofya, İstanbul'da bulunan tarihi yapı.");
    }

    [Fact]
    public void Yonlendirilen_baslik_da_ayni_ozete_isaret_eder()
    {
        // Wikidata "Ayasofya Camii" derken Wikipedia makalesi "Ayasofya" olabilir
        const string json = """
            {"query":{
                "redirects":[{"from":"Ayasofya Camii","to":"Ayasofya"}],
                "pages":{"1":{"title":"Ayasofya","extract":"Tarihi yapı."}}
            }}
            """;

        var extracts = WikipediaResponseParser.Parse(json);

        extracts["Ayasofya"].ShouldBe("Tarihi yapı.");
        extracts["Ayasofya Camii"].ShouldBe("Tarihi yapı.");
    }

    [Fact]
    public void Bulunamayan_makale_atlanir()
    {
        const string json = """{"query":{"pages":{"-1":{"title":"Yok","missing":""}}}}""";

        WikipediaResponseParser.Parse(json).ShouldBeEmpty();
    }

    [Fact]
    public void Bos_ozetli_makale_atlanir()
    {
        const string json = """{"query":{"pages":{"1":{"title":"Test","extract":""}}}}""";

        WikipediaResponseParser.Parse(json).ShouldBeEmpty();
    }

    [Fact]
    public void Uzun_ozet_cumle_sonundan_kesilir()
    {
        var text = "Birinci cümle burada. İkinci cümle de burada. " + new string('x', 600);

        var result = WikipediaResponseParser.Shorten(text, 60);

        result.ShouldBe("Birinci cümle burada. İkinci cümle de burada.");
    }

    [Fact]
    public void Cumle_sonu_metnin_cok_basindaysa_kelime_sinirindan_kesilir()
    {
        // "Ay." gibi kısa bir ilk cümleden sonra kesmek özeti işe yaramaz hale getirirdi
        var text = "Kısa. " + string.Join(' ', Enumerable.Repeat("devam", 100));

        var result = WikipediaResponseParser.Shorten(text, 100);

        result.ShouldEndWith("…");
        result.Length.ShouldBeGreaterThan(50);
    }

    [Fact]
    public void Cumle_sonu_yoksa_kelime_sinirindan_kesilir()
    {
        var text = string.Join(' ', Enumerable.Repeat("kelime", 100));

        var result = WikipediaResponseParser.Shorten(text, 50);

        result.Length.ShouldBeLessThanOrEqualTo(51);
        result.ShouldEndWith("…");
        result.ShouldNotContain("kelimek");
    }

    [Fact]
    public void Kisa_ozet_oldugu_gibi_kalir()
    {
        WikipediaResponseParser.Shorten("Kısa açıklama.", 600).ShouldBe("Kısa açıklama.");
    }

    [Fact]
    public void Ozetteki_fazla_bosluklar_sadelesir()
    {
        WikipediaResponseParser.Shorten("Bir\n\nİki   Üç", 600).ShouldBe("Bir İki Üç");
    }

    // --- Makalenin öne çıkan görseli ---

    [Fact]
    public void Sayfa_gorseli_cozulur()
    {
        const string json = """
            {"query":{"pages":{"1":{"title":"Uçhisar Kalesi","pageimage":"Uchisar Castle.jpg"}}}}
            """;

        WikipediaResponseParser.ParsePageImages(json)["Uçhisar Kalesi"]
            .ShouldBe("Uchisar Castle.jpg");
    }

    [Fact]
    public void Gorseli_olmayan_makale_atlanir()
    {
        const string json = """{"query":{"pages":{"1":{"title":"Test"}}}}""";

        WikipediaResponseParser.ParsePageImages(json).ShouldBeEmpty();
    }

    [Fact]
    public void Sayfa_gorselinde_de_yonlendirme_izlenir()
    {
        const string json = """
            {"query":{
                "redirects":[{"from":"Uçhisar Kale","to":"Uçhisar Kalesi"}],
                "pages":{"1":{"title":"Uçhisar Kalesi","pageimage":"Uchisar.jpg"}}
            }}
            """;

        WikipediaResponseParser.ParsePageImages(json)["Uçhisar Kale"].ShouldBe("Uchisar.jpg");
    }
}

public class WikidataDescriptionTests
{
    [Fact]
    public void Kisa_tanim_cozulur()
    {
        // Wikipedia makalesi olmayan yerler için son çare açıklama kaynağı
        const string json = """
            {"entities":{"Q1":{"claims":{},"descriptions":{
                "tr":{"language":"tr","value":"Türkiye'de tarihi yapı"},
                "en":{"language":"en","value":"historic building in Türkiye"}
            }}}}
            """;

        var entity = WikidataResponseParser.Parse(json)["Q1"];

        entity.TurkishDescription.ShouldBe("Türkiye'de tarihi yapı");
        entity.EnglishDescription.ShouldBe("historic building in Türkiye");
    }

    [Fact]
    public void Tanimi_olmayan_kayitta_null_kalir()
    {
        const string json = """{"entities":{"Q1":{"claims":{}}}}""";

        var entity = WikidataResponseParser.Parse(json)["Q1"];

        entity.TurkishDescription.ShouldBeNull();
        entity.EnglishDescription.ShouldBeNull();
    }
}
