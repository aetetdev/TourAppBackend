using Yolla.Application.Common;

namespace Yolla.Harvester.Enrichment;

/// <summary>
/// Koordinat aramasıyla bulunan fotoğrafın gerçekten o yere ait olup olmadığını değerlendirir.
/// </summary>
/// <remarks>
/// Coğrafi arama yalnızca "yakında çekilmiş" fotoğrafları döndürür, "bu yerin fotoğrafı"
/// olduğunu garanti etmez. Gerçek örnekler: bir camiye 120 metre uzaktaki kedi fotoğrafı,
/// bir büyükelçiliğe yakında çekilmiş insan portresi eşleşti. Yanlış fotoğraf, fotoğrafsızlıktan
/// kötüdür - kullanıcı kartta gördüğü yere gidip başka bir şey bulursa güven biter.
///
/// Bu yüzden dosya adının yer adıyla örtüşmesi aranır: "Harran Kalesi" -> "Harran_Kalesi.jpg"
/// kabul edilir, "Kedi - gato - cat.jpg" edilmez.
/// </remarks>
public static class PhotoRelevanceFilter
{
    // Yer adlarında sık geçen ve tek başına eşleşme sayılmaması gereken tür sözcükleri
    private static readonly HashSet<string> WeakWords =
    [
        "cami", "camii", "camisi", "kilise", "kilisesi", "manastir", "manastiri",
        "muze", "muzesi", "kale", "kalesi", "hisar", "kule", "kulesi", "koprusu", "kopru",
        "hoyuk", "hoyugu", "oren", "yeri", "antik", "kent", "harabe", "harabeleri",
        "park", "parki", "bahce", "bahcesi", "plaj", "plaji", "magara", "magarasi",
        "selale", "selalesi", "gol", "golu", "vadi", "vadisi", "ada", "adasi",
        "turbe", "turbesi", "han", "hani", "hamam", "hamami", "kervansaray", "kervansarayi",
        "merkez", "merkezi", "buyuk", "kucuk", "yeni", "eski", "ulu", "tarihi",
        "the", "of", "and", "ve", "castle", "mosque", "church", "museum", "tower"
    ];

    private const int MinimumWordLength = 4;

    /// <summary>
    /// Dosya adının yer adıyla yeterince örtüşüp örtüşmediğini söyler.
    /// </summary>
    /// <param name="placeName">Yerin adı, örn. "Harran Kalesi".</param>
    /// <param name="fileTitle">Commons dosya başlığı, örn. "File:Harran Kalesi 01.jpg".</param>
    public static bool IsLikelyRelevant(string? placeName, string? fileTitle)
    {
        var placeWords = ExtractWords(placeName);

        if (placeWords.Count == 0)
        {
            return false;
        }

        var fileWords = ExtractWords(StripFileMetadata(fileTitle));

        if (fileWords.Count == 0)
        {
            return false;
        }

        // Ayırt edici en az bir sözcük dosya adında geçmeli
        return placeWords.Any(fileWords.Contains);
    }

    private static string? StripFileMetadata(string? fileTitle)
    {
        if (string.IsNullOrWhiteSpace(fileTitle))
        {
            return null;
        }

        var value = fileTitle;

        if (value.StartsWith("File:", StringComparison.OrdinalIgnoreCase))
        {
            value = value[5..];
        }

        // Uzantıyı at
        var lastDot = value.LastIndexOf('.');

        if (lastDot > 0)
        {
            value = value[..lastDot];
        }

        return value;
    }

    /// <summary>
    /// Ayırt edici sözcükleri çıkarır: tür adları, kısa sözcükler ve sayılar elenir.
    /// </summary>
    private static HashSet<string> ExtractWords(string? value)
    {
        var normalized = TextNormalizer.Normalize(value);
        var words = new HashSet<string>(StringComparer.Ordinal);

        foreach (var word in normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (word.Length < MinimumWordLength || WeakWords.Contains(word))
            {
                continue;
            }

            // "panoramio", "img", tarih ve numara gibi dosya adı gürültüsü
            if (word.All(char.IsDigit) || word is "panoramio" or "image" or "photo" or "wiki")
            {
                continue;
            }

            words.Add(word);
        }

        return words;
    }
}
