using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Yolla.Application.Common;
using Yolla.Application.Notifications;
using Yolla.Application.Places;
using Yolla.Application.Rewards;
using Yolla.Domain.Entities;
using Yolla.Domain.Enums;
using Yolla.Infrastructure.Persistence;

namespace Yolla.Infrastructure.Services;

/// <inheritdoc cref="IPlaceSuggestionService"/>
public sealed class PlaceSuggestionService(
    YollaDbContext context,
    INotificationService notifications) : IPlaceSuggestionService
{
    /// <summary>Moderatöre gösterilecek yakın kayıtların yarıçapı (metre).</summary>
    private const double NearbyRadiusMeters = 1000;

    private const int NearbyTake = 5;

    /// <summary>
    /// Kullanıcı önerileri için sahte OSM kimliği tabanı.
    /// </summary>
    /// <remarks>
    /// <c>(OsmType, OsmId)</c> ikilisi benzersiz ve zorunlu: harvester tekrar
    /// çalıştığında kayıtları bu ikiliyle eşleştiriyor. Kullanıcı önerisinin
    /// OSM karşılığı yok, o yüzden **negatif** kimlik veriliyor — gerçek OSM
    /// kimlikleri pozitif olduğu için çakışma imkânsız ve kaydın OSM'den
    /// gelmediği kimliğine bakınca anlaşılıyor.
    /// </remarks>
    private const long SuggestedOsmIdBase = -1_000_000L;

    /// <summary>Önerilen yerin kalite puanı.</summary>
    /// <remarks>
    /// Fotoğrafı ve Wikipedia bağlantısı olmadığı için kart destesinin
    /// eşiğini geçmiyor; haritada işaret olarak görünüyor ve fotoğraf katkısı
    /// bekliyor. Fotoğraf gelince puanı hesaplayan iş yeniden değerlendiriyor.
    /// </remarks>
    private const short SuggestedQualityScore = 30;

    public async Task<IReadOnlyList<SuggestionCategoryDto>> GetCategoriesAsync(
        string language = "tr",
        CancellationToken cancellationToken = default)
    {
        var isEnglish = IsEnglish(language);

        return await context.Categories
            .AsNoTracking()
            .Where(x => x.IsVisible)
            .OrderBy(x => isEnglish ? x.NameEn : x.NameTr)
            .Select(x => new SuggestionCategoryDto
            {
                Key = x.Key,
                Name = isEnglish ? x.NameEn : x.NameTr,
                Icon = x.Icon
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<PlaceSuggestionDto> SuggestAsync(
        int userId,
        int? deviceId,
        CreatePlaceSuggestionRequest request,
        string language = "tr",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var name = (request.Name ?? string.Empty).Trim();

        if (name.Length < RewardRules.MinSuggestionNameLength
            || name.Length > RewardRules.MaxSuggestionNameLength)
        {
            throw RequestValidationException.Single(
                nameof(request.Name),
                $"Yer adı {RewardRules.MinSuggestionNameLength}-{RewardRules.MaxSuggestionNameLength} karakter olmalı.");
        }

        var description = Trimmed(request.Description);

        if (description is { Length: > RewardRules.MaxSuggestionDescriptionLength })
        {
            throw RequestValidationException.Single(
                nameof(request.Description),
                $"Tanıtım en fazla {RewardRules.MaxSuggestionDescriptionLength} karakter olabilir.");
        }

        var category = await context.Categories
            .FirstOrDefaultAsync(x => x.Key == request.CategoryKey, cancellationToken)
            ?? throw RequestValidationException.Single(
                nameof(request.CategoryKey), "Böyle bir kategori yok.");

        var location = new Point(request.Longitude, request.Latitude) { SRID = 4326 };

        // Şehir gönderide bulunuyor: koordinat sınırların dışındaysa kullanıcı
        // bunu göndermeden öğrenmeli, moderatörün önüne düşmemeli.
        var city = await context.Cities
            .Where(x => x.Boundary != null && x.Boundary.Contains(location))
            .Select(x => new { x.Id, x.Name })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw RequestValidationException.Single(
                nameof(request.Latitude),
                "Bu konum Türkiye sınırlarının dışında görünüyor.");

        var districtId = await context.Districts
            .Where(x => x.CityId == city.Id && x.Boundary != null && x.Boundary.Contains(location))
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var pending = await context.PlaceSuggestions
            .CountAsync(
                x => x.UserId == userId && x.Status == PlaceSuggestionStatus.Pending,
                cancellationToken);

        if (pending >= RewardRules.MaxPendingSuggestionsPerUser)
        {
            throw RequestValidationException.Single(
                "pending",
                $"Aynı anda en fazla {RewardRules.MaxPendingSuggestionsPerUser} önerin incelemede olabilir.");
        }

        await EnsureNotDuplicateAsync(name, location, cancellationToken);

        var suggestion = new PlaceSuggestion
        {
            UserId = userId,
            DeviceId = deviceId,
            Name = name,
            CategoryId = category.Id,
            Location = location,
            CityId = city.Id,
            DistrictId = districtId,
            Description = description,
            Address = Trimmed(request.Address)
        };

        context.PlaceSuggestions.Add(suggestion);
        await context.SaveChangesAsync(cancellationToken);

        var isEnglish = IsEnglish(language);

        return new PlaceSuggestionDto
        {
            Id = suggestion.Id,
            Name = suggestion.Name,
            CategoryName = isEnglish ? category.NameEn : category.NameTr,
            CityName = city.Name,
            Latitude = location.Y,
            Longitude = location.X,
            Description = suggestion.Description,
            Status = suggestion.Status,
            CreatedAt = suggestion.CreatedAt
        };
    }

    public async Task<IReadOnlyList<PlaceSuggestionDto>> GetMineAsync(
        int userId,
        string language = "tr",
        CancellationToken cancellationToken = default)
    {
        var isEnglish = IsEnglish(language);

        // Koordinatlar kayıt çekildikten sonra bellekte okunuyor: `geography`
        // sütununda `.Y`/`.X` sorguya çevrilemiyor. Katalogdaki diğer
        // servisler de aynı yolu izliyor.
        var rows = await context.PlaceSuggestions
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.Name,
                CategoryName = isEnglish ? x.Category.NameEn : x.Category.NameTr,
                CityName = x.City.Name,
                x.Location,
                x.Description,
                x.Status,
                x.RejectionReason,
                x.PlaceId,
                x.CoinsAwarded,
                x.CreatedAt,
                x.ReviewedAt
            })
            .ToListAsync(cancellationToken);

        return rows.Select(x => new PlaceSuggestionDto
        {
            Id = x.Id,
            Name = x.Name,
            CategoryName = x.CategoryName,
            CityName = x.CityName,
            Latitude = x.Location.Y,
            Longitude = x.Location.X,
            Description = x.Description,
            Status = x.Status,
            RejectionReason = x.RejectionReason,
            PlaceId = x.PlaceId,
            CoinsAwarded = x.CoinsAwarded,
            CreatedAt = x.CreatedAt,
            ReviewedAt = x.ReviewedAt
        }).ToList();
    }

    public async Task<IReadOnlyList<PlaceSuggestionModerationDto>> GetPendingAsync(
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var rows = await context.PlaceSuggestions
            .AsNoTracking()
            .Where(x => x.Status == PlaceSuggestionStatus.Pending)
            .OrderBy(x => x.CreatedAt)
            .Take(Math.Clamp(take, 1, 200))
            .Select(x => new
            {
                x.Id,
                x.Name,
                CategoryName = x.Category.NameTr,
                CityName = x.City.Name,
                DistrictName = x.District != null ? x.District.Name : null,
                x.Location,
                x.Description,
                x.Address,
                x.UserId,
                x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var result = new List<PlaceSuggestionModerationDto>(rows.Count);

        foreach (var row in rows)
        {
            // Yakın kayıtlar öneri başına ayrı sorgulanıyor: tek sorguda
            // yapmanın yolu her satır için ayrı bir mesafe koşulu üretmek ve
            // kuyruk zaten en fazla iki yüz satır.
            var nearby = await context.Places
                .AsNoTracking()
                .Where(x => x.IsActive && x.Location.IsWithinDistance(row.Location, NearbyRadiusMeters))
                .OrderBy(x => x.Location.Distance(row.Location))
                .Take(NearbyTake)
                .Select(x => new SuggestionNearbyDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    CategoryName = x.Category.NameTr,
                    DistanceMeters = (int)x.Location.Distance(row.Location)
                })
                .ToListAsync(cancellationToken);

            var approved = await context.PlaceSuggestions
                .CountAsync(
                    x => x.UserId == row.UserId && x.Status == PlaceSuggestionStatus.Approved,
                    cancellationToken);

            result.Add(new PlaceSuggestionModerationDto
            {
                Id = row.Id,
                Name = row.Name,
                CategoryName = row.CategoryName,
                CityName = row.CityName,
                DistrictName = row.DistrictName,
                Latitude = row.Location.Y,
                Longitude = row.Location.X,
                Description = row.Description,
                Address = row.Address,
                UserId = row.UserId,
                UserApprovedCount = approved,
                Nearby = nearby,
                CreatedAt = row.CreatedAt
            });
        }

        return result;
    }

    public async Task ApproveAsync(
        int suggestionId,
        int reviewerUserId,
        CancellationToken cancellationToken = default)
    {
        var suggestion = await context.PlaceSuggestions
            .Include(x => x.City)
            .FirstOrDefaultAsync(x => x.Id == suggestionId, cancellationToken)
            ?? throw new NotFoundException("Öneri", suggestionId);

        if (suggestion.Status != PlaceSuggestionStatus.Pending)
        {
            throw RequestValidationException.Single(
                "status", "Bu öneri zaten incelenmiş.");
        }

        var place = new Place
        {
            CountryId = suggestion.City.CountryId,
            CityId = suggestion.CityId,
            DistrictId = suggestion.DistrictId,
            CategoryId = suggestion.CategoryId,
            OsmType = OsmElementType.Node,
            OsmId = SuggestedOsmIdBase - suggestion.Id,
            Name = suggestion.Name,
            Slug = TextNormalizer.SlugifyWithSuffix(suggestion.Name, suggestion.City.Name),
            Location = suggestion.Location,
            Address = suggestion.Address,
            DescriptionTr = suggestion.Description,
            QualityScore = SuggestedQualityScore,
            IsActive = true
        };

        context.Places.Add(place);
        await context.SaveChangesAsync(cancellationToken);

        suggestion.Status = PlaceSuggestionStatus.Approved;
        suggestion.ReviewedAt = DateTimeOffset.UtcNow;
        suggestion.ReviewedByUserId = reviewerUserId;
        suggestion.PlaceId = place.Id;
        suggestion.CoinsAwarded = RewardRules.CoinsPerApprovedSuggestion;
        suggestion.UpdatedAt = DateTimeOffset.UtcNow;

        // Defterdeki koşullu benzersiz indeks aynı öneriye ikinci kez coin
        // yazılmasını veritabanı düzeyinde engelliyor.
        context.CoinEntries.Add(new CoinEntry
        {
            UserId = suggestion.UserId,
            Amount = RewardRules.CoinsPerApprovedSuggestion,
            Reason = CoinReason.SuggestionApproved,
            PlaceSuggestionId = suggestion.Id
        });

        await context.SaveChangesAsync(cancellationToken);

        await notifications.CreateAsync(
            suggestion.UserId,
            NotificationKind.SuggestionApproved,
            new NotificationContext
            {
                PlaceName = suggestion.Name,
                PlaceId = place.Id,
                Coins = RewardRules.CoinsPerApprovedSuggestion
            },
            cancellationToken);
    }

    public async Task RejectAsync(
        int suggestionId,
        int reviewerUserId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var suggestion = await context.PlaceSuggestions
            .FirstOrDefaultAsync(x => x.Id == suggestionId, cancellationToken)
            ?? throw new NotFoundException("Öneri", suggestionId);

        if (suggestion.Status != PlaceSuggestionStatus.Pending)
        {
            throw RequestValidationException.Single(
                "status", "Bu öneri zaten incelenmiş.");
        }

        suggestion.Status = PlaceSuggestionStatus.Rejected;
        suggestion.ReviewedAt = DateTimeOffset.UtcNow;
        suggestion.ReviewedByUserId = reviewerUserId;
        suggestion.RejectionReason = Trimmed(reason);
        suggestion.UpdatedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        await notifications.CreateAsync(
            suggestion.UserId,
            NotificationKind.SuggestionRejected,
            new NotificationContext
            {
                PlaceName = suggestion.Name,
                Reason = suggestion.RejectionReason
            },
            cancellationToken);
    }

    /// <summary>
    /// Aynı yer zaten kayıtlıysa ya da önerilmişse gönderiyi durdurur.
    /// </summary>
    /// <remarks>
    /// Ad karşılaştırması Türkçeye duyarsız normalize üzerinden yapılıyor
    /// ("Kalesi" / "kalesı" aynı sayılıyor). Yalnızca mesafeye bakmak yanlış
    /// olurdu: aynı meydanda birbirine yakın iki ayrı yer olabilir.
    /// </remarks>
    private async Task EnsureNotDuplicateAsync(
        string name,
        Point location,
        CancellationToken cancellationToken)
    {
        var normalized = TextNormalizer.Normalize(name);
        var radius = RewardRules.DuplicateSuggestionRadiusMeters;

        var nearbyNames = await context.Places
            .AsNoTracking()
            .Where(x => x.IsActive && x.Location.IsWithinDistance(location, radius))
            .Select(x => x.Name)
            .ToListAsync(cancellationToken);

        if (nearbyNames.Any(x => TextNormalizer.Normalize(x) == normalized))
        {
            throw RequestValidationException.Single(
                "name", "Bu yer zaten kayıtlı görünüyor.");
        }

        var pendingNames = await context.PlaceSuggestions
            .AsNoTracking()
            .Where(x => x.Status == PlaceSuggestionStatus.Pending
                        && x.Location.IsWithinDistance(location, radius))
            .Select(x => x.Name)
            .ToListAsync(cancellationToken);

        if (pendingNames.Any(x => TextNormalizer.Normalize(x) == normalized))
        {
            throw RequestValidationException.Single(
                "name", "Bu yer için zaten bekleyen bir öneri var.");
        }
    }

    private static string? Trimmed(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static bool IsEnglish(string language) =>
        language.StartsWith("en", StringComparison.OrdinalIgnoreCase);
}
