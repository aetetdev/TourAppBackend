namespace Yolla.Application.Rewards;

/// <summary>
/// Coin ve premium ekonomisinin sayıları.
/// </summary>
/// <remarks>
/// Hepsi tek yerde: bu değerler ürün kararı, iş mantığının içine dağılırsa
/// değiştirmek imkânsızlaşır ve istemciyle sunucu farklı sayılar gösterir.
/// İstemci bu değerleri <c>GET /rewards/kurallar</c> ucundan okuyor.
///
/// 2026-08-13'te belirlendi: onaylanan fotoğraf başına 10 coin, bir aylık
/// premium 100 coin (yani 10 fotoğraf). Ulaşılabilir ama premium'u
/// değersizleştirmeyecek bir eşik olarak seçildi.
/// </remarks>
public static class RewardRules
{
    /// <summary>Onaylanan bir fotoğrafın kazandırdığı coin.</summary>
    public const int CoinsPerApprovedPhoto = 10;

    /// <summary>Bir aylık premium kaç coin.</summary>
    public const int CoinsForOneMonth = 100;

    /// <summary>İki aylık premium kaç coin. Tek aylık iki kez almaktan ucuz.</summary>
    public const int CoinsForTwoMonths = 180;

    /// <summary>Süresiz premium kaç coin.</summary>
    public const int CoinsForUnlimited = 1000;

    /// <summary>
    /// Ücretsiz hesabın bir takvim ayında kaydedebileceği plan sayısı.
    /// </summary>
    /// <remarks>
    /// Her ayın başında sıfırlanıyor — toplam bir tavan değil, aylık bir
    /// kota. Premium'da sınır yok.
    /// </remarks>
    public const int FreeMonthlyTripLimit = 5;

    /// <summary>Bir gönderinin en büyük dosya boyutu.</summary>
    public const long MaxPhotoBytes = 12 * 1024 * 1024;

    /// <summary>Kabul edilen en küçük kenar. Daha küçüğü kart olarak kullanılamıyor.</summary>
    public const int MinPhotoEdge = 640;

    /// <summary>
    /// Bir kullanıcının aynı anda bekleyebilecek en fazla gönderi sayısı.
    /// </summary>
    /// <remarks>
    /// Moderasyon kuyruğunu tek kullanıcının doldurmasını engelliyor.
    /// </remarks>
    public const int MaxPendingPerUser = 20;

    /// <summary>İstenen premium süresinin coin karşılığı; tanımsızsa null.</summary>
    public static int? CostFor(PremiumPackage package) => package switch
    {
        PremiumPackage.OneMonth => CoinsForOneMonth,
        PremiumPackage.TwoMonths => CoinsForTwoMonths,
        PremiumPackage.Unlimited => CoinsForUnlimited,
        _ => null
    };

    /// <summary>Paketin süresi; süresiz pakette null.</summary>
    public static TimeSpan? DurationFor(PremiumPackage package) => package switch
    {
        PremiumPackage.OneMonth => TimeSpan.FromDays(30),
        PremiumPackage.TwoMonths => TimeSpan.FromDays(60),
        _ => null
    };
}

/// <summary>Coinle alınabilen premium paketleri.</summary>
public enum PremiumPackage
{
    OneMonth = 0,
    TwoMonths = 1,
    Unlimited = 2
}
