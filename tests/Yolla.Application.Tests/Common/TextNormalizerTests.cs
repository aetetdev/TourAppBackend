using Shouldly;
using Yolla.Application.Common;

namespace Yolla.Application.Tests.Common;

public class TextNormalizerTests
{
    [Theory]
    [InlineData("İstanbul", "istanbul")]
    [InlineData("Iğdır", "igdir")]
    [InlineData("Şanlıurfa", "sanliurfa")]
    [InlineData("Çanakkale", "canakkale")]
    [InlineData("Kütahya", "kutahya")]
    [InlineData("Nevşehir", "nevsehir")]
    [InlineData("Gümüşhane", "gumushane")]
    [InlineData("Afyonkarahisar", "afyonkarahisar")]
    public void Turkce_sehir_adlari_dogru_sadelestirilir(string input, string expected)
    {
        TextNormalizer.Normalize(input).ShouldBe(expected);
    }

    [Fact]
    public void Buyuk_I_harfi_birlesik_nokta_uretmez()
    {
        // Kültüre duyarlı ToLower() burada "i̇stanbul" üretir ve arama eşleşmez
        var result = TextNormalizer.Normalize("İSTANBUL");

        result.ShouldBe("istanbul");
        result.Length.ShouldBe(8);
    }

    [Fact]
    public void Noktalama_temizlenir_bosluklar_sadelesir()
    {
        TextNormalizer.Normalize("  Ayasofya-i   Kebir   Camii  ")
            .ShouldBe("ayasofya i kebir camii");
    }

    [Fact]
    public void Bos_ve_null_degerler_bos_string_dondurur()
    {
        TextNormalizer.Normalize(null).ShouldBe(string.Empty);
        TextNormalizer.Normalize("").ShouldBe(string.Empty);
        TextNormalizer.Normalize("   ").ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData("Ayasofya Camii", "ayasofya-camii")]
    [InlineData("Sümela Manastırı", "sumela-manastiri")]
    [InlineData("Düden Şelalesi", "duden-selalesi")]
    [InlineData("İzmir Saat Kulesi", "izmir-saat-kulesi")]
    public void Slug_url_uyumlu_uretilir(string input, string expected)
    {
        TextNormalizer.Slugify(input).ShouldBe(expected);
    }

    [Fact]
    public void Slug_yabanci_aksanlari_temizler()
    {
        TextNormalizer.Slugify("Café Museum").ShouldBe("cafe-museum");
    }

    [Fact]
    public void Slug_bos_deger_icin_bos_string_dondurur()
    {
        TextNormalizer.Slugify(null).ShouldBe(string.Empty);
        TextNormalizer.Slugify("!!!").ShouldBe(string.Empty);
    }

    [Fact]
    public void Ayirt_edici_sonek_eklenebilir()
    {
        // Aynı adı taşıyan yerleri ayırmak için: iki farklı ildeki "Kale"
        TextNormalizer.SlugifyWithSuffix("Kale", "Çanakkale").ShouldBe("kale-canakkale");
        TextNormalizer.SlugifyWithSuffix("Kale", "Malatya").ShouldBe("kale-malatya");
    }

    [Fact]
    public void Sonek_bos_ise_sadece_slug_dondurur()
    {
        TextNormalizer.SlugifyWithSuffix("Anıtkabir", null).ShouldBe("anitkabir");
        TextNormalizer.SlugifyWithSuffix("Anıtkabir", "").ShouldBe("anitkabir");
    }

    [Fact]
    public void Ad_bos_ise_sadece_sonek_dondurur()
    {
        TextNormalizer.SlugifyWithSuffix(null, "Ankara").ShouldBe("ankara");
    }
}
