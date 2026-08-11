using Yolla.Application.Common;
using Yolla.Application.Osm;
using Yolla.Application.Places;
using Yolla.Harvester.Osm;

namespace Yolla.Harvester.Import;

/// <summary>
/// Ham OSM kaydını veritabanına yazılmaya hazır satıra çevirir.
/// </summary>
/// <remarks>
/// Elemenin büyük kısmı burada olur: adı olmayan, kategorisi çözülemeyen ve gizli
/// kategoriye düşen kayıtlar geri döndürülmez. Veritabanına yazma yolu bu yüzden
/// sade kalır ve tüm kural mantığı test edilebilir tek bir yerde toplanır.
/// </remarks>
public static class PlaceFeatureConverter
{
    // Veritabanı kolon sınırları; OSM'de aşırı uzun değerler görülebiliyor
    private const int NameMaxLength = 250;
    private const int SlugMaxLength = 280;
    private const int AddressMaxLength = 400;
    private const int WebsiteMaxLength = 500;
    private const int OpeningHoursMaxLength = 250;
    private const int WikidataMaxLength = 32;
    private const int WikipediaMaxLength = 250;

    /// <summary>
    /// Kaydı içe aktarılabilir satıra çevirir.
    /// </summary>
    /// <param name="feature">osmium çıktısından okunmuş kayıt.</param>
    /// <param name="slugSuffix">
    /// Slug'a eklenecek ayırt edici son ek (genelde il adı). Aynı adı taşıyan yerleri ayırır.
    /// </param>
    /// <param name="includeHiddenCategories">
    /// Otel, turizm bürosu, kamp alanı gibi gizli kategorilerin de aktarılıp aktarılmayacağı.
    /// </param>
    /// <returns>
    /// Dönüşüm sonucu. Kayıt elendiyse <see cref="PlaceConversionResult.Row"/> null olur ve
    /// <see cref="PlaceConversionResult.SkipReason"/> sebebi söyler.
    /// </returns>
    public static PlaceConversionResult Convert(
        OsmFeature feature,
        string? slugSuffix = null,
        bool includeHiddenCategories = false)
    {
        ArgumentNullException.ThrowIfNull(feature);

        var categoryKey = OsmCategoryMapper.Map(feature.Tags);

        if (categoryKey is null)
        {
            return PlaceConversionResult.Skipped(PlaceSkipReason.NoCategory);
        }

        if (!includeHiddenCategories && IsHidden(categoryKey))
        {
            return PlaceConversionResult.Skipped(PlaceSkipReason.HiddenCategory);
        }

        var name = Clean(feature.GetTag("name"), NameMaxLength);

        // Adsız kayıt kullanıcıya gösterilemez
        if (string.IsNullOrWhiteSpace(name))
        {
            return PlaceConversionResult.Skipped(PlaceSkipReason.NoName);
        }

        var nameEn = Clean(feature.GetTag("name:en"), NameMaxLength);
        var wikidataId = CleanWikidataId(feature.GetTag("wikidata"));
        var wikipediaTitle = CleanWikipediaTitle(feature.GetTag("wikipedia"));
        var description = Clean(feature.GetTag("description"), 2000);
        var website = CleanWebsite(feature.GetTag("website") ?? feature.GetTag("contact:website"));
        var openingHours = Clean(feature.GetTag("opening_hours"), OpeningHoursMaxLength);
        var address = BuildAddress(feature);

        var score = PlaceQualityScorer.Score(new PlaceQualityInput
        {
            Name = name,
            HasWikidata = wikidataId is not null,
            HasWikipedia = wikipediaTitle is not null,
            HasNameEn = nameEn is not null,
            HasDescription = description is not null,
            HasWebsite = website is not null,
            HasOpeningHours = openingHours is not null,
            // Fotoğraf bu aşamada yok; zenginleştirmeden sonra puan yeniden hesaplanır
            HasPhoto = false,
            // Kategori ağırlığı veritabanındaki categories tablosunda; SQL tarafında eklenir
            CategoryWeight = 0
        });

        var slug = Truncate(TextNormalizer.SlugifyWithSuffix(name, slugSuffix), SlugMaxLength);

        // Slug üretilemiyorsa (ad tamamen noktalama işaretlerinden oluşuyorsa) kayıt işe yaramaz
        if (slug.Length == 0)
        {
            return PlaceConversionResult.Skipped(PlaceSkipReason.NoSlug);
        }

        return PlaceConversionResult.Converted(new PlaceImportRow
        {
            OsmType = feature.ElementType,
            OsmId = feature.OsmId,
            Name = name,
            NameEn = nameEn,
            Slug = slug,
            CategoryKey = categoryKey,
            Location = feature.Location,
            Address = address,
            Website = website,
            OpeningHours = openingHours,
            WikidataId = wikidataId,
            WikipediaTitle = wikipediaTitle,
            DescriptionTr = description,
            QualityScore = score
        });
    }

    public static bool IsHidden(string categoryKey) =>
        categoryKey is OsmCategoryMapper.Accommodation
            or OsmCategoryMapper.TouristInformation
            or OsmCategoryMapper.CampSite;

    /// <summary>
    /// OSM adres etiketlerini tek satırda birleştirir: "Sultanahmet Meydanı No:1, Fatih".
    /// </summary>
    internal static string? BuildAddress(OsmFeature feature)
    {
        var street = feature.GetTag("addr:street")?.Trim();
        var houseNumber = feature.GetTag("addr:housenumber")?.Trim();
        var neighbourhood = feature.GetTag("addr:neighbourhood")?.Trim()
                            ?? feature.GetTag("addr:suburb")?.Trim();
        var city = feature.GetTag("addr:city")?.Trim();

        var parts = new List<string>(3);

        if (!string.IsNullOrWhiteSpace(street))
        {
            parts.Add(string.IsNullOrWhiteSpace(houseNumber) ? street : $"{street} No:{houseNumber}");
        }

        if (!string.IsNullOrWhiteSpace(neighbourhood))
        {
            parts.Add(neighbourhood);
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            parts.Add(city);
        }

        return parts.Count == 0 ? null : Truncate(string.Join(", ", parts), AddressMaxLength);
    }

    /// <summary>
    /// Wikidata kimliğini doğrular. Geçerli biçim: Q ile başlayıp rakamla devam eder (Q12345).
    /// </summary>
    internal static string? CleanWikidataId(string? value)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed) || trimmed.Length is < 2 or > WikidataMaxLength)
        {
            return null;
        }

        if (trimmed[0] is not ('Q' or 'q'))
        {
            return null;
        }

        return trimmed.AsSpan(1).ContainsAnyExceptInRange('0', '9')
            ? null
            : string.Concat("Q", trimmed.AsSpan(1));
    }

    /// <summary>
    /// OSM'de wikipedia etiketi "tr:Ayasofya" biçimindedir; dil ön eki ayrılır.
    /// </summary>
    internal static string? CleanWikipediaTitle(string? value)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        var separatorIndex = trimmed.IndexOf(':');

        // Ön ek yoksa değer olduğu gibi başlıktır
        if (separatorIndex <= 0 || separatorIndex >= trimmed.Length - 1)
        {
            return Truncate(trimmed, WikipediaMaxLength);
        }

        var languageCode = trimmed[..separatorIndex];

        // Dil kodu iki-üç harflidir; "https://tr.wikipedia.org/..." gibi tam URL'leri ayıklar
        if (languageCode.Length is < 2 or > 3 || !languageCode.All(char.IsLetter))
        {
            return null;
        }

        return Truncate(trimmed[(separatorIndex + 1)..].Trim(), WikipediaMaxLength);
    }

    /// <summary>Yalnızca http(s) adreslerini kabul eder.</summary>
    internal static string? CleanWebsite(string? value)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > WebsiteMaxLength)
        {
            return null;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return uri.Scheme is "http" or "https" ? trimmed : null;
    }

    private static string? Clean(string? value, int maxLength)
    {
        var trimmed = value?.Trim();

        return string.IsNullOrWhiteSpace(trimmed) ? null : Truncate(trimmed, maxLength);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
