using Yolla.Domain.Enums;

namespace Yolla.Application.Routing;

/// <summary>Bir planın yürünerek mi araçla mı gezileceğine karar verir.</summary>
/// <remarks>
/// İstemcinin gönderdiği profile güvenilmiyor. Mobil uygulama şehir içi
/// planlarda varsayılan olarak yürümeyi gönderiyordu; kullanıcı haritadan iki
/// ayrı şehirden yer seçince 505 km'lik bir *yürüme* rotası çıkıyor ve plan
/// "101 saat" diyordu. Karar sunucuda veriliyor ki web istemcisi de aynı
/// kuralı alsın.
/// </remarks>
public static class TravelModePolicy
{
    /// <summary>Yürünebilir sayılan en uzak durak arası (metre).</summary>
    /// <remarks>
    /// Bir şehrin içinde gezerken duraklar arası kuş uçuşu 15 km'yi geçmiyor;
    /// geçiyorsa artık şehir gezisi değil yolculuktur.
    /// </remarks>
    public const double WalkableSpanMeters = 15_000;

    private const double EarthRadiusMeters = 6_371_000;

    /// <summary>Duraklar arası en uzak mesafeye göre profil seçer.</summary>
    /// <remarks>
    /// Ölçü kuş uçuşu. Gerçek yol mesafesi ancak rota motoruna sorulunca
    /// bilinir ama profili seçmek için ona *gitmeden* önce karar vermek
    /// gerekiyor. Kuş uçuşu yol mesafesinden her zaman kısa olduğundan eşiği
    /// aşan bir plan kesinlikle yürünemez; ters yönde yanılma payı varsa da o
    /// taraf güvenli — bir tık fazla araç önerilir.
    ///
    /// Ardışık duraklar değil, en uzak çift aranıyor: duraklar bu karardan
    /// sonra sıralanacak, ve şehir içinde yakın yakın dizilmiş yerlerin
    /// arasına tek bir uzak durak girdiğinde bütün planı araca çevirmek
    /// doğrusu.
    /// </remarks>
    public static TravelMode Choose(IReadOnlyList<GeoPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        for (var i = 0; i < points.Count; i++)
        {
            for (var j = i + 1; j < points.Count; j++)
            {
                if (StraightLineMeters(points[i], points[j]) > WalkableSpanMeters)
                {
                    return TravelMode.Car;
                }
            }
        }

        return TravelMode.Foot;
    }

    /// <summary>İki nokta arası kuş uçuşu mesafe (haversine, metre).</summary>
    public static double StraightLineMeters(GeoPoint a, GeoPoint b)
    {
        var lat1 = double.DegreesToRadians(a.Latitude);
        var lat2 = double.DegreesToRadians(b.Latitude);
        var deltaLat = lat2 - lat1;
        var deltaLon = double.DegreesToRadians(b.Longitude - a.Longitude);

        var h = (Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2))
                + (Math.Cos(lat1) * Math.Cos(lat2)
                   * Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2));

        return 2 * EarthRadiusMeters * Math.Asin(Math.Sqrt(h));
    }
}
