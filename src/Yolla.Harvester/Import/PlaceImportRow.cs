using NetTopologySuite.Geometries;
using Yolla.Domain.Enums;

namespace Yolla.Harvester.Import;

/// <summary>
/// İçe aktarma için hazırlanmış tek bir yer kaydı. Geçici (staging) tabloya bu şekilde yazılır,
/// il/ilçe ataması ve kategori çözümü veritabanı tarafında SQL ile yapılır.
/// </summary>
public sealed record PlaceImportRow
{
    public required OsmElementType OsmType { get; init; }

    public required long OsmId { get; init; }

    public required string Name { get; init; }

    public required string Slug { get; init; }

    /// <summary>Yolla kategori anahtarı; veritabanında categories.key ile eşleşir.</summary>
    public required string CategoryKey { get; init; }

    public required Point Location { get; init; }

    public string? NameEn { get; init; }

    public string? Address { get; init; }

    public string? Website { get; init; }

    public string? OpeningHours { get; init; }

    public string? WikidataId { get; init; }

    /// <summary>Wikipedia makale başlığı, dil ön eki ayrılmış hali.</summary>
    public string? WikipediaTitle { get; init; }

    public string? DescriptionTr { get; init; }

    public string? DescriptionEn { get; init; }

    /// <summary>
    /// Fotoğraf öncesi kalite puanı. Zenginleştirme adımından sonra yeniden hesaplanır.
    /// </summary>
    public required short QualityScore { get; init; }
}
