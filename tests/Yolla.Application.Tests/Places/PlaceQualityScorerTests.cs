using Shouldly;
using Yolla.Application.Places;

namespace Yolla.Application.Tests.Places;

public class PlaceQualityScorerTests
{
    [Fact]
    public void Tam_donanimli_kayit_yuz_puan_alir()
    {
        var score = PlaceQualityScorer.Score(new PlaceQualityInput
        {
            Name = "Ayasofya",
            HasWikidata = true,
            HasPhoto = true,
            HasWikipedia = true,
            HasDescription = true,
            HasNameEn = true,
            HasWebsite = true,
            HasOpeningHours = true,
            CategoryWeight = 10
        });

        score.ShouldBe((short)100);
    }

    [Fact]
    public void Sadece_adi_olan_kayit_dusuk_puan_alir_ve_feed_esiginin_altinda_kalir()
    {
        var score = PlaceQualityScorer.Score(new PlaceQualityInput
        {
            Name = "Zafer Anıtı",
            CategoryWeight = 7
        });

        score.ShouldBe((short)7);
        score.ShouldBeLessThan(PlaceQualityScorer.FeedThreshold);
    }

    [Fact]
    public void Fotografi_ve_wikipedia_makalesi_olan_kayit_feed_esigini_gecer()
    {
        var score = PlaceQualityScorer.Score(new PlaceQualityInput
        {
            Name = "Sümela Manastırı",
            HasPhoto = true,
            HasWikipedia = true,
            CategoryWeight = 9
        });

        score.ShouldBe((short)52);
        score.ShouldBeGreaterThanOrEqualTo(PlaceQualityScorer.FeedThreshold);
    }

    [Fact]
    public void Adi_olmayan_kayit_sifir_alir()
    {
        PlaceQualityScorer.Score(new PlaceQualityInput { Name = null }).ShouldBe((short)0);
        PlaceQualityScorer.Score(new PlaceQualityInput { Name = "  " }).ShouldBe((short)0);
    }

    [Fact]
    public void Cok_kisa_ad_zenginlestirme_dolu_olsa_bile_sifir_alir()
    {
        var score = PlaceQualityScorer.Score(new PlaceQualityInput
        {
            Name = "AB",
            HasWikidata = true,
            HasPhoto = true,
            HasWikipedia = true,
            CategoryWeight = 10
        });

        score.ShouldBe((short)0);
    }

    [Fact]
    public void Jenerik_ad_ceza_alir()
    {
        var jenerik = PlaceQualityScorer.Score(new PlaceQualityInput
        {
            Name = "Merkez Camii",
            HasPhoto = true,
            HasWikidata = true,
            CategoryWeight = 6
        });

        var ayirtEdici = PlaceQualityScorer.Score(new PlaceQualityInput
        {
            Name = "Selimiye Camii",
            HasPhoto = true,
            HasWikidata = true,
            CategoryWeight = 6
        });

        ayirtEdici.ShouldBe((short)56);
        jenerik.ShouldBe((short)36);
        (ayirtEdici - jenerik).ShouldBe(20);
    }

    [Fact]
    public void Kategori_agirligi_ust_sinirla_kirpilir()
    {
        var score = PlaceQualityScorer.Score(new PlaceQualityInput
        {
            Name = "Test Yeri",
            CategoryWeight = 50
        });

        score.ShouldBe((short)10);
    }

    [Fact]
    public void Negatif_kategori_agirligi_puani_dusurmez()
    {
        var score = PlaceQualityScorer.Score(new PlaceQualityInput
        {
            Name = "Test Yeri",
            HasPhoto = true,
            CategoryWeight = -5
        });

        score.ShouldBe((short)25);
    }

    [Fact]
    public void Puan_hicbir_zaman_sifirin_altina_inmez()
    {
        // Jenerik ad cezası toplam puandan büyük olabilir
        var score = PlaceQualityScorer.Score(new PlaceQualityInput
        {
            Name = "Park",
            CategoryWeight = 4
        });

        score.ShouldBe((short)0);
    }

    [Fact]
    public void Null_girdi_hata_firlatir()
    {
        Should.Throw<ArgumentNullException>(() => PlaceQualityScorer.Score(null!));
    }

    // --- Jenerik ad tespiti ---

    [Theory]
    [InlineData("Cami")]
    [InlineData("Camii")]
    [InlineData("Merkez Camii")]
    [InlineData("Yeni Cami")]
    [InlineData("Park")]
    [InlineData("Mesire Alanı")]
    [InlineData("Piknik Alanı")]
    [InlineData("Ören Yeri")]
    [InlineData("Seyir Terası")]
    [InlineData("Büyük Mağara")]
    [InlineData("Eski Köprü")]
    [InlineData("Milli Park")]
    public void Sadece_tur_adindan_olusan_isimler_jenerik_sayilir(string name)
    {
        PlaceQualityScorer.IsGenericName(name).ShouldBeTrue();
    }

    [Theory]
    [InlineData("Ayasofya")]
    [InlineData("Ayasofya Camii")]
    [InlineData("Selimiye Camii")]
    [InlineData("Sümela Manastırı")]
    [InlineData("Düden Şelalesi")]
    [InlineData("Kapadokya")]
    [InlineData("Anıtkabir")]
    [InlineData("Galata Kulesi")]
    [InlineData("Ölüdeniz Plajı")]
    public void Ayirt_edici_kelime_iceren_isimler_jenerik_sayilmaz(string name)
    {
        PlaceQualityScorer.IsGenericName(name).ShouldBeFalse();
    }

    [Fact]
    public void Bos_isim_jenerik_sayilir()
    {
        PlaceQualityScorer.IsGenericName(null).ShouldBeTrue();
        PlaceQualityScorer.IsGenericName("").ShouldBeTrue();
        PlaceQualityScorer.IsGenericName("   ").ShouldBeTrue();
    }
}
