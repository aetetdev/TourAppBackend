using Yolla.Domain.Enums;

namespace Yolla.Application.Routing;

/// <summary>Rota üzerindeki tek bir nokta.</summary>
public readonly record struct GeoPoint(double Latitude, double Longitude);

/// <summary>Rota motorundan dönen sonuç.</summary>
public sealed record RouteResult
{
    /// <summary>Toplam mesafe (metre).</summary>
    public required double DistanceMeters { get; init; }

    /// <summary>Toplam süre (saniye).</summary>
    public required double DurationSeconds { get; init; }

    /// <summary>Haritada çizilecek rota çizgisi, kodlanmış polyline biçiminde.</summary>
    public required string Geometry { get; init; }

    /// <summary>
    /// Duraklara uğrama sırası. Gönderilen noktaların dizindeki karşılıkları:
    /// <c>[0, 2, 1]</c> ikinci noktaya üçüncü sırada uğranacağını söyler.
    /// </summary>
    public required IReadOnlyList<int> WaypointOrder { get; init; }
}

/// <summary>Rota motoruyla konuşan istemci.</summary>
public interface IRoutingClient
{
    /// <summary>
    /// Durakları en kısa toplam süreyi verecek şekilde sıralar ve rotayı hesaplar.
    /// </summary>
    /// <remarks>
    /// Gezgin satıcı problemini çözer. Noktaları verilen sırayla bağlayan basit rota
    /// hesabından farkı budur: 10 durağı yanlış sırayla gezmek doğru sıraya göre
    /// saatler fazla sürebilir.
    /// </remarks>
    /// <param name="points">İlk nokta başlangıç kabul edilir.</param>
    /// <param name="travelMode">Yürüme ya da araç.</param>
    /// <param name="roundTrip">Başlangıca dönülecekse true.</param>
    Task<RouteResult> OptimizeTripAsync(
        IReadOnlyList<GeoPoint> points,
        TravelMode travelMode,
        bool roundTrip = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// İki nokta arasındaki rotayı verilen sırayla hesaplar (sıra optimizasyonu yapmaz).
    /// </summary>
    /// <remarks>Şehirlerarası koridoru çıkarmak için kullanılır.</remarks>
    Task<RouteResult> GetRouteAsync(
        IReadOnlyList<GeoPoint> points,
        TravelMode travelMode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rota çizgisini GeoJSON LineString olarak döndürür.
    /// </summary>
    /// <remarks>
    /// Koridor sorgusu için gerekir: PostGIS bu çizginin çevresindeki yerleri arar.
    /// </remarks>
    Task<string> GetRouteGeoJsonAsync(
        IReadOnlyList<GeoPoint> points,
        TravelMode travelMode,
        CancellationToken cancellationToken = default);
}
