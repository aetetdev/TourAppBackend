using Yolla.Domain.Common;
using Yolla.Domain.Enums;

namespace Yolla.Domain.Entities;

/// <summary>
/// Coin defterindeki tek satır.
/// </summary>
/// <remarks>
/// Bakiye ayrı bir alanda tutulmuyor, defterin toplamı olarak hesaplanıyor.
/// Sebep: bakiye alanı ile hareketler er ya da geç birbirini tutmaz hale
/// gelir (yarıda kalan istek, eşzamanlı iki onay) ve hangisinin doğru olduğu
/// anlaşılamaz. Defter tek kaynak; "neden bu kadar coinim var" sorusunun
/// cevabı her zaman satırlarda duruyor.
///
/// <see cref="Amount"/> kazanımda artı, harcamada eksi.
/// </remarks>
public class CoinEntry : BaseEntity
{
    public int UserId { get; set; }

    /// <summary>Kazanımda pozitif, harcamada negatif.</summary>
    public int Amount { get; set; }

    public CoinReason Reason { get; set; }

    /// <summary>Kazanım bir fotoğraf onayından geldiyse hangi gönderi.</summary>
    public int? PhotoSubmissionId { get; set; }

    /// <summary>Harcama premium hakkına çevrildiyse hangi hak.</summary>
    public int? PremiumGrantId { get; set; }

    /// <summary>Elle düzeltmelerde açıklama.</summary>
    public string? Note { get; set; }
}
