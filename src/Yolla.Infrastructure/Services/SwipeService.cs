using Microsoft.EntityFrameworkCore;
using Yolla.Application.Common;
using Yolla.Application.Discovery;
using Yolla.Domain.Entities;
using Yolla.Domain.Enums;
using Yolla.Infrastructure.Persistence;

namespace Yolla.Infrastructure.Services;

/// <inheritdoc cref="ISwipeService"/>
public sealed class SwipeService(YollaDbContext context) : ISwipeService
{
    /// <summary>Tek istekte kabul edilen en fazla kaydırma sayısı.</summary>
    public const int MaxBatchSize = 200;

    public async Task<SwipeResultDto> RecordAsync(
        int deviceId,
        IReadOnlyList<SwipeRequest> swipes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(swipes);

        if (swipes.Count == 0)
        {
            throw RequestValidationException.Single(nameof(swipes), "En az bir kaydırma gönderilmeli.");
        }

        if (swipes.Count > MaxBatchSize)
        {
            throw RequestValidationException.Single(
                nameof(swipes), $"Tek istekte en fazla {MaxBatchSize} kaydırma gönderilebilir.");
        }

        var deviceExists = await context.Devices.AnyAsync(x => x.Id == deviceId, cancellationToken);

        if (!deviceExists)
        {
            throw new NotFoundException("Cihaz", deviceId);
        }

        // Aynı yer birden fazla kez gönderilmişse son gönderilen geçerlidir
        var byPlace = swipes
            .GroupBy(x => x.PlaceId)
            .ToDictionary(g => g.Key, g => g.Last());

        var placeIds = byPlace.Keys.ToList();

        var knownPlaceIds = await context.Places
            .Where(x => placeIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (knownPlaceIds.Count == 0)
        {
            throw RequestValidationException.Single(nameof(swipes), "Gönderilen yerler bulunamadı.");
        }

        var existing = await context.Swipes
            .Where(x => x.DeviceId == deviceId && placeIds.Contains(x.PlaceId))
            .ToDictionaryAsync(x => x.PlaceId, cancellationToken);

        foreach (var placeId in knownPlaceIds)
        {
            var request = byPlace[placeId];

            if (existing.TryGetValue(placeId, out var swipe))
            {
                // Kullanıcı fikrini değiştirebilir: yeni kayıt açılmaz, yön güncellenir
                swipe.Direction = request.Direction;
                swipe.Context = request.Context;
                swipe.TripId = request.TripId;
                continue;
            }

            context.Swipes.Add(new Swipe
            {
                DeviceId = deviceId,
                PlaceId = placeId,
                Direction = request.Direction,
                Context = request.Context,
                TripId = request.TripId
            });
        }

        await context.SaveChangesAsync(cancellationToken);

        var totalLiked = await context.Swipes
            .CountAsync(x => x.DeviceId == deviceId && x.Direction == SwipeDirection.Like, cancellationToken);

        return new SwipeResultDto
        {
            Recorded = knownPlaceIds.Count,
            TotalLiked = totalLiked
        };
    }

    public async Task<SwipeUndoResultDto> UndoAsync(
        int deviceId,
        int placeId,
        CancellationToken cancellationToken = default)
    {
        var swipe = await context.Swipes
            .FirstOrDefaultAsync(x => x.DeviceId == deviceId && x.PlaceId == placeId, cancellationToken);

        if (swipe is not null)
        {
            // Kayıt silinir, yön değiştirilmez: feed daha önce kaydırılmış her yeri
            // eliyor. Yerin desteye geri dönmesi ancak kaydın kalkmasıyla olur.
            context.Swipes.Remove(swipe);
            await context.SaveChangesAsync(cancellationToken);
        }

        var totalLiked = await context.Swipes
            .CountAsync(x => x.DeviceId == deviceId && x.Direction == SwipeDirection.Like, cancellationToken);

        return new SwipeUndoResultDto
        {
            Removed = swipe is not null,
            TotalLiked = totalLiked
        };
    }

    public async Task<IReadOnlyList<PlaceCardDto>> GetLikedPlacesAsync(
        int deviceId,
        string language = "tr",
        CancellationToken cancellationToken = default)
    {
        var isEnglish = language.StartsWith("en", StringComparison.OrdinalIgnoreCase);

        var rows = await context.Swipes
            .AsNoTracking()
            .Where(x => x.DeviceId == deviceId && x.Direction == SwipeDirection.Like)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Place.Id,
                x.Place.Name,
                x.Place.Slug,
                CategoryKey = x.Place.Category.Key,
                CategoryNameTr = x.Place.Category.NameTr,
                CategoryNameEn = x.Place.Category.NameEn,
                CategoryIcon = x.Place.Category.Icon,
                x.Place.PhotoUrl,
                x.Place.PhotoAuthor,
                x.Place.PhotoLicense,
                x.Place.PhotoSource,
                x.Place.DescriptionTr,
                x.Place.DescriptionEn,
                CityName = x.Place.City.Name,
                DistrictName = x.Place.District != null ? x.Place.District.Name : null,
                x.Place.Location,
                x.Place.AvgVisitMinutes,
                x.Place.QualityScore
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(x => new PlaceCardDto
            {
                Id = x.Id,
                Name = x.Name,
                Slug = x.Slug,
                CategoryKey = x.CategoryKey,
                CategoryName = isEnglish ? x.CategoryNameEn : x.CategoryNameTr,
                CategoryIcon = x.CategoryIcon,
                PhotoUrl = x.PhotoUrl ?? string.Empty,
                PhotoAttribution = Attribution.ForPhoto(x.PhotoAuthor, x.PhotoLicense) ?? string.Empty,
                PhotoSource = x.PhotoSource,
                Description = isEnglish ? x.DescriptionEn ?? x.DescriptionTr : x.DescriptionTr,
                CityName = x.CityName,
                DistrictName = x.DistrictName,
                Latitude = x.Location.Y,
                Longitude = x.Location.X,
                AverageVisitMinutes = x.AvgVisitMinutes,
                QualityScore = x.QualityScore
            })
            .ToList();
    }
}
