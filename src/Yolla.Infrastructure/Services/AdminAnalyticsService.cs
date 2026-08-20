using Microsoft.EntityFrameworkCore;
using Yolla.Application.Admin;
using Yolla.Domain.Enums;
using Yolla.Infrastructure.Persistence;

namespace Yolla.Infrastructure.Services;

/// <inheritdoc cref="IAdminAnalyticsService"/>
public sealed class AdminAnalyticsService(YollaDbContext context) : IAdminAnalyticsService
{
    /// <summary>Panodaki eğrinin kapsadığı gün sayısı.</summary>
    private const int TrendDays = 30;

    public async Task<AdminOverviewDto> GetOverviewAsync(
        CancellationToken cancellationToken = default)
    {
        var simdi = DateTimeOffset.UtcNow;
        var yediGun = simdi.AddDays(-7);
        var otuzGun = simdi.AddDays(-TrendDays);

        var platformlar = await context.Devices
            .AsNoTracking()
            .GroupBy(x => x.Platform)
            .Select(g => new NameCountDto { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync(cancellationToken);

        // Günlük plan sayısı: veritabanı yalnızca **dolu** günleri döndürüyor,
        // boş günler aşağıda tamamlanıyor. Eksik gün eğride kopukluk değil,
        // sıfır olarak görünmeli.
        var gunlukHam = await context.Trips
            .AsNoTracking()
            .Where(x => x.CreatedAt >= otuzGun)
            .GroupBy(x => x.CreatedAt.Date)
            .Select(g => new { Gun = g.Key, Adet = g.Count() })
            .ToListAsync(cancellationToken);

        var gunluk = new List<DayCountDto>(TrendDays);
        var bugun = DateOnly.FromDateTime(simdi.UtcDateTime);

        for (var i = TrendDays - 1; i >= 0; i--)
        {
            var gun = bugun.AddDays(-i);
            var satir = gunlukHam.FirstOrDefault(
                x => DateOnly.FromDateTime(x.Gun) == gun);

            gunluk.Add(new DayCountDto { Day = gun, Count = satir?.Adet ?? 0 });
        }

        return new AdminOverviewDto
        {
            UserCount = await context.Users.CountAsync(cancellationToken),
            PremiumUserCount = await AktifPremiumSayisiAsync(simdi, cancellationToken),
            DeviceCount = await context.Devices.CountAsync(cancellationToken),
            ActiveDevices7 = await context.Devices
                .CountAsync(x => x.LastSeenAt >= yediGun, cancellationToken),
            ActiveDevices30 = await context.Devices
                .CountAsync(x => x.LastSeenAt >= otuzGun, cancellationToken),
            PlaceCount = await context.Places
                .CountAsync(x => x.IsActive, cancellationToken),
            PlacesWithoutPhoto = await context.Places
                .CountAsync(x => x.IsActive && x.PhotoUrl == null, cancellationToken),
            CityCount = await context.Cities.CountAsync(cancellationToken),
            SwipeCount = await context.Swipes.CountAsync(cancellationToken),
            LikeCount = await context.Swipes
                .CountAsync(x => x.Direction == SwipeDirection.Like, cancellationToken),
            TripCount = await context.Trips.CountAsync(cancellationToken),
            TripsLast30Days = await context.Trips
                .CountAsync(x => x.CreatedAt >= otuzGun, cancellationToken),
            PendingPhotoCount = await context.PhotoSubmissions
                .CountAsync(x => x.Status == PhotoSubmissionStatus.Pending, cancellationToken),
            PendingSuggestionCount = await context.PlaceSuggestions
                .CountAsync(x => x.Status == PlaceSuggestionStatus.Pending, cancellationToken),
            Platforms = platformlar,
            TripsByDay = gunluk
        };
    }

    public async Task<IReadOnlyList<CityUsageDto>> GetCityUsageAsync(
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 100);

        // Kaydırmalar yerin şehrine göre toplanıyor: kullanıcının nerede
        // olduğunu bilmiyoruz ve bilmek de istemiyoruz, ilgisini ölçüyoruz.
        var kaydirmalar = await context.Swipes
            .AsNoTracking()
            .GroupBy(x => x.Place.CityId)
            .Select(g => new
            {
                CityId = g.Key,
                Swipes = g.Count(),
                Likes = g.Count(x => x.Direction == SwipeDirection.Like),
                Devices = g.Select(x => x.DeviceId).Distinct().Count()
            })
            .ToListAsync(cancellationToken);

        var planlar = await context.Trips
            .AsNoTracking()
            .Where(x => x.CityId != null)
            .GroupBy(x => x.CityId!.Value)
            .Select(g => new { CityId = g.Key, Trips = g.Count() })
            .ToListAsync(cancellationToken);

        var ilgiliSehirler = kaydirmalar.Select(x => x.CityId)
            .Concat(planlar.Select(x => x.CityId))
            .Distinct()
            .ToList();

        var sehirler = await context.Cities
            .AsNoTracking()
            .Where(x => ilgiliSehirler.Contains(x.Id))
            .Select(x => new { x.Id, x.Name, Places = x.Places.Count(p => p.IsActive) })
            .ToListAsync(cancellationToken);

        return sehirler
            .Select(sehir =>
            {
                var k = kaydirmalar.FirstOrDefault(x => x.CityId == sehir.Id);
                var p = planlar.FirstOrDefault(x => x.CityId == sehir.Id);

                return new CityUsageDto
                {
                    CityId = sehir.Id,
                    CityName = sehir.Name,
                    Swipes = k?.Swipes ?? 0,
                    Likes = k?.Likes ?? 0,
                    Devices = k?.Devices ?? 0,
                    Trips = p?.Trips ?? 0,
                    Places = sehir.Places
                };
            })
            // Sıralama beğeniye göre: ham kaydırma sayısı "kaç kart gördü"yü
            // ölçüyor, beğeni "neyi istedi"yi.
            .OrderByDescending(x => x.Likes)
            .ThenByDescending(x => x.Trips)
            .Take(take)
            .ToList();
    }

    public async Task<IReadOnlyList<PopularPlaceDto>> GetPopularPlacesAsync(
        int? cityId = null,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 100);

        var begeniler = await context.Swipes
            .AsNoTracking()
            .Where(x => x.Direction == SwipeDirection.Like)
            .Where(x => cityId == null || x.Place.CityId == cityId)
            .GroupBy(x => x.PlaceId)
            .Select(g => new { PlaceId = g.Key, Likes = g.Count() })
            .OrderByDescending(x => x.Likes)
            // Beğenisi olan her yer değil, en çok beğenilenler; liste
            // aşağıda plana eklenmelerle birleştirilirken küçük kalmalı.
            .Take(take * 3)
            .ToListAsync(cancellationToken);

        var planaEklenenler = await context.TripPlaces
            .AsNoTracking()
            .Where(x => cityId == null || x.Place.CityId == cityId)
            .GroupBy(x => x.PlaceId)
            .Select(g => new { PlaceId = g.Key, Adds = g.Count() })
            .OrderByDescending(x => x.Adds)
            .Take(take * 3)
            .ToListAsync(cancellationToken);

        var kimlikler = begeniler.Select(x => x.PlaceId)
            .Concat(planaEklenenler.Select(x => x.PlaceId))
            .Distinct()
            .ToList();

        var yerler = await context.Places
            .AsNoTracking()
            .Where(x => kimlikler.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.Name,
                CityName = x.City.Name,
                CategoryName = x.Category.NameTr,
                HasPhoto = x.PhotoUrl != null
            })
            .ToListAsync(cancellationToken);

        return yerler
            .Select(yer => new PopularPlaceDto
            {
                PlaceId = yer.Id,
                Name = yer.Name,
                CityName = yer.CityName,
                CategoryName = yer.CategoryName,
                HasPhoto = yer.HasPhoto,
                Likes = begeniler.FirstOrDefault(x => x.PlaceId == yer.Id)?.Likes ?? 0,
                TripAdds = planaEklenenler.FirstOrDefault(x => x.PlaceId == yer.Id)?.Adds ?? 0
            })
            // Plana eklenmek beğeniden ağır basıyor: biri niyet, diğeri ilgi.
            .OrderByDescending(x => x.TripAdds * 3 + x.Likes)
            .Take(take)
            .ToList();
    }

    public async Task<RouteInsightsDto> GetRouteInsightsAsync(
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 100);

        var sehirIci = await context.Trips
            .AsNoTracking()
            .Where(x => x.Mode == TripMode.City && x.CityId != null)
            .GroupBy(x => x.City!.Name)
            .Select(g => new
            {
                CityName = g.Key,
                Trips = g.Count(),
                Stops = g.Average(x => (double)x.Places.Count),
                Distance = g.Average(x => x.TotalDistanceMeters ?? 0)
            })
            .OrderByDescending(x => x.Trips)
            .Take(take)
            .ToListAsync(cancellationToken);

        // Koridor planlarının uçları nokta olarak duruyor; hangi şehre
        // düştükleri sınırlardan bulunuyor.
        //
        // Sorgu elle yazıldı çünkü LINQ'de ifade edilemiyor: uç noktalar
        // `geography`, şehir sınırları `geometry` ve PostGIS ikisini
        // doğrudan karşılaştırmıyor (`st_contains(geometry, geography)`
        // diye bir işlev yok). Dönüşüm burada açıkça duruyor.
        var koridorOzet = await context.Database
            .SqlQuery<CorridorRow>(
                $"""
                 SELECT cf.name AS from_city_name,
                        ct.name AS to_city_name,
                        count(*)::int AS trips
                 FROM trips t
                 JOIN cities cf ON cf.boundary IS NOT NULL
                                AND ST_Contains(cf.boundary, t.start_point::geometry)
                 JOIN cities ct ON ct.boundary IS NOT NULL
                                AND ST_Contains(ct.boundary, t.end_point::geometry)
                 WHERE t.mode = 1
                   AND t.start_point IS NOT NULL
                   AND t.end_point IS NOT NULL
                 GROUP BY cf.name, ct.name
                 ORDER BY count(*) DESC
                 LIMIT {take}
                 """)
            .ToListAsync(cancellationToken);

        var ulasim = await context.Trips
            .AsNoTracking()
            .GroupBy(x => x.TravelMode)
            .Select(g => new { Mode = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return new RouteInsightsDto
        {
            CityTrips = sehirIci
                .Select(x => new CityTripDto
                {
                    CityName = x.CityName,
                    Trips = x.Trips,
                    AverageStops = Math.Round(x.Stops, 1),
                    AverageDistanceKm = Math.Round(x.Distance / 1000, 1)
                })
                .ToList(),
            Corridors = koridorOzet
                .Select(x => new CorridorDto
                {
                    FromCityName = x.FromCityName,
                    ToCityName = x.ToCityName,
                    Trips = x.Trips
                })
                .ToList(),
            TravelModes = ulasim
                .Select(x => new NameCountDto
                {
                    Name = x.Mode == TravelMode.Foot ? "Yürüme" : "Araç",
                    Count = x.Count
                })
                .OrderByDescending(x => x.Count)
                .ToList()
        };
    }

    public async Task<MembershipDto> GetMembershipAsync(
        CancellationToken cancellationToken = default)
    {
        var simdi = DateTimeOffset.UtcNow;

        var toplam = await context.Users.CountAsync(cancellationToken);
        var premium = await AktifPremiumSayisiAsync(simdi, cancellationToken);

        // Kaynak dağılımı **geçerli** haklara bakıyor: süresi dolmuş bir
        // hakkı "premium kullanıcı" saymak sayıyı olduğundan büyük gösterir.
        var kaynaklar = await context.PremiumGrants
            .AsNoTracking()
            .Where(x => x.StartsAt <= simdi && (x.ExpiresAt == null || x.ExpiresAt > simdi))
            .GroupBy(x => x.Source)
            .Select(g => new { Source = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var kazanilan = await context.CoinEntries
            .AsNoTracking()
            .Where(x => x.Amount > 0)
            .SumAsync(x => (int?)x.Amount, cancellationToken) ?? 0;

        var harcanan = await context.CoinEntries
            .AsNoTracking()
            .Where(x => x.Amount < 0)
            .SumAsync(x => (int?)x.Amount, cancellationToken) ?? 0;

        var sebepler = await context.CoinEntries
            .AsNoTracking()
            .Where(x => x.Amount > 0)
            .GroupBy(x => x.Reason)
            .Select(g => new { Reason = g.Key, Count = g.Sum(x => x.Amount) })
            .ToListAsync(cancellationToken);

        return new MembershipDto
        {
            TotalUsers = toplam,
            PremiumUsers = premium,
            FreeUsers = toplam - premium,
            PremiumBySource = kaynaklar
                .Select(x => new NameCountDto
                {
                    Name = KaynakAdi(x.Source),
                    Count = x.Count
                })
                .OrderByDescending(x => x.Count)
                .ToList(),
            CoinsEarned = kazanilan,
            CoinsSpent = Math.Abs(harcanan),
            CoinsOutstanding = kazanilan + harcanan,
            CoinsByReason = sebepler
                .Select(x => new NameCountDto
                {
                    Name = SebepAdi(x.Reason),
                    Count = x.Count
                })
                .OrderByDescending(x => x.Count)
                .ToList()
        };
    }

    private Task<int> AktifPremiumSayisiAsync(
        DateTimeOffset simdi,
        CancellationToken cancellationToken) =>
        context.PremiumGrants
            .AsNoTracking()
            .Where(x => x.StartsAt <= simdi && (x.ExpiresAt == null || x.ExpiresAt > simdi))
            // Bir kullanıcının birden çok hakkı olabilir (uzatma, telafi);
            // kişi sayısı isteniyorsa tekilleştirmek şart.
            .Select(x => x.UserId)
            .Distinct()
            .CountAsync(cancellationToken);

    private static string KaynakAdi(PremiumSource kaynak) => kaynak switch
    {
        PremiumSource.CoinRedemption => "Coin ile",
        PremiumSource.Purchase => "Satın alma",
        PremiumSource.Manual => "Elle verildi",
        _ => kaynak.ToString()
    };

    /// <summary>Elle yazılan koridor sorgusunun satır şekli.</summary>
    private sealed record CorridorRow(string FromCityName, string ToCityName, int Trips);

    private static string SebepAdi(CoinReason sebep) => sebep switch
    {
        CoinReason.PhotoApproved => "Fotoğraf onayı",
        CoinReason.SuggestionApproved => "Yer önerisi onayı",
        CoinReason.Adjustment => "Elle düzeltme",
        _ => sebep.ToString()
    };
}
