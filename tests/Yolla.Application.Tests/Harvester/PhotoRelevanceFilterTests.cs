using Shouldly;
using Yolla.Harvester.Enrichment;

namespace Yolla.Application.Tests.Harvester;

/// <summary>
/// Vakaların tamamı koordinat aramasının gerçek çıktısından alındı.
/// </summary>
public class PhotoRelevanceFilterTests
{
    [Theory]
    [InlineData("Harran Kalesi", "File:Harran Kalesi.jpg")]
    [InlineData("Akhan Kervansarayı", "File:Akhan açık alana bakan iki katlı odalar longuner - panoramio.jpg")]
    [InlineData("Anıtkabir", "File:Anitkabir 2019.jpg")]
    [InlineData("Sümela Manastırı", "File:Sumela Monastery view.jpg")]
    [InlineData("Uçhisar Kalesi", "File:Uchisar_Kalesi_01.jpg")]
    public void Adi_ortusen_fotograf_kabul_edilir(string placeName, string fileTitle)
    {
        PhotoRelevanceFilter.IsLikelyRelevant(placeName, fileTitle).ShouldBeTrue();
    }

    [Theory]
    // Bir camiye 120 metre uzaktaki kedi fotoğrafı eşleşmişti
    [InlineData("Sofular Molla Hüsrev Camii", "File:Kedi - gato - cat.jpg")]
    // Büyükelçiliğe yakında çekilmiş insan portresi
    [InlineData("Polonya Büyükelçiliği", "File:Sevim Tekeli (2012).jpg")]
    // Höyüğe alakasız manzara
    [InlineData("Dilkaya Höyüğü", "File:Sulak alan.jpg")]
    [InlineData("Mercimektepe Höyüğü", "File:Yozgat 2014.jpg")]
    // Müzeye otel terası manzarası
    [InlineData("Etnoğrafya Müzesi", "File:View from Castle Inn terrace - panoramio.jpg")]
    [InlineData("Bostancı Gösteri Merkezi", "File:Pi Times - panoramio.jpg")]
    public void Alakasiz_fotograf_reddedilir(string placeName, string fileTitle)
    {
        PhotoRelevanceFilter.IsLikelyRelevant(placeName, fileTitle).ShouldBeFalse();
    }

    [Fact]
    public void Baska_bir_caminin_fotografi_reddedilir()
    {
        // İki cami de "camii" içeriyor; tür sözcüğü eşleşme sayılmamalı
        PhotoRelevanceFilter.IsLikelyRelevant("Murat Reis Camii", "File:Fenai Ali Ef. Camii - panoramio.jpg")
            .ShouldBeFalse();
    }

    [Fact]
    public void Sadece_tur_sozcugu_eslesirse_reddedilir()
    {
        PhotoRelevanceFilter.IsLikelyRelevant("Ayasofya Camii", "File:Merkez Camii.jpg").ShouldBeFalse();
        PhotoRelevanceFilter.IsLikelyRelevant("Topkapı Sarayı", "File:Eski Kale.jpg").ShouldBeFalse();
    }

    [Fact]
    public void Turkce_karakter_farki_eslesmeyi_engellemez()
    {
        // Commons dosya adları çoğunlukla Türkçe karakter içermez
        PhotoRelevanceFilter.IsLikelyRelevant("Çanakkale Şehitleri Anıtı", "File:Canakkale Sehitleri.jpg")
            .ShouldBeTrue();
    }

    [Fact]
    public void Dosya_adindaki_gurultu_eslesme_sayilmaz()
    {
        // "panoramio", numara ve uzantı ayırt edici değil
        PhotoRelevanceFilter.IsLikelyRelevant("Panorama Müzesi", "File:1453 - panoramio.jpg")
            .ShouldBeFalse();
    }

    [Theory]
    [InlineData(null, "File:Test.jpg")]
    [InlineData("Test Yeri", null)]
    [InlineData("", "")]
    [InlineData("Cami", "File:Cami.jpg")]
    public void Ayirt_edici_bilgi_yoksa_reddedilir(string? placeName, string? fileTitle)
    {
        PhotoRelevanceFilter.IsLikelyRelevant(placeName, fileTitle).ShouldBeFalse();
    }
}
