using Yolla.Domain.Enums;

namespace Yolla.Application.Places;

/// <summary>Yeni bir turistik yer önerisi.</summary>
public sealed record CreatePlaceSuggestionRequest
{
    /// <summary>Yerin adı.</summary>
    /// <example>Kuşcenneti Seyir Terası</example>
    public required string Name { get; init; }

    /// <summary>Kategori anahtarı; <c>GET /places/kategoriler</c> ile listeleniyor.</summary>
    /// <example>viewpoint</example>
    public required string CategoryKey { get; init; }

    public required double Latitude { get; init; }

    public required double Longitude { get; init; }

    /// <summary>Kısa tanıtım; isteğe bağlı.</summary>
    public string? Description { get; init; }

    /// <summary>Adres ya da tarif; isteğe bağlı.</summary>
    public string? Address { get; init; }
}

/// <summary>Kullanıcının kendi önerisi.</summary>
public sealed record PlaceSuggestionDto
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    public required string CategoryName { get; init; }

    public required string CityName { get; init; }

    public required double Latitude { get; init; }

    public required double Longitude { get; init; }

    public string? Description { get; init; }

    public required PlaceSuggestionStatus Status { get; init; }

    /// <summary>Reddedildiyse sebebi.</summary>
    public string? RejectionReason { get; init; }

    /// <summary>Onaylandıysa kataloğa giren yerin kimliği.</summary>
    public int? PlaceId { get; init; }

    /// <summary>Onay karşılığı yazılan coin.</summary>
    public int? CoinsAwarded { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? ReviewedAt { get; init; }
}

/// <summary>Moderatörün kuyrukta gördüğü öneri.</summary>
/// <remarks>
/// Kullanıcıya dönenden farklı: moderatörün kararı verebilmesi için yakındaki
/// benzer kayıtları ve önerenin geçmişini de taşıyor.
/// </remarks>
public sealed record PlaceSuggestionModerationDto
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    public required string CategoryName { get; init; }

    public required string CityName { get; init; }

    public string? DistrictName { get; init; }

    public required double Latitude { get; init; }

    public required double Longitude { get; init; }

    public string? Description { get; init; }

    public string? Address { get; init; }

    public required int UserId { get; init; }

    /// <summary>Önerenin daha önce onaylanmış öneri sayısı — güven göstergesi.</summary>
    public required int UserApprovedCount { get; init; }

    /// <summary>
    /// Yakındaki mevcut kayıtlar.
    /// </summary>
    /// <remarks>
    /// Tekrar önerileri gözle ayıklamak için: moderatör "bu zaten var mı"
    /// sorusunu ayrı bir ekrana gitmeden cevaplayabilmeli.
    /// </remarks>
    public required IReadOnlyList<SuggestionNearbyDto> Nearby { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Öneriye yakın, kataloğa kayıtlı yer.</summary>
public sealed record SuggestionNearbyDto
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    public required string CategoryName { get; init; }

    public required int DistanceMeters { get; init; }
}

/// <summary>Öneri kurulurken seçilebilecek kategori.</summary>
public sealed record SuggestionCategoryDto
{
    /// <summary>İstekte gönderilen anahtar.</summary>
    /// <example>viewpoint</example>
    public required string Key { get; init; }

    public required string Name { get; init; }

    /// <summary>Mobil taraftaki ikon adı.</summary>
    public string? Icon { get; init; }
}

/// <summary>Kullanıcıların önerdiği yerler ve moderasyonu.</summary>
public interface IPlaceSuggestionService
{
    /// <summary>
    /// Öneri kurulurken seçilebilecek kategoriler.
    /// </summary>
    /// <remarks>
    /// Kullanıcıya gösterilmeyen kategoriler (otel, pansiyon) listede yok:
    /// katalogda da gizlendikleri için önerilmeleri anlamsız.
    /// </remarks>
    Task<IReadOnlyList<SuggestionCategoryDto>> GetCategoriesAsync(
        string language = "tr",
        CancellationToken cancellationToken = default);

    /// <summary>Yeni yer önerir. Öneri moderasyona düşer.</summary>
    Task<PlaceSuggestionDto> SuggestAsync(
        int userId,
        int? deviceId,
        CreatePlaceSuggestionRequest request,
        string language = "tr",
        CancellationToken cancellationToken = default);

    /// <summary>Kullanıcının kendi önerileri, yeniden eskiye.</summary>
    Task<IReadOnlyList<PlaceSuggestionDto>> GetMineAsync(
        int userId,
        string language = "tr",
        CancellationToken cancellationToken = default);

    /// <summary>İncelenmeyi bekleyen öneriler, eskiden yeniye.</summary>
    Task<IReadOnlyList<PlaceSuggestionModerationDto>> GetPendingAsync(
        int take = 50,
        CancellationToken cancellationToken = default);

    /// <summary>Öneriyi kataloğa alır ve kullanıcıya coin verir.</summary>
    Task ApproveAsync(
        int suggestionId,
        int reviewerUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Öneriyi reddeder. Coin verilmez.</summary>
    Task RejectAsync(
        int suggestionId,
        int reviewerUserId,
        string? reason,
        CancellationToken cancellationToken = default);
}
