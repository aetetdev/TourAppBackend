using Yolla.Domain.Common;
using Yolla.Domain.Enums;

namespace Yolla.Domain.Entities;

/// <summary>
/// Bir yere elle eklenen içerik.
/// </summary>
/// <remarks>
/// Otomatik kaynaklar (Wikidata, Commons, Wikipedia) 54 bin yerin ancak 3 binine fotoğraf
/// bulabildi. Kalanı yalnızca elle doldurulabilir. Bu kayıtlar ayrı tabloda tutulur:
/// veri toplayıcı yeniden çalıştığında kendi içeriğimizin üzerine yazmasın ve içeriğin
/// kaynağı her zaman izlenebilir olsun.
///
/// Kendi çektiğimiz fotoğraf ve yazdığımız metin telif sorunu doğurmaz; kaynak "Yolla"
/// olarak görünür.
/// </remarks>
public class PlaceContribution : BaseEntity
{
    public int PlaceId { get; set; }

    public Place Place { get; set; } = null!;

    public ContributionType Type { get; set; }

    /// <summary>Fotoğraf adresi ya da açıklama metni.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>İçerik dili; açıklamalar için anlamlı.</summary>
    public string Language { get; set; } = "tr";

    /// <summary>Fotoğrafı çeken ya da metni yazan.</summary>
    public string? Author { get; set; }

    /// <summary>Lisans bilgisi. Kendi içeriğimizde "Yolla" olur.</summary>
    public string? License { get; set; }

    /// <summary>Varsa kaynak bağlantısı.</summary>
    public string? SourceUrl { get; set; }

    /// <summary>Katkıyı ekleyen (yönetici kullanıcı ya da ileride son kullanıcı).</summary>
    public string? SubmittedBy { get; set; }

    /// <summary>
    /// Yayında olup olmadığı. Yayından kaldırılan katkı silinmez; yerin içeriği
    /// otomatik kaynaklara geri döner.
    /// </summary>
    public bool IsPublished { get; set; } = true;

    public string? Note { get; set; }
}
