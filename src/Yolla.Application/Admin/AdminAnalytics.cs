namespace Yolla.Application.Admin;

/// <summary>
/// Yönetim panosunun beslendiği ölçümler.
/// </summary>
/// <remarks>
/// **Hiçbir uç kişisel veri döndürmüyor.** Ne e-posta, ne ad, ne kullanıcı
/// kimliği: yalnızca toplamlar. "Ankara'da 340 beğeni" cevaplanabiliyor,
/// "şu kullanıcı Ankara'yı beğendi" cevaplanamıyor. KVKK açısından tercih
/// değil şart — panelin amacı tercihi anlamak, kişiyi tanımak değil.
///
/// Moderasyon ekranlarında öneren yalnızca sıra numarasıyla görünüyor;
/// tekrar eden kötüye kullanımı ayırt etmeye yetiyor, kimliği açmıyor.
/// </remarks>
public interface IAdminAnalyticsService
{
    Task<AdminOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CityUsageDto>> GetCityUsageAsync(
        int take = 20,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PopularPlaceDto>> GetPopularPlacesAsync(
        int? cityId = null,
        int take = 20,
        CancellationToken cancellationToken = default);

    Task<RouteInsightsDto> GetRouteInsightsAsync(
        int take = 20,
        CancellationToken cancellationToken = default);

    Task<MembershipDto> GetMembershipAsync(CancellationToken cancellationToken = default);
}

/// <summary>Panonun üst şeridi: her şeyin tek bakışta özeti.</summary>
public sealed record AdminOverviewDto
{
    public required int UserCount { get; init; }

    /// <summary>Şu an geçerli premium hakkı olan kullanıcı sayısı.</summary>
    public required int PremiumUserCount { get; init; }

    public required int DeviceCount { get; init; }

    /// <summary>Son 7 ve 30 günde uygulamayı açan cihazlar.</summary>
    public required int ActiveDevices7 { get; init; }

    public required int ActiveDevices30 { get; init; }

    public required int PlaceCount { get; init; }

    /// <summary>Fotoğrafı olmayan yerler — içerik ekibinin iş listesi.</summary>
    public required int PlacesWithoutPhoto { get; init; }

    public required int CityCount { get; init; }

    public required int SwipeCount { get; init; }

    public required int LikeCount { get; init; }

    public required int TripCount { get; init; }

    public required int TripsLast30Days { get; init; }

    /// <summary>İncelenmeyi bekleyen fotoğraf gönderisi.</summary>
    public required int PendingPhotoCount { get; init; }

    /// <summary>İncelenmeyi bekleyen yer önerisi.</summary>
    public required int PendingSuggestionCount { get; init; }

    /// <summary>Cihazların platform dağılımı.</summary>
    public required IReadOnlyList<NameCountDto> Platforms { get; init; }

    /// <summary>Son 30 günün günlük plan sayısı; panodaki eğri.</summary>
    public required IReadOnlyList<DayCountDto> TripsByDay { get; init; }
}

/// <summary>Şehir bazlı kullanım.</summary>
/// <remarks>
/// Kullanıcının nerede *olduğu* değil, neyi gezdiği ölçülüyor. Konum
/// toplamıyoruz; ilgi, kaydırılan ve plana eklenen yerlerin şehrinden
/// çıkarılıyor.
/// </remarks>
public sealed record CityUsageDto
{
    public required int CityId { get; init; }

    public required string CityName { get; init; }

    public required int Swipes { get; init; }

    public required int Likes { get; init; }

    public required int Trips { get; init; }

    /// <summary>Bu şehirde kaydırma yapan ayrı cihaz sayısı.</summary>
    public required int Devices { get; init; }

    public required int Places { get; init; }
}

/// <summary>En çok ilgi gören yer.</summary>
public sealed record PopularPlaceDto
{
    public required int PlaceId { get; init; }

    public required string Name { get; init; }

    public required string CityName { get; init; }

    public required string CategoryName { get; init; }

    public required int Likes { get; init; }

    /// <summary>
    /// Kaç plana durak olarak eklendi.
    /// </summary>
    /// <remarks>
    /// Beğeniden daha güçlü sinyal: beğeni "hoşuma gitti", plana eklemek
    /// "gitmeyi düşünüyorum" demek.
    /// </remarks>
    public required int TripAdds { get; init; }

    public required bool HasPhoto { get; init; }
}

/// <summary>Rota tercihleri.</summary>
public sealed record RouteInsightsDto
{
    /// <summary>Şehir içi planlar, şehre göre.</summary>
    public required IReadOnlyList<CityTripDto> CityTrips { get; init; }

    /// <summary>Şehirlerarası planların uç şehirleri.</summary>
    public required IReadOnlyList<CorridorDto> Corridors { get; init; }

    /// <summary>Yürüme/araç dağılımı.</summary>
    public required IReadOnlyList<NameCountDto> TravelModes { get; init; }
}

public sealed record CityTripDto
{
    public required string CityName { get; init; }

    public required int Trips { get; init; }

    public required double AverageStops { get; init; }

    public required double AverageDistanceKm { get; init; }
}

/// <summary>Bir şehirden diğerine kurulan planlar.</summary>
public sealed record CorridorDto
{
    public required string FromCityName { get; init; }

    public required string ToCityName { get; init; }

    public required int Trips { get; init; }
}

/// <summary>Üyelik ve coin ekonomisi.</summary>
public sealed record MembershipDto
{
    public required int TotalUsers { get; init; }

    public required int PremiumUsers { get; init; }

    public required int FreeUsers { get; init; }

    /// <summary>Premium hakkının nereden geldiği: coin, satın alma, elle.</summary>
    public required IReadOnlyList<NameCountDto> PremiumBySource { get; init; }

    /// <summary>Dağıtılan toplam coin.</summary>
    public required int CoinsEarned { get; init; }

    /// <summary>Premium'a çevrilen toplam coin.</summary>
    public required int CoinsSpent { get; init; }

    /// <summary>Kullanıcıların elindeki toplam bakiye.</summary>
    public required int CoinsOutstanding { get; init; }

    /// <summary>Coin kazanımının kaynağı: fotoğraf, yer önerisi, elle.</summary>
    public required IReadOnlyList<NameCountDto> CoinsByReason { get; init; }
}

/// <summary>Ad-sayı çifti; pastalar ve sütun grafikleri için.</summary>
public sealed record NameCountDto
{
    public required string Name { get; init; }

    public required int Count { get; init; }
}

/// <summary>Gün-sayı çifti; zaman eğrileri için.</summary>
public sealed record DayCountDto
{
    public required DateOnly Day { get; init; }

    public required int Count { get; init; }
}
