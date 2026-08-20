namespace Yolla.Domain.Enums;

/// <summary>Fotoğraf gönderisinin moderasyon durumu.</summary>
public enum PhotoSubmissionStatus
{
    /// <summary>İncelenmeyi bekliyor.</summary>
    Pending = 0,

    /// <summary>Yayına alındı; kullanıcıya coin verildi.</summary>
    Approved = 1,

    /// <summary>Reddedildi. Kayıt siliniyor değil — aynı fotoğrafın tekrar
    /// gönderilmesini engellemek ve kullanıcıya sebebi göstermek için duruyor.</summary>
    Rejected = 2
}

/// <summary>Önerilen yerin moderasyon durumu.</summary>
public enum PlaceSuggestionStatus
{
    /// <summary>İncelenmeyi bekliyor.</summary>
    Pending = 0,

    /// <summary>Kataloğa alındı; kullanıcıya coin verildi.</summary>
    Approved = 1,

    /// <summary>Reddedildi. Kayıt siliniyor değil — aynı yerin tekrar tekrar
    /// önerilmesini görmek ve kullanıcıya sebebi göstermek için duruyor.</summary>
    Rejected = 2
}

/// <summary>Coin defterindeki hareketin sebebi.</summary>
public enum CoinReason
{
    /// <summary>Gönderilen fotoğraf onaylandı.</summary>
    PhotoApproved = 0,

    /// <summary>Coin premium hakkına çevrildi.</summary>
    PremiumRedeemed = 1,

    /// <summary>Elle düzeltme (destek, telafi, hata giderme).</summary>
    Adjustment = 2,

    /// <summary>Önerilen yer kataloğa alındı.</summary>
    SuggestionApproved = 3
}

/// <summary>Premium hakkının nereden geldiği.</summary>
public enum PremiumSource
{
    /// <summary>Coin harcanarak alındı.</summary>
    CoinRedemption = 0,

    /// <summary>Elle verildi.</summary>
    Manual = 1,

    /// <summary>Satın alındı. Mağaza entegrasyonu henüz yok.</summary>
    Purchase = 2
}
