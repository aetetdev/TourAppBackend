using NetTopologySuite.Geometries;

namespace Yolla.Harvester.Import;

/// <summary>İçe aktarmaya hazır idari sınır kaydı (il veya ilçe).</summary>
public sealed record BoundaryImportRow
{
    public required long OsmRelationId { get; init; }

    public required string Name { get; init; }

    public required string NameNormalized { get; init; }

    public required string Slug { get; init; }

    public string? NameEn { get; init; }

    /// <summary>Sınır poligonu. İl/ilçe ataması ve şehir içi sorgular buna dayanır.</summary>
    public required Geometry Boundary { get; init; }
}
