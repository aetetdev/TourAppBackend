using Yolla.Domain.Enums;

namespace Yolla.Application.Rewards;

/// <summary>Kullanıcının gönderdiği bir fotoğrafın durumu.</summary>
public sealed class PhotoSubmissionDto
{
    public int Id { get; init; }
    public int PlaceId { get; init; }
    public string PlaceName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;

    /// <summary>Gönderinin görüntülenebilir adresi.</summary>
    public string Url { get; init; } = string.Empty;

    public string? RejectionReason { get; init; }
    public int? CoinsAwarded { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ReviewedAt { get; init; }
}

/// <summary>Moderasyon kuyruğundaki bir gönderi — inceleyene gösterilen gövde.</summary>
public sealed class ModerationItemDto
{
    public int Id { get; init; }
    public int PlaceId { get; init; }
    public string PlaceName { get; init; } = string.Empty;
    public string? CityName { get; init; }

    /// <summary>Yerin hâlihazırdaki fotoğrafı; varsa karşılaştırma için.</summary>
    public string? ExistingPhotoUrl { get; init; }

    public string Url { get; init; } = string.Empty;
    public int Width { get; init; }
    public int Height { get; init; }
    public long SizeBytes { get; init; }

    public int UserId { get; init; }
    public string? UserEmail { get; init; }

    /// <summary>Aynı kullanıcının daha önce kaç gönderisi onaylandı /
    /// reddedildi. Tekrar eden kötüye kullanımı görmek için.</summary>
    public int UserApprovedCount { get; init; }
    public int UserRejectedCount { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Kullanıcının ödül durumu: coin, premium ve kota.</summary>
public sealed class RewardStatusDto
{
    /// <summary>Coin defterinin toplamı.</summary>
    public int CoinBalance { get; init; }

    public int PendingSubmissions { get; init; }
    public int ApprovedSubmissions { get; init; }
    public int RejectedSubmissions { get; init; }

    public bool IsPremium { get; init; }

    /// <summary>Premium bitişi; süresizse null.</summary>
    public DateTimeOffset? PremiumExpiresAt { get; init; }

    /// <summary>Premium süresiz mi?</summary>
    public bool IsPremiumUnlimited { get; init; }

    /// <summary>Bu ay kaç plan kaydedildi.</summary>
    public int TripsThisMonth { get; init; }

    /// <summary>Ücretsiz hesabın aylık plan kotası. Premium'da null (sınırsız).</summary>
    public int? MonthlyTripLimit { get; init; }
}

/// <summary>Coin defterindeki bir hareket.</summary>
public sealed class CoinEntryDto
{
    public int Amount { get; init; }
    public CoinReason Reason { get; init; }
    public string? Note { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>İstemciye açılan ekonomi kuralları.</summary>
public sealed class RewardRulesDto
{
    public int CoinsPerApprovedPhoto { get; init; }
    public int CoinsForOneMonth { get; init; }
    public int CoinsForTwoMonths { get; init; }
    public int CoinsForUnlimited { get; init; }
    public int FreeMonthlyTripLimit { get; init; }
}
