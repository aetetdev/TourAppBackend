namespace Yolla.Application.Common;

/// <summary>
/// Veri kaynaklarının zorunlu atıf metinleri.
/// </summary>
/// <remarks>
/// OpenStreetMap verisi ODbL lisanslı ve atıf zorunlu. Wikimedia Commons fotoğraflarının
/// çoğu CC BY-SA; fotoğrafçı adı ve lisans gösterilmeden kullanılamaz. Bu yüzden fotoğraf
/// içeren her yanıtta atıf bilgisi taşınır ve atıf bilgisi eksik olan fotoğraflar hiç
/// gösterilmez.
/// </remarks>
public static class Attribution
{
    public const string OpenStreetMap = "© OpenStreetMap katkıcıları";

    public const string Wikimedia = "Fotoğraflar: Wikimedia Commons";

    /// <summary>Yer listesi ve harita içeren yanıtların ortak atıfları.</summary>
    public static readonly IReadOnlyList<string> Places = [OpenStreetMap, Wikimedia];

    /// <summary>Yalnızca coğrafi veri içeren yanıtlar (şehir listesi gibi).</summary>
    public static readonly IReadOnlyList<string> GeoOnly = [OpenStreetMap];

    /// <summary>
    /// Tek bir fotoğrafın atıf satırını üretir: "Fotoğraf: Ali Veli (CC BY-SA 4.0)".
    /// </summary>
    public static string? ForPhoto(string? author, string? license)
    {
        if (string.IsNullOrWhiteSpace(author) && string.IsNullOrWhiteSpace(license))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(author))
        {
            return $"Fotoğraf: {license}";
        }

        return string.IsNullOrWhiteSpace(license)
            ? $"Fotoğraf: {author}"
            : $"Fotoğraf: {author} ({license})";
    }
}
