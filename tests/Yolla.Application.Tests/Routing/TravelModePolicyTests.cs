using Yolla.Application.Routing;
using Yolla.Domain.Enums;

namespace Yolla.Application.Tests.Routing;

/// <summary>
/// Profil seçimi yanlış olduğunda kullanıcı sessizce saçma bir plan alıyor:
/// iki ayrı şehirden yer seçen kullanıcıya 505 km'lik yürüme rotası ve
/// "101 saat" yazan bir gezi planı çıkmıştı. Eşik burada sabitleniyor.
/// </summary>
public class TravelModePolicyTests
{
    // Sultanahmet ve Ayasofya: yan yana, klasik şehir içi gezi.
    private static readonly GeoPoint Sultanahmet = new(41.0054, 28.9768);
    private static readonly GeoPoint Ayasofya = new(41.0086, 28.9802);

    // Kadıköy: Sultanahmet'e kuş uçuşu ~5 km, hâlâ aynı şehir.
    private static readonly GeoPoint Kadikoy = new(40.9903, 29.0270);

    // Ekecek Dağı (Aksaray) ve Uçansu Şelalesi (Isparta): ekran görüntüsündeki
    // 505 km'lik planın iki durağı.
    private static readonly GeoPoint EkecekDagi = new(38.6422072, 34.0424898);
    private static readonly GeoPoint UcansuSelalesi = new(37.1989677, 30.9075383);

    [Fact]
    public void Yakin_duraklar_yuruyerek_geziliyor()
    {
        var mode = TravelModePolicy.Choose([Sultanahmet, Ayasofya, Kadikoy]);

        Assert.Equal(TravelMode.Foot, mode);
    }

    [Fact]
    public void Iki_sehre_yayilan_plan_araca_ceviriliyor()
    {
        var mode = TravelModePolicy.Choose([EkecekDagi, UcansuSelalesi]);

        Assert.Equal(TravelMode.Car, mode);
    }

    [Fact]
    public void Tek_uzak_durak_butun_plani_araca_ceviriyor()
    {
        // Duraklar sıralanmadan karar veriliyor; aradaki tek uzak nokta
        // listenin sonunda da olsa yakalanmalı.
        var mode = TravelModePolicy.Choose(
            [Sultanahmet, Ayasofya, Kadikoy, UcansuSelalesi]);

        Assert.Equal(TravelMode.Car, mode);
    }

    [Fact]
    public void Tek_nokta_ve_bos_liste_yuruyerek_sayiliyor()
    {
        // Karşılaştırılacak çift yok; rota motoruna gitmeden önce en
        // zararsız varsayım yürüme.
        Assert.Equal(TravelMode.Foot, TravelModePolicy.Choose([]));
        Assert.Equal(TravelMode.Foot, TravelModePolicy.Choose([Sultanahmet]));
    }

    [Fact]
    public void Esigin_iki_yani_ayriliyor()
    {
        // 1 derece enlem ≈ 111 km, yani 0,1 derece ≈ 11,1 km (eşiğin altı)
        // ve 0,2 derece ≈ 22,2 km (üstü).
        var yakin = new GeoPoint(Sultanahmet.Latitude + 0.1, Sultanahmet.Longitude);
        var uzak = new GeoPoint(Sultanahmet.Latitude + 0.2, Sultanahmet.Longitude);

        Assert.Equal(TravelMode.Foot, TravelModePolicy.Choose([Sultanahmet, yakin]));
        Assert.Equal(TravelMode.Car, TravelModePolicy.Choose([Sultanahmet, uzak]));
    }

    [Fact]
    public void Kus_ucusu_mesafe_bilinen_araligi_veriyor()
    {
        // Sultanahmet–Kadıköy boğaz üstünden ~5 km.
        var meters = TravelModePolicy.StraightLineMeters(Sultanahmet, Kadikoy);

        Assert.InRange(meters, 4_500, 5_500);
    }
}
