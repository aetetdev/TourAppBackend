using Yolla.Domain.Common;
using Yolla.Domain.Enums;

namespace Yolla.Domain.Entities;

/// <summary>
/// Bir kullanıcıya verilmiş premium hakkı.
/// </summary>
/// <remarks>
/// Kullanıcının üstünde tek bir "premium mi?" bayrağı tutulmuyor; haklar
/// kayıt olarak birikiyor. Böylece süre uzatmak yeni bir satır eklemek
/// oluyor ve hakkın nereden geldiği (coin, elle verilmiş, satın alınmış)
/// her zaman izlenebiliyor.
///
/// <see cref="ExpiresAt"/> null ise **süresiz**.
/// </remarks>
public class PremiumGrant : BaseEntity
{
    public int UserId { get; set; }

    public DateTimeOffset StartsAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Bitiş; null ise süresiz.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    public PremiumSource Source { get; set; }

    /// <summary>Coinle alındıysa kaç coin harcandı.</summary>
    public int? CoinsSpent { get; set; }

    public string? Note { get; set; }

    /// <summary>Verilen anda geçerli mi?</summary>
    public bool IsActiveAt(DateTimeOffset moment) =>
        StartsAt <= moment && (ExpiresAt is null || ExpiresAt > moment);
}
