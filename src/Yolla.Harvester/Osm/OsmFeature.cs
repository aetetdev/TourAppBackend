using NetTopologySuite.Geometries;
using Yolla.Domain.Enums;

namespace Yolla.Harvester.Osm;

/// <summary>
/// osmium'un ürettiği GeoJSONSeq dosyasındaki tek bir kaydın okunmuş hali.
/// </summary>
public sealed record OsmFeature
{
    public required OsmElementType ElementType { get; init; }

    public required long OsmId { get; init; }

    /// <summary>Ham OSM etiketleri. Kategori eşlemesi ve zenginleştirme buradan beslenir.</summary>
    public required IReadOnlyDictionary<string, string> Tags { get; init; }

    /// <summary>
    /// Kaydın temsil noktası. Alan geometrili kayıtlarda (kale, milli park) merkez noktası alınır.
    /// </summary>
    public required Point Location { get; init; }

    /// <summary>Alan geometrisi varsa saklanır; il/ilçe sınırları bu alanı kullanır.</summary>
    public Geometry? Area { get; init; }

    public string? GetTag(string key) => Tags.TryGetValue(key, out var value) ? value : null;
}
