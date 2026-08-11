using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Yolla.Application.Common;
using Yolla.Application.Discovery;
using Yolla.Application.Places;
using Yolla.Domain.Entities;
using Yolla.Infrastructure.Persistence;

namespace Yolla.Infrastructure.Services;

/// <inheritdoc cref="IDiscoveryService"/>
public sealed class DiscoveryService(YollaDbContext context) : IDiscoveryService
{
    private const int MinTake = 1;
    private const int MaxTake = 50;

    public async Task<CursorPage<PlaceCardDto>> GetCityFeedAsync(
        CityFeedRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var cityExists = await context.Cities
            .AnyAsync(x => x.Id == request.CityId && x.IsActive, cancellationToken);

        if (!cityExists)
        {
            throw new NotFoundException("Şehir", request.CityId);
        }

        var take = Math.Clamp(request.Take, MinTake, MaxTake);

        var query = BuildBaseQuery(request);

        // İmleç: (puan, kimlik) ikilisinden küçük olanlar. Puan azalan sırada olduğu için
        // "daha küçük puan" ya da "eşit puanda daha büyük kimlik" koşulu aranır.
        if (FeedCursor.TryDecode(request.Cursor, out var cursor))
        {
            query = query.Where(x =>
                x.QualityScore < cursor.QualityScore
                || (x.QualityScore == cursor.QualityScore && x.Id > cursor.PlaceId));
        }

        // Bir fazlası çekiliyor: devamı var mı anlamak için
        var entities = await query
            .OrderByDescending(x => x.QualityScore)
            .ThenBy(x => x.Id)
            .Take(take + 1)
            .Select(x => new PlaceProjection
            {
                Id = x.Id,
                Name = x.Name,
                Slug = x.Slug,
                CategoryKey = x.Category.Key,
                CategoryNameTr = x.Category.NameTr,
                CategoryNameEn = x.Category.NameEn,
                CategoryIcon = x.Category.Icon,
                PhotoUrl = x.PhotoUrl!,
                PhotoAuthor = x.PhotoAuthor,
                PhotoLicense = x.PhotoLicense,
                PhotoSource = x.PhotoSource,
                DescriptionTr = x.DescriptionTr,
                DescriptionEn = x.DescriptionEn,
                CityName = x.City.Name,
                DistrictName = x.District != null ? x.District.Name : null,
                Location = x.Location,
                AverageVisitMinutes = x.AvgVisitMinutes,
                QualityScore = x.QualityScore
            })
            .ToListAsync(cancellationToken);

        var hasMore = entities.Count > take;

        if (hasMore)
        {
            entities.RemoveAt(entities.Count - 1);
        }

        if (entities.Count == 0)
        {
            return CursorPage<PlaceCardDto>.Empty();
        }

        // İmleç çeşitlendirmeden ÖNCEKİ sıraya göre üretilir; çeşitlendirme yalnızca
        // sayfa içi görüntüleme sırasını değiştirir, sayfalama bütünlüğünü bozmaz
        var last = entities[^1];
        var nextCursor = hasMore ? new FeedCursor(last.QualityScore, last.Id).Encode() : null;

        var cards = entities.Select(x => x.ToCard(request.Language)).ToList();

        return CursorPage<PlaceCardDto>.Create(FeedDiversifier.Diversify(cards), nextCursor);
    }

    private IQueryable<Place> BuildBaseQuery(CityFeedRequest request)
    {
        var query = context.Places
            .AsNoTracking()
            .Where(x => x.CityId == request.CityId)
            .Where(x => x.IsActive)
            .Where(x => x.Category.IsVisible)
            // Kart fotoğrafsız olamaz
            .Where(x => x.PhotoUrl != null)
            // Atıf bilgisi eksik fotoğraf gösterilemez: Commons görsellerinin çoğu CC BY-SA
            // ve fotoğrafçı adı ile lisansı göstermek hukuki zorunluluk
            .Where(x => x.PhotoAuthor != null && x.PhotoLicense != null)
            .Where(x => x.QualityScore >= PlaceQualityScorer.FeedThreshold);

        if (request.CategoryKeys is { Count: > 0 })
        {
            var keys = request.CategoryKeys;
            query = query.Where(x => keys.Contains(x.Category.Key));
        }

        // Daha önce kaydırılmış yerler tekrar gösterilmez
        if (request.DeviceId is { } deviceId)
        {
            query = query.Where(x =>
                !context.Swipes.Any(s => s.DeviceId == deviceId && s.PlaceId == x.Id));
        }

        return query;
    }

    /// <summary>Sorgu sonucunun ara temsili; dil seçimi bellekte yapılır.</summary>
    private sealed record PlaceProjection
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
        public required string Slug { get; init; }
        public required string CategoryKey { get; init; }
        public required string CategoryNameTr { get; init; }
        public required string CategoryNameEn { get; init; }
        public string? CategoryIcon { get; init; }
        public required string PhotoUrl { get; init; }
        public string? PhotoAuthor { get; init; }
        public string? PhotoLicense { get; init; }
        public string? PhotoSource { get; init; }
        public string? DescriptionTr { get; init; }
        public string? DescriptionEn { get; init; }
        public required string CityName { get; init; }
        public string? DistrictName { get; init; }

        /// <remarks>
        /// Nokta nesnesi olarak çekiliyor: PostGIS'te ST_X/ST_Y yalnızca <c>geometry</c>
        /// üzerinde tanımlı, kolonumuz ise <c>geography</c>.
        /// </remarks>
        public required Point Location { get; init; }

        public short? AverageVisitMinutes { get; init; }
        public required short QualityScore { get; init; }

        public PlaceCardDto ToCard(string language)
        {
            var isEnglish = language.StartsWith("en", StringComparison.OrdinalIgnoreCase);

            return new PlaceCardDto
            {
                Id = Id,
                Name = Name,
                Slug = Slug,
                CategoryKey = CategoryKey,
                CategoryName = isEnglish ? CategoryNameEn : CategoryNameTr,
                CategoryIcon = CategoryIcon,
                PhotoUrl = PhotoUrl,
                PhotoAttribution = Attribution.ForPhoto(PhotoAuthor, PhotoLicense)!,
                PhotoSource = PhotoSource,
                Description = isEnglish ? DescriptionEn ?? DescriptionTr : DescriptionTr,
                CityName = CityName,
                DistrictName = DistrictName,
                Latitude = Location.Y,
                Longitude = Location.X,
                AverageVisitMinutes = AverageVisitMinutes,
                QualityScore = QualityScore
            };
        }
    }
}
