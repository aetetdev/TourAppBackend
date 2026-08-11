using Yolla.Domain.Common;

namespace Yolla.Domain.Entities;

// Yer içeriğinin dil çevirileri - yeni ülke/dil eklendiğinde şema değişmesin diye ayrı tablo
public class PlaceTranslation : BaseEntity
{
    public int PlaceId { get; set; }

    public Place Place { get; set; } = null!;

    // ISO 639-1: "tr", "en", "de", "ar", "ru"
    public string Language { get; set; } = string.Empty;

    public string? Name { get; set; }

    public string? Description { get; set; }
}
