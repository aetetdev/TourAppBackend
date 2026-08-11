using Yolla.Application.Common;

namespace Yolla.Application.Places;

/// <summary>
/// Bir yerin kart destesinde gösterilmeye ne kadar değer olduğunu 0-100 arası puanlar.
/// </summary>
/// <remarks>
/// OSM verisi ham haliyle kullanılamaz: "Cami" adlı isimsiz bir nokta ile Ayasofya aynı
/// tabloda durur. Bu puan ikisini ayırır. Fotoğrafı, Wikipedia makalesi ve Wikidata kaydı
/// olan yerler öne çıkar; ayırt edici adı olmayanlar cezalandırılır.
/// </remarks>
public static class PlaceQualityScorer
{
    // Feed'e girebilmek için gereken alt sınır. Bunun altındakiler veride kalır
    // ama kullanıcıya kart olarak gösterilmez.
    public const short FeedThreshold = 25;

    private const int WikidataPoints = 25;
    private const int PhotoPoints = 25;
    private const int WikipediaPoints = 18;
    private const int DescriptionPoints = 8;
    private const int NameEnPoints = 6;
    private const int WebsitePoints = 4;
    private const int OpeningHoursPoints = 4;
    private const int MaxCategoryPoints = 10;
    private const int GenericNamePenalty = 20;

    // Tek başına hiçbir şey ifade etmeyen tür adları.
    // "Ayasofya Camii" ayırt edicidir; sadece "Cami" değildir.
    private static readonly HashSet<string> GenericWords =
    [
        "cami", "camii", "camisi", "mescit", "mescidi", "kilise", "kilisesi",
        "sinagog", "manastir", "manastiri", "turbe", "turbesi", "tekke",
        "muze", "muzesi", "kale", "kalesi", "hisar", "sur", "surlari",
        "koprusu", "kopru", "cesme", "cesmesi", "hamam", "hamami", "han", "hani",
        "kervansaray", "kervansarayi", "kule", "kulesi", "anit", "aniti",
        "oren", "yeri", "antik", "kent", "harabe", "harabeleri", "kalinti",
        "park", "parki", "bahce", "bahcesi", "mesire", "alani", "piknik",
        "plaj", "plaji", "sahil", "kumsal", "koy", "magara", "magarasi",
        "selale", "selalesi", "gol", "golu", "golet", "vadi", "vadisi",
        "kanyon", "kanyonu", "ada", "adasi", "tepe", "tepesi", "dagi", "dag",
        "manzara", "seyir", "terasi", "noktasi", "kaplica", "kaplicasi", "ilica",
        "milli", "tabiat", "mezarlik", "mezarligi", "kutuphane", "kutuphanesi",
        "carsi", "carsisi", "pazar", "pazari", "bedesten", "meydan", "meydani",
        "heykel", "heykeli", "galeri", "galerisi", "tiyatro", "tiyatrosu"
    ];

    // Ayırt edicilik katmayan niteleyiciler
    private static readonly HashSet<string> FillerWords =
    [
        "merkez", "merkezi", "yeni", "eski", "buyuk", "kucuk", "ulu", "orta",
        "asagi", "yukari", "kuzey", "guney", "dogu", "bati", "the", "of", "and", "ve"
    ];

    public static short Score(PlaceQualityInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        // Adı olmayan kayıt kullanıcıya gösterilemez
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length < 3)
        {
            return 0;
        }

        var score = 0;

        if (input.HasWikidata) score += WikidataPoints;
        if (input.HasPhoto) score += PhotoPoints;
        if (input.HasWikipedia) score += WikipediaPoints;
        if (input.HasDescription) score += DescriptionPoints;
        if (input.HasNameEn) score += NameEnPoints;
        if (input.HasWebsite) score += WebsitePoints;
        if (input.HasOpeningHours) score += OpeningHoursPoints;

        score += Math.Clamp((int)input.CategoryWeight, 0, MaxCategoryPoints);

        if (IsGenericName(input.Name))
        {
            score -= GenericNamePenalty;
        }

        return (short)Math.Clamp(score, 0, 100);
    }

    /// <summary>
    /// Ad yalnızca tür adı ve niteleyicilerden oluşuyorsa true döner:
    /// "Merkez Camii" jeneriktir, "Ayasofya Camii" değildir.
    /// </summary>
    public static bool IsGenericName(string? name)
    {
        var normalized = TextNormalizer.Normalize(name);

        if (normalized.Length == 0)
        {
            return true;
        }

        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return words.All(word => GenericWords.Contains(word) || FillerWords.Contains(word));
    }
}

/// <summary>Puanlama girdisi. Tüm alanlar OSM etiketlerinden ve zenginleştirme adımından gelir.</summary>
public sealed record PlaceQualityInput
{
    public required string? Name { get; init; }

    public bool HasWikidata { get; init; }

    public bool HasPhoto { get; init; }

    public bool HasWikipedia { get; init; }

    public bool HasDescription { get; init; }

    public bool HasNameEn { get; init; }

    public bool HasWebsite { get; init; }

    public bool HasOpeningHours { get; init; }

    /// <summary>Kategori ağırlığı (0-10). Müze bir piknik alanından daha değerlidir.</summary>
    public short CategoryWeight { get; init; }
}
