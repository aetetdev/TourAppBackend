using Yolla.Domain.Enums;

namespace Yolla.Application.Content;

/// <summary>Bir yere elle içerik ekleme isteği.</summary>
public sealed record ContributionRequest
{
    /// <summary>Fotoğraf, açıklama ya da gezme süresi.</summary>
    public required ContributionType Type { get; init; }

    /// <summary>
    /// Fotoğraf adresi, açıklama metni ya da dakika cinsinden süre.
    /// </summary>
    public required string Value { get; init; }

    /// <summary>Açıklamalar için içerik dili.</summary>
    public string Language { get; init; } = "tr";

    /// <summary>
    /// Fotoğrafı çeken ya da metni yazan. Kendi içeriğimizde ekibin adı,
    /// dışarıdan alınmışsa kaynağın sahibi.
    /// </summary>
    public string? Author { get; init; }

    /// <summary>
    /// Lisans. Kendi ürettiğimiz içerikte <c>Yolla</c>; başkasının içeriğinde
    /// gerçek lisans yazılmalıdır.
    /// </summary>
    public string? License { get; init; }

    public string? SourceUrl { get; init; }

    public string? Note { get; init; }
}

/// <summary>Kaydedilmiş katkı.</summary>
public sealed record ContributionDto
{
    public required int Id { get; init; }

    public required int PlaceId { get; init; }

    public required string PlaceName { get; init; }

    public required ContributionType Type { get; init; }

    public required string Value { get; init; }

    public required string Language { get; init; }

    public string? Author { get; init; }

    public string? License { get; init; }

    public string? SourceUrl { get; init; }

    public required bool IsPublished { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Elle içerik girişini yönetir.</summary>
public interface IContentService
{
    /// <summary>
    /// Yere içerik ekler veya aynı tür ve dildeki mevcut katkıyı günceller,
    /// ardından yerin gösterilen içeriğini tazeler.
    /// </summary>
    Task<ContributionDto> SubmitAsync(
        int placeId,
        ContributionRequest request,
        string? submittedBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>Bir yerin katkılarını listeler.</summary>
    Task<IReadOnlyList<ContributionDto>> GetForPlaceAsync(
        int placeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Katkıyı yayından kaldırır. Kayıt silinmez; yerin içeriği otomatik kaynaklara döner.
    /// </summary>
    Task UnpublishAsync(int contributionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// İçerik girilmesi en çok işe yarayacak yerleri listeler.
    /// </summary>
    /// <remarks>
    /// Kaliteli ama fotoğrafsız kayıtlar önceliklidir: bir fotoğraf eklendiğinde
    /// doğrudan kart destesine girerler.
    /// </remarks>
    Task<IReadOnlyList<MissingContentDto>> GetMissingContentAsync(
        string? citySlug = null,
        int take = 50,
        CancellationToken cancellationToken = default);
}

/// <summary>İçerik bekleyen yer.</summary>
public sealed record MissingContentDto
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    public required string CityName { get; init; }

    public required string CategoryName { get; init; }

    public required short QualityScore { get; init; }

    public required bool HasPhoto { get; init; }

    public required bool HasDescription { get; init; }

    /// <summary>Varsa Wikidata kimliği; elle araştırma için başlangıç noktası.</summary>
    public string? WikidataId { get; init; }

    public required double Latitude { get; init; }

    public required double Longitude { get; init; }
}
