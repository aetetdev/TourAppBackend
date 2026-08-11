using Yolla.Domain.Enums;

namespace Yolla.Application.Discovery;

/// <summary>Kart kaydırma kaydı.</summary>
public sealed record SwipeRequest
{
    /// <summary>Kaydırılan yerin kimliği.</summary>
    public required int PlaceId { get; init; }

    /// <summary>
    /// Kaydırma yönü: <c>Pass</c> ilgilenmiyorum, <c>Like</c> plana ekle,
    /// <c>Later</c> şimdilik atla.
    /// </summary>
    public required SwipeDirection Direction { get; init; }

    /// <summary>Kaydırmanın yapıldığı mod: <c>City</c> ya da <c>Route</c>.</summary>
    public TripMode Context { get; init; } = TripMode.City;

    /// <summary>Varsa ilgili gezi planı.</summary>
    public int? TripId { get; init; }
}

/// <summary>Toplu kaydırma isteği; çevrimdışı biriken kayıtlar için.</summary>
public sealed record SwipeBatchRequest
{
    public required IReadOnlyList<SwipeRequest> Swipes { get; init; }
}

/// <summary>Kaydırma sonucu.</summary>
public sealed record SwipeResultDto
{
    /// <summary>Kaydedilen kaydırma sayısı.</summary>
    public required int Recorded { get; init; }

    /// <summary>Bu cihazın beğendiği toplam yer sayısı.</summary>
    public required int TotalLiked { get; init; }
}

/// <summary>Kart kaydırma kayıtlarını yönetir.</summary>
public interface ISwipeService
{
    /// <summary>
    /// Kaydırmaları kaydeder. Aynı yer daha önce kaydırılmışsa yön güncellenir,
    /// yeni kayıt oluşmaz.
    /// </summary>
    Task<SwipeResultDto> RecordAsync(
        int deviceId,
        IReadOnlyList<SwipeRequest> swipes,
        CancellationToken cancellationToken = default);

    /// <summary>Cihazın beğendiği yerleri kart biçiminde döndürür.</summary>
    Task<IReadOnlyList<PlaceCardDto>> GetLikedPlacesAsync(
        int deviceId,
        string language = "tr",
        CancellationToken cancellationToken = default);
}
