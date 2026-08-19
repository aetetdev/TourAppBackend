using Shouldly;
using Yolla.Application.Discovery;

namespace Yolla.Application.Tests.Discovery;

public class FeedDiversifierTests
{
    [Fact]
    public void Ayni_kategoriden_yigilma_dagitilir()
    {
        // Türkiye verisinde camiler sayıca baskın; ham sıralama desteyi tek tipe çeviriyor
        var cards = Cards("mosque", "mosque", "mosque", "mosque", "museum", "castle");

        var result = FeedDiversifier.Diversify(cards);

        MaxConsecutive(result).ShouldBe(2);
    }

    [Fact]
    public void Yigilma_listenin_sonuna_itilir()
    {
        // Açgözlü dağıtım sondaki kalıntıyı çözemez: bir kategori tükenince geriye kalanlar
        // arka arkaya dizilir. Önemli olan kullanıcının ilk gördüğü kartların çeşitli olması.
        var cards = Cards(
            "mosque", "mosque", "mosque", "mosque", "mosque", "mosque",
            "museum", "museum", "museum",
            "castle", "castle", "castle");

        var result = FeedDiversifier.Diversify(cards);

        // İlk yarıda kural geçerli
        MaxConsecutive(result.Take(result.Count / 2).ToList()).ShouldBeLessThanOrEqualTo(2);

        // Ham listede 6 cami art arda geliyordu; dağıtım bunu belirgin biçimde azaltmalı
        MaxConsecutive(result).ShouldBeLessThan(MaxConsecutive(cards));
    }

    [Fact]
    public void Bastaki_kartlar_her_zaman_cesitlidir()
    {
        // Kullanıcı desteyi ilk kartlardan yargılar; oradaki tekdüzelik en pahalıya mal olur
        var cards = Cards(
            "mosque", "mosque", "mosque", "mosque", "mosque",
            "museum", "castle");

        var result = FeedDiversifier.Diversify(cards);

        result.Take(4).Select(x => x.CategoryKey).Distinct().Count().ShouldBeGreaterThan(1);
    }

    [Fact]
    public void Tek_kategori_kaldiginda_kural_uygulanamaz()
    {
        // Sınır durum: serpiştirilecek başka kategori yoksa sıra korunur
        var cards = Cards("mosque", "mosque", "mosque", "mosque");

        var result = FeedDiversifier.Diversify(cards);

        result.Count.ShouldBe(4);
        MaxConsecutive(result).ShouldBe(4);
    }

    [Fact]
    public void Hicbir_kart_kaybolmaz_veya_cogalmaz()
    {
        var cards = Cards("mosque", "museum", "mosque", "castle", "mosque", "mosque", "museum");

        var result = FeedDiversifier.Diversify(cards);

        result.Count.ShouldBe(cards.Count);
        result.Select(x => x.Id).OrderBy(x => x).ShouldBe(cards.Select(x => x.Id).OrderBy(x => x));
    }

    [Fact]
    public void Zaten_cesitli_liste_degismez()
    {
        var cards = Cards("mosque", "museum", "castle", "beach");

        FeedDiversifier.Diversify(cards).Select(x => x.CategoryKey)
            .ShouldBe(["mosque", "museum", "castle", "beach"]);
    }

    [Fact]
    public void Kisa_liste_oldugu_gibi_dondurulur()
    {
        var cards = Cards("mosque", "mosque");

        FeedDiversifier.Diversify(cards).ShouldBe(cards);
    }

    [Fact]
    public void Bos_liste_hata_vermez()
    {
        FeedDiversifier.Diversify([]).ShouldBeEmpty();
    }

    [Fact]
    public void Null_liste_hata_firlatir()
    {
        Should.Throw<ArgumentNullException>(() => FeedDiversifier.Diversify(null!));
    }

    private static int MaxConsecutive(IReadOnlyList<PlaceCardDto> cards)
    {
        var max = 0;
        var current = 0;
        string? last = null;

        foreach (var card in cards)
        {
            current = card.CategoryKey == last ? current + 1 : 1;
            last = card.CategoryKey;
            max = Math.Max(max, current);
        }

        return max;
    }

    private static List<PlaceCardDto> Cards(params string[] categoryKeys) =>
        categoryKeys.Select((key, index) => new PlaceCardDto
        {
            Id = index + 1,
            Name = $"Yer {index + 1}",
            Slug = $"yer-{index + 1}",
            CategoryKey = key,
            CategoryName = key,
            PhotoUrl = "https://example.com/foto.jpg",
            PhotoAttribution = "Fotoğraf: Test (CC BY-SA 4.0)",
            CityId = 1,
            CityName = "Test",
            Latitude = 39,
            Longitude = 35,
            QualityScore = (short)(100 - index)
        }).ToList();
}
