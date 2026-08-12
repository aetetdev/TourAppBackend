using Yolla.Application.Discovery;
using Yolla.Application.Routing;
using Yolla.Domain.Enums;

namespace Yolla.Application.Trips;

/// <summary>Yeni gezi planı isteği.</summary>
public sealed record CreateTripRequest
{
    /// <summary>Plan adı. Verilmezse şehir adından üretilir.</summary>
    /// <example>Kapadokya Hafta Sonu</example>
    public string? Name { get; init; }

    /// <summary>Şehir içi mi, şehirlerarası rota mı.</summary>
    public TripMode Mode { get; init; } = TripMode.City;

    public TravelMode TravelMode { get; init; } = TravelMode.Foot;

    /// <summary>Şehir içi modda gezilecek şehir.</summary>
    public int? CityId { get; init; }

    /// <summary>Rota modunda başlangıç noktası.</summary>
    public GeoPoint? StartPoint { get; init; }

    /// <summary>Rota modunda varış noktası.</summary>
    public GeoPoint? EndPoint { get; init; }

    /// <summary>Plana baştan eklenecek yerler.</summary>
    public IReadOnlyList<int>? PlaceIds { get; init; }
}

/// <summary>Plan güncelleme isteği; yalnızca gönderilen alanlar değişir.</summary>
public sealed record UpdateTripRequest
{
    public string? Name { get; init; }

    public TravelMode? TravelMode { get; init; }
}

/// <summary>Plana yer ekleme/çıkarma isteği.</summary>
public sealed record ModifyTripPlacesRequest
{
    /// <summary>Eklenecek yerler.</summary>
    public IReadOnlyList<int>? Add { get; init; }

    /// <summary>Çıkarılacak yerler.</summary>
    public IReadOnlyList<int>? Remove { get; init; }
}

/// <summary>Plan listesi öğesi.</summary>
public sealed record TripSummaryDto
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    public required TripMode Mode { get; init; }

    public required TravelMode TravelMode { get; init; }

    public string? CityName { get; init; }

    public required int PlaceCount { get; init; }

    /// <summary>Hesaplanmışsa toplam yol mesafesi (metre).</summary>
    public double? DistanceMeters { get; init; }

    /// <summary>Listede gösterilecek kapak görseli; ilk durağın fotoğrafı.</summary>
    public string? CoverPhotoUrl { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }
}

/// <summary>Planın tüm ayrıntıları.</summary>
public sealed record TripDetailDto
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    public required TripMode Mode { get; init; }

    public required TravelMode TravelMode { get; init; }

    public string? CityName { get; init; }

    public int? CityId { get; init; }

    public GeoPoint? StartPoint { get; init; }

    public GeoPoint? EndPoint { get; init; }

    /// <summary>Duraklar, rota hesaplandıysa uğrama sırasında.</summary>
    public required IReadOnlyList<TripPlaceDto> Places { get; init; }

    /// <summary>Hesaplanmışsa toplam yol mesafesi (metre).</summary>
    public double? DistanceMeters { get; init; }

    /// <summary>Hesaplanmışsa toplam yolculuk süresi (saniye).</summary>
    public double? DurationSeconds { get; init; }

    /// <summary>Durakların tahmini gezme süresi toplamı (dakika).</summary>
    public required int VisitDurationMinutes { get; init; }

    /// <summary>Haritada çizilecek rota; hesaplanmadıysa null.</summary>
    public string? RouteGeometry { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Plandaki bir durak.</summary>
public sealed record TripPlaceDto
{
    /// <summary>Uğrama sırası; rota hesaplanmadıysa ekleme sırası.</summary>
    public required int Order { get; init; }

    /// <summary>Çok günlük planlarda hangi gün (0 tabanlı).</summary>
    public required int DayIndex { get; init; }

    public required bool IsVisited { get; init; }

    public string? Note { get; init; }

    public required PlaceCardDto Place { get; init; }
}

/// <summary>Gezi planlarını yönetir.</summary>
public interface ITripService
{
    Task<TripDetailDto> CreateAsync(
        int deviceId, CreateTripRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TripSummaryDto>> GetAllAsync(
        int deviceId, CancellationToken cancellationToken = default);

    Task<TripDetailDto> GetAsync(
        int deviceId, int tripId, string language = "tr", CancellationToken cancellationToken = default);

    Task<TripDetailDto> UpdateAsync(
        int deviceId, int tripId, UpdateTripRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(int deviceId, int tripId, CancellationToken cancellationToken = default);

    Task<TripDetailDto> ModifyPlacesAsync(
        int deviceId, int tripId, ModifyTripPlacesRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Planın duraklarını en kısa sırayla dizer, rotayı hesaplar ve sonucu plana kaydeder.
    /// </summary>
    Task<TripDetailDto> OptimizeAsync(
        int deviceId, int tripId, CancellationToken cancellationToken = default);
}
