using Yolla.Application.Common;
using Yolla.Application.Discovery;

namespace Yolla.Application.Places;

/// <summary>Yer detay sayfasının tüm içeriği.</summary>
public sealed record PlaceDetailDto
{
    public required int Id { get; init; }

    /// <example>Uçhisar Kalesi</example>
    public required string Name { get; init; }

    /// <summary>İngilizce adı, varsa.</summary>
    public string? NameEn { get; init; }

    public required string Slug { get; init; }

    public required string CategoryKey { get; init; }

    public required string CategoryName { get; init; }

    public string? CategoryIcon { get; init; }

    /// <summary>Tam açıklama. Kart üzerindeki kısaltılmış metnin uzun hali.</summary>
    public string? Description { get; init; }

    /// <summary>Görselin orijinali. Büyüktür; ekranda küçültülmüş sürümler kullanılmalı.</summary>
    public string? PhotoUrl { get; init; }

    /// <summary>Önizleme için küçültülmüş görsel (500 piksel genişlik).</summary>
    public string? PhotoThumbUrl => CommonsThumbnail.Thumb(PhotoUrl);

    /// <summary>Detay sayfasının başlık görseli (960 piksel genişlik).</summary>
    public string? PhotoLargeUrl => CommonsThumbnail.Large(PhotoUrl);

    /// <summary>Görselin altında gösterilmesi zorunlu atıf satırı.</summary>
    public string? PhotoAttribution { get; init; }

    /// <summary>Görselin kaynak sayfası; atıf bağlantısı.</summary>
    public string? PhotoSource { get; init; }

    public required double Latitude { get; init; }

    public required double Longitude { get; init; }

    public required string CityName { get; init; }

    public required string CitySlug { get; init; }

    public string? DistrictName { get; init; }

    public string? Address { get; init; }

    public string? Website { get; init; }

    /// <summary>OSM biçiminde çalışma saatleri, örn. <c>Tu-Su 09:00-19:00</c>.</summary>
    public string? OpeningHours { get; init; }

    /// <summary>Wikipedia makalesinin adresi, varsa.</summary>
    public string? WikipediaUrl { get; init; }

    public short? AverageVisitMinutes { get; init; }

    public required short QualityScore { get; init; }

    /// <summary>Haritada yol tarifi için hazır bağlantı.</summary>
    /// <remarks>
    /// Kullanıcı yol tarifi, güncel yorumlar ve çalışma saatleri için harita uygulamasına
    /// gidebilir. Bu bağlantı ücretsizdir ve dış servis şartlarına uygundur.
    /// </remarks>
    public required string DirectionsUrl { get; init; }

    /// <summary>Yakındaki diğer turistik yerler.</summary>
    public required IReadOnlyList<NearbyPlaceDto> Nearby { get; init; }
}

/// <summary>Detay sayfasında listelenen yakın yer.</summary>
public sealed record NearbyPlaceDto
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    public required string Slug { get; init; }

    public required string CategoryName { get; init; }

    public string? PhotoUrl { get; init; }

    /// <summary>Liste satırı için küçültülmüş görsel (500 piksel genişlik).</summary>
    public string? PhotoThumbUrl => CommonsThumbnail.Thumb(PhotoUrl);

    /// <summary>Kuş uçuşu mesafe (metre).</summary>
    public required int DistanceMeters { get; init; }
}

/// <summary>Yer detaylarını sunar.</summary>
public interface IPlaceService
{
    Task<PlaceDetailDto> GetByIdAsync(
        int placeId,
        string language = "tr",
        CancellationToken cancellationToken = default);

    Task<PlaceDetailDto> GetBySlugAsync(
        string citySlug,
        string placeSlug,
        string language = "tr",
        CancellationToken cancellationToken = default);

    /// <summary>Bir noktanın çevresindeki yerleri kart biçiminde döndürür.</summary>
    Task<IReadOnlyList<PlaceCardDto>> GetNearbyCardsAsync(
        double latitude,
        double longitude,
        int radiusMeters = 3000,
        int take = 20,
        string language = "tr",
        CancellationToken cancellationToken = default);
}
