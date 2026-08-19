using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using Yolla.Application.Common;
using Yolla.Application.Rewards;
using Yolla.Domain.Entities;
using Yolla.Domain.Enums;
using Yolla.Infrastructure.Persistence;

namespace Yolla.Infrastructure.Services;

/// <inheritdoc cref="IRewardService"/>
public sealed class RewardService(
    YollaDbContext context,
    IPhotoStorage storage,
    ICacheService cache) : IRewardService
{
    /// <summary>
    /// Gönderilen fotoğrafın kaydedileceği en büyük kenar.
    /// </summary>
    /// <remarks>
    /// Telefon fotoğrafları 12 MP ve üstü geliyor. Kart olarak en fazla
    /// 1920 px kullanılıyor; ham dosyayı saklamak diski boşuna doldurur ve
    /// moderasyon sayfasını yavaşlatır.
    /// </remarks>
    private const int MaxStoredEdge = 1920;

    public async Task<PhotoSubmissionDto> SubmitPhotoAsync(
        int userId,
        int? deviceId,
        int placeId,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var place = await context.Places
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == placeId && x.IsActive, cancellationToken)
            ?? throw new NotFoundException("Yer", placeId);

        var pending = await context.PhotoSubmissions
            .CountAsync(
                x => x.UserId == userId && x.Status == PhotoSubmissionStatus.Pending,
                cancellationToken);

        if (pending >= RewardRules.MaxPendingPerUser)
        {
            throw RequestValidationException.Single(
                "submission",
                $"Bekleyen {RewardRules.MaxPendingPerUser} gönderin var. " +
                "Bunlar incelendikten sonra yenisini gönderebilirsin.");
        }

        // Aynı yere aynı kullanıcıdan bekleyen ikinci bir gönderi olmasın:
        // moderasyon kuyruğunda aynı şeyi iki kere görmek zaman kaybı.
        var duplicate = await context.PhotoSubmissions.AnyAsync(
            x => x.UserId == userId
                 && x.PlaceId == placeId
                 && x.Status == PhotoSubmissionStatus.Pending,
            cancellationToken);

        if (duplicate)
        {
            throw RequestValidationException.Single(
                "placeId", "Bu yer için bekleyen bir gönderin zaten var.");
        }

        using var image = await LoadAndNormalizeAsync(content, cancellationToken);

        var relativePath = $"submissions/{userId}/{Guid.NewGuid():N}.jpg";

        using var buffer = new MemoryStream();
        await image.SaveAsync(buffer, new JpegEncoder { Quality = 82 }, cancellationToken);
        buffer.Position = 0;

        await storage.SaveAsync(relativePath, buffer, cancellationToken);

        var submission = new PhotoSubmission
        {
            PlaceId = placeId,
            UserId = userId,
            DeviceId = deviceId,
            StoragePath = relativePath,
            ContentType = "image/jpeg",
            SizeBytes = buffer.Length,
            Width = image.Width,
            Height = image.Height,
            Status = PhotoSubmissionStatus.Pending
        };

        context.PhotoSubmissions.Add(submission);
        await context.SaveChangesAsync(cancellationToken);

        return ToDto(submission, place.Name);
    }

    /// <summary>Gönderiyi okur, doğrular ve saklanacak boyuta indirir.</summary>
    private async Task<Image> LoadAndNormalizeAsync(
        Stream content,
        CancellationToken cancellationToken)
    {
        Image image;
        try
        {
            image = await Image.LoadAsync(content, cancellationToken);
        }
        catch (UnknownImageFormatException)
        {
            throw RequestValidationException.Single(
                "photo", "Dosya okunamadı; JPEG veya PNG bir fotoğraf gönder.");
        }

        if (image.Width < RewardRules.MinPhotoEdge && image.Height < RewardRules.MinPhotoEdge)
        {
            image.Dispose();
            throw RequestValidationException.Single(
                "photo",
                $"Fotoğraf çok küçük. Kısa kenarı en az {RewardRules.MinPhotoEdge} piksel olmalı.");
        }

        if (image.Width > MaxStoredEdge || image.Height > MaxStoredEdge)
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(MaxStoredEdge, MaxStoredEdge)
            }));
        }

        // EXIF yönü uygulanıp temizleniyor: telefon fotoğrafları yan
        // görünmesin, konum bilgisi de dosyayla birlikte yayına çıkmasın.
        image.Mutate(x => x.AutoOrient());
        image.Metadata.ExifProfile = null;

        return image;
    }

    public async Task<IReadOnlyList<PhotoSubmissionDto>> GetMySubmissionsAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var rows = await context.PhotoSubmissions
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new { Submission = x, PlaceName = x.Place.Name })
            .ToListAsync(cancellationToken);

        return rows.Select(x => ToDto(x.Submission, x.PlaceName)).ToList();
    }

    public async Task<RewardStatusDto> GetStatusAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var balance = await GetBalanceAsync(userId, cancellationToken);

        var counts = await context.PhotoSubmissions
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var grant = await GetActiveGrantAsync(userId, cancellationToken);
        var tripsThisMonth = await CountTripsThisMonthAsync(userId, cancellationToken);

        return new RewardStatusDto
        {
            CoinBalance = balance,
            PendingSubmissions = CountOf(counts, PhotoSubmissionStatus.Pending),
            ApprovedSubmissions = CountOf(counts, PhotoSubmissionStatus.Approved),
            RejectedSubmissions = CountOf(counts, PhotoSubmissionStatus.Rejected),
            IsPremium = grant is not null,
            PremiumExpiresAt = grant?.ExpiresAt,
            IsPremiumUnlimited = grant is { ExpiresAt: null },
            TripsThisMonth = tripsThisMonth,
            // Premiumda sınır yok; istemci null görünce kotayı hiç göstermiyor.
            MonthlyTripLimit = grant is null ? RewardRules.FreeMonthlyTripLimit : null
        };

        static int CountOf(
            IEnumerable<dynamic> rows,
            PhotoSubmissionStatus status)
        {
            foreach (var row in rows)
            {
                if ((PhotoSubmissionStatus)row.Status == status) return (int)row.Count;
            }
            return 0;
        }
    }

    public async Task<IReadOnlyList<CoinEntryDto>> GetCoinHistoryAsync(
        int userId,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        return await context.CoinEntries
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(Math.Clamp(take, 1, 200))
            .Select(x => new CoinEntryDto
            {
                Amount = x.Amount,
                Reason = x.Reason,
                Note = x.Note,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<RewardStatusDto> RedeemAsync(
        int userId,
        PremiumPackage package,
        CancellationToken cancellationToken = default)
    {
        var cost = RewardRules.CostFor(package)
                   ?? throw RequestValidationException.Single("package", "Geçersiz paket.");

        var balance = await GetBalanceAsync(userId, cancellationToken);

        if (balance < cost)
        {
            throw RequestValidationException.Single(
                "coins",
                $"Bu paket {cost} coin. Bakiyen {balance} coin.");
        }

        var now = DateTimeOffset.UtcNow;
        var existing = await GetActiveGrantAsync(userId, cancellationToken);

        if (existing is { ExpiresAt: null })
        {
            throw RequestValidationException.Single(
                "package", "Zaten süresiz premium hakkın var.");
        }

        var duration = RewardRules.DurationFor(package);

        // Süre uzatılırken mevcut hakkın bitişinden devam ediliyor; şimdiden
        // başlatılsaydı kullanıcı kalan günlerini kaybederdi.
        var startsAt = existing?.ExpiresAt is { } end && end > now ? end : now;

        var grant = new PremiumGrant
        {
            UserId = userId,
            StartsAt = startsAt,
            ExpiresAt = duration is null ? null : startsAt + duration.Value,
            Source = PremiumSource.CoinRedemption,
            CoinsSpent = cost
        };

        context.PremiumGrants.Add(grant);
        await context.SaveChangesAsync(cancellationToken);

        context.CoinEntries.Add(new CoinEntry
        {
            UserId = userId,
            Amount = -cost,
            Reason = CoinReason.PremiumRedeemed,
            PremiumGrantId = grant.Id
        });

        await context.SaveChangesAsync(cancellationToken);

        return await GetStatusAsync(userId, cancellationToken);
    }

    public async Task<bool> IsPremiumAsync(
        int userId,
        CancellationToken cancellationToken = default) =>
        await GetActiveGrantAsync(userId, cancellationToken) is not null;

    public async Task<bool> CanCreateTripAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (await IsPremiumAsync(userId, cancellationToken)) return true;

        var used = await CountTripsThisMonthAsync(userId, cancellationToken);
        return used < RewardRules.FreeMonthlyTripLimit;
    }

    public async Task<IReadOnlyList<ModerationItemDto>> GetPendingAsync(
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var rows = await context.PhotoSubmissions
            .AsNoTracking()
            .Where(x => x.Status == PhotoSubmissionStatus.Pending)
            .OrderBy(x => x.CreatedAt)
            .Take(Math.Clamp(take, 1, 200))
            .Select(x => new
            {
                x.Id,
                x.PlaceId,
                PlaceName = x.Place.Name,
                CityName = x.Place.City.Name,
                ExistingPhotoUrl = x.Place.PhotoUrl,
                x.StoragePath,
                x.Width,
                x.Height,
                x.SizeBytes,
                x.UserId,
                x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0) return [];

        var userIds = rows.Select(x => x.UserId).Distinct().ToList();

        var emails = await context.Users
            .AsNoTracking()
            .Where(x => userIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Email })
            .ToDictionaryAsync(x => x.Id, x => x.Email, cancellationToken);

        // Gönderenin geçmişi: aynı kişi sürekli reddediliyorsa inceleyen
        // bunu görüp daha dikkatli baksın.
        var history = await context.PhotoSubmissions
            .AsNoTracking()
            .Where(x => userIds.Contains(x.UserId)
                        && x.Status != PhotoSubmissionStatus.Pending)
            .GroupBy(x => new { x.UserId, x.Status })
            .Select(g => new { g.Key.UserId, g.Key.Status, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return rows.Select(x => new ModerationItemDto
        {
            Id = x.Id,
            PlaceId = x.PlaceId,
            PlaceName = x.PlaceName,
            CityName = x.CityName,
            ExistingPhotoUrl = x.ExistingPhotoUrl,
            Url = storage.UrlFor(x.StoragePath),
            Width = x.Width,
            Height = x.Height,
            SizeBytes = x.SizeBytes,
            UserId = x.UserId,
            UserEmail = emails.GetValueOrDefault(x.UserId),
            UserApprovedCount = history
                .Where(h => h.UserId == x.UserId
                            && h.Status == PhotoSubmissionStatus.Approved)
                .Sum(h => h.Count),
            UserRejectedCount = history
                .Where(h => h.UserId == x.UserId
                            && h.Status == PhotoSubmissionStatus.Rejected)
                .Sum(h => h.Count),
            CreatedAt = x.CreatedAt
        }).ToList();
    }

    public async Task ApproveAsync(
        int submissionId,
        int reviewerUserId,
        CancellationToken cancellationToken = default)
    {
        var submission = await context.PhotoSubmissions
            .Include(x => x.Place)
            .FirstOrDefaultAsync(x => x.Id == submissionId, cancellationToken)
            ?? throw new NotFoundException("Gönderi", submissionId);

        if (submission.Status != PhotoSubmissionStatus.Pending)
        {
            throw RequestValidationException.Single(
                "status", "Bu gönderi zaten incelenmiş.");
        }

        // Yayına alınıyor: katkı kaydı otomatik kaynakların üstünde gösteriliyor.
        var contribution = new PlaceContribution
        {
            PlaceId = submission.PlaceId,
            Type = ContributionType.Photo,
            Value = storage.UrlFor(submission.StoragePath),
            Author = $"Yolla kullanıcısı #{submission.UserId}",
            License = "Yolla",
            SubmittedBy = submission.UserId.ToString(),
            IsPublished = true
        };

        context.PlaceContributions.Add(contribution);
        await context.SaveChangesAsync(cancellationToken);

        submission.Status = PhotoSubmissionStatus.Approved;
        submission.ReviewedAt = DateTimeOffset.UtcNow;
        submission.ReviewedByUserId = reviewerUserId;
        submission.CoinsAwarded = RewardRules.CoinsPerApprovedPhoto;
        submission.PlaceContributionId = contribution.Id;
        submission.UpdatedAt = DateTimeOffset.UtcNow;

        // Defterdeki benzersiz indeks aynı gönderiye ikinci kez coin
        // yazılmasını veritabanı düzeyinde engelliyor.
        context.CoinEntries.Add(new CoinEntry
        {
            UserId = submission.UserId,
            Amount = RewardRules.CoinsPerApprovedPhoto,
            Reason = CoinReason.PhotoApproved,
            PhotoSubmissionId = submission.Id
        });

        await context.SaveChangesAsync(cancellationToken);

        // Yer detayı önbellekte 2 saat duruyor; temizlenmezse yeni fotoğraf
        // görünmez.
        await cache.RemoveAsync(CacheKeys.PlaceDetail(submission.PlaceId, "tr"), cancellationToken);
        await cache.RemoveAsync(CacheKeys.PlaceDetail(submission.PlaceId, "en"), cancellationToken);
    }

    public async Task RejectAsync(
        int submissionId,
        int reviewerUserId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var submission = await context.PhotoSubmissions
            .FirstOrDefaultAsync(x => x.Id == submissionId, cancellationToken)
            ?? throw new NotFoundException("Gönderi", submissionId);

        if (submission.Status != PhotoSubmissionStatus.Pending)
        {
            throw RequestValidationException.Single(
                "status", "Bu gönderi zaten incelenmiş.");
        }

        submission.Status = PhotoSubmissionStatus.Rejected;
        submission.ReviewedAt = DateTimeOffset.UtcNow;
        submission.ReviewedByUserId = reviewerUserId;
        submission.RejectionReason = reason.Trim();
        submission.UpdatedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        // Dosya siliniyor: reddedilen içerik diskte durmasın. Kayıt kalıyor,
        // aynı kullanıcı aynı yere tekrar tekrar göndermesin diye.
        await storage.DeleteAsync(submission.StoragePath, cancellationToken);
    }

    private Task<int> GetBalanceAsync(int userId, CancellationToken cancellationToken) =>
        context.CoinEntries
            .Where(x => x.UserId == userId)
            .SumAsync(x => (int?)x.Amount, cancellationToken)
            .ContinueWith(t => t.Result ?? 0, cancellationToken,
                TaskContinuationOptions.OnlyOnRanToCompletion,
                TaskScheduler.Default);

    private async Task<PremiumGrant?> GetActiveGrantAsync(
        int userId,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        // Süresiz hak önce geliyor: varsa bitiş tarihi olanlara bakmaya gerek yok.
        return await context.PremiumGrants
            .AsNoTracking()
            .Where(x => x.UserId == userId
                        && x.StartsAt <= now
                        && (x.ExpiresAt == null || x.ExpiresAt > now))
            .OrderBy(x => x.ExpiresAt == null ? 0 : 1)
            .ThenByDescending(x => x.ExpiresAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>Bu takvim ayında kaydedilen plan sayısı.</summary>
    /// <remarks>
    /// Kota toplam bir tavan değil: her ayın 1'inde sıfırlanıyor. Silinen
    /// planlar da sayılıyor — yoksa kotayı silip yeniden kurarak sonsuza kadar
    /// aşmak mümkün olurdu.
    /// </remarks>
    private Task<int> CountTripsThisMonthAsync(int userId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);

        return context.Trips
            .IgnoreQueryFilters()
            .CountAsync(
                x => x.UserId == userId && x.CreatedAt >= monthStart,
                cancellationToken);
    }

    private PhotoSubmissionDto ToDto(PhotoSubmission x, string placeName) =>
        new()
        {
            Id = x.Id,
            PlaceId = x.PlaceId,
            PlaceName = placeName,
            Status = x.Status.ToString(),
            Url = storage.UrlFor(x.StoragePath),
            RejectionReason = x.RejectionReason,
            CoinsAwarded = x.CoinsAwarded,
            CreatedAt = x.CreatedAt,
            ReviewedAt = x.ReviewedAt
        };
}
