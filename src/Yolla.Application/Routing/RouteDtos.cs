using Yolla.Application.Discovery;
using Yolla.Domain.Enums;

namespace Yolla.Application.Routing;

/// <summary>Beğenilen yerleri en kısa sırayla dizme isteği.</summary>
public sealed record RouteOptimizeRequest
{
    /// <summary>Rotaya dahil edilecek yerler.</summary>
    public required IReadOnlyList<int> PlaceIds { get; init; }

    /// <summary>
    /// Başlangıç noktası (kullanıcının konumu ya da oteli). Verilmezse ilk yer
    /// başlangıç kabul edilir.
    /// </summary>
    public GeoPoint? StartPoint { get; init; }

    /// <summary>Yürüme (şehir içi) ya da araç (şehirlerarası).</summary>
    public TravelMode TravelMode { get; init; } = TravelMode.Foot;

    /// <summary>Gezi başlangıç noktasında bitecekse true.</summary>
    public bool RoundTrip { get; init; }

    /// <summary>İçerik dili.</summary>
    public string Language { get; init; } = "tr";
}

/// <summary>Rota üzerindeki bir durak.</summary>
public sealed record RouteStopDto
{
    /// <summary>Uğrama sırası (1'den başlar).</summary>
    public required int Order { get; init; }

    public required PlaceCardDto Place { get; init; }
}

/// <summary>Hesaplanmış rota.</summary>
public sealed record OptimizedRouteDto
{
    /// <summary>Toplam yol mesafesi (metre).</summary>
    public required double DistanceMeters { get; init; }

    /// <summary>Toplam yolculuk süresi (saniye). Gezme süreleri dahil değildir.</summary>
    public required double TravelDurationSeconds { get; init; }

    /// <summary>
    /// Durakların tahmini gezme süresi toplamı (dakika). Yolculuk süresiyle birlikte
    /// gezinin ne kadar süreceğini verir.
    /// </summary>
    public required int VisitDurationMinutes { get; init; }

    /// <summary>Haritada çizilecek rota, kodlanmış polyline biçiminde.</summary>
    public required string Geometry { get; init; }

    /// <summary>Duraklar, uğrama sırasına göre.</summary>
    public required IReadOnlyList<RouteStopDto> Stops { get; init; }
}

/// <summary>İki şehir arası yol koridorundaki yerleri isteme.</summary>
public sealed record CorridorFeedRequest
{
    public required GeoPoint Start { get; init; }

    public required GeoPoint End { get; init; }

    /// <summary>Yolun kaç kilometre çevresine bakılacağı (1-50, varsayılan 15).</summary>
    public int BufferKm { get; init; } = 15;

    /// <summary>
    /// Başlangıç ve varış noktalarının kaç kilometre çevresi hariç tutulacağı
    /// (0-100, varsayılan 20).
    /// </summary>
    /// <remarks>
    /// Kullanıcı yola çıkarken bulunduğu şehrin merkezindeki yerleri görmek istemiyor;
    /// onları zaten biliyor. Bu ayar olmadan İstanbul-Antalya sorgusu ilk sayfada
    /// Topkapı Sarayı çevresindeki 15 yeri döndürüyordu.
    /// </remarks>
    public int ExcludeEndpointsKm { get; init; } = 20;

    public int Take { get; init; } = 20;

    public string? Cursor { get; init; }

    public IReadOnlyList<string>? CategoryKeys { get; init; }

    public int? DeviceId { get; init; }

    public string Language { get; init; } = "tr";
}

/// <summary>Koridor sonucu; kartlarla birlikte ana rotanın kendisi.</summary>
public sealed record CorridorFeedDto
{
    /// <summary>Yol koridorundaki yerler, yol boyunca ilerleme sırasına göre.</summary>
    public required CursorPageOfCards Cards { get; init; }

    /// <summary>Ana rotanın toplam mesafesi (metre).</summary>
    public required double RouteDistanceMeters { get; init; }

    /// <summary>Ana rotanın süresi (saniye), duraklar hariç.</summary>
    public required double RouteDurationSeconds { get; init; }

    /// <summary>Haritada çizilecek ana rota.</summary>
    public required string RouteGeometry { get; init; }
}

/// <summary>Kart sayfası (OpenAPI'de genel tip yerine somut tip görünsün diye).</summary>
public sealed record CursorPageOfCards
{
    public required IReadOnlyList<PlaceCardDto> Items { get; init; }

    public string? NextCursor { get; init; }

    public bool HasMore => NextCursor is not null;
}

/// <summary>Rota hesaplama ve koridor keşfi.</summary>
public interface IRouteService
{
    /// <summary>Verilen yerleri en kısa sırayla dizer ve rotayı hesaplar.</summary>
    Task<OptimizedRouteDto> OptimizeAsync(
        RouteOptimizeRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>İki nokta arasındaki yol koridorunda bulunan yerleri döndürür.</summary>
    Task<CorridorFeedDto> GetCorridorFeedAsync(
        CorridorFeedRequest request,
        CancellationToken cancellationToken = default);
}
