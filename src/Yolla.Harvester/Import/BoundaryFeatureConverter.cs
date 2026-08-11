using Yolla.Application.Common;
using Yolla.Harvester.Osm;

namespace Yolla.Harvester.Import;

/// <summary>
/// OSM idari sınır kaydını içe aktarılabilir satıra çevirir.
/// </summary>
/// <remarks>
/// Türkiye extract'i sınır bölgelerinde komşu ülkelerin idari birimlerini de içerir.
/// ISO 3166-2 kodu varsa ülke ön ekiyle süzülür ("TR-34" kabul, "GR-A" elenir).
/// </remarks>
public static class BoundaryFeatureConverter
{
    private const int NameMaxLength = 120;
    private const int SlugMaxLength = 140;

    public static BoundaryImportRow? Convert(
        OsmFeature feature,
        int expectedAdminLevel,
        string countryCodePrefix = "TR-")
    {
        ArgumentNullException.ThrowIfNull(feature);

        // Sınır alan geometrisi olmadan işe yaramaz
        if (feature.Area is null || feature.Area.IsEmpty)
        {
            return null;
        }

        if (!MatchesAdminLevel(feature, expectedAdminLevel))
        {
            return null;
        }

        if (!BelongsToCountry(feature, countryCodePrefix))
        {
            return null;
        }

        var name = Truncate(feature.GetTag("name")?.Trim(), NameMaxLength);

        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var slug = TextNormalizer.Slugify(name);

        if (slug.Length == 0)
        {
            return null;
        }

        if (slug.Length > SlugMaxLength)
        {
            slug = slug[..SlugMaxLength];
        }

        return new BoundaryImportRow
        {
            OsmRelationId = feature.OsmId,
            Name = name,
            NameEn = Truncate(feature.GetTag("name:en")?.Trim(), NameMaxLength),
            NameNormalized = TextNormalizer.Normalize(name),
            Slug = slug,
            Boundary = feature.Area
        };
    }

    private static bool MatchesAdminLevel(OsmFeature feature, int expectedAdminLevel)
    {
        var value = feature.GetTag("admin_level");

        // Etiket yoksa dosya zaten seviyeye göre süzülmüş kabul edilir
        return string.IsNullOrWhiteSpace(value)
               || (int.TryParse(value, out var level) && level == expectedAdminLevel);
    }

    private static bool BelongsToCountry(OsmFeature feature, string countryCodePrefix)
    {
        var isoCode = feature.GetTag("ISO3166-2") ?? feature.GetTag("iso3166-2");

        // Kod yoksa eleme yapılmaz: Türkiye'de bazı ilçelerde bu etiket bulunmuyor
        return string.IsNullOrWhiteSpace(isoCode)
               || isoCode.StartsWith(countryCodePrefix, StringComparison.OrdinalIgnoreCase);
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
