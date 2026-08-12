using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Yolla.Application.Common;
using Yolla.Application.Discovery;
using Yolla.Application.Places;
using Yolla.Domain.Entities;
using Yolla.Infrastructure.Persistence;

namespace Yolla.Infrastructure.Services;

/// <inheritdoc cref="IPlaceService"/>
public sealed class PlaceService(YollaDbContext context) : IPlaceService
{
    private const int NearbyRadiusMeters = 3000;
    private const int NearbyLimit = 6;
    private const int MaxNearbyRadiusMeters = 20_000;
    private const int MaxNearbyTake = 50;

    public async Task<PlaceDetailDto> GetByIdAsync(
        int placeId,
        string language = "tr",
        CancellationToken cancellationToken = default)
    {
        var place = await LoadAsync(x => x.Id == placeId, cancellationToken)
                    ?? throw new NotFoundException("Yer", placeId);

        return await BuildDetailAsync(place, language, cancellationToken);
    }

    public async Task<PlaceDetailDto> GetBySlugAsync(
        string citySlug,
        string placeSlug,
        string language = "tr",
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(citySlug);
        ArgumentException.ThrowIfNullOrWhiteSpace(placeSlug);

        var place = await LoadAsync(
                        x => x.Slug == placeSlug && x.City.Slug == citySlug, cancellationToken)
                    ?? throw new NotFoundException("Yer", $"{citySlug}/{placeSlug}");

        return await BuildDetailAsync(place, language, cancellationToken);
    }

    public async Task<IReadOnlyList<PlaceCardDto>> GetNearbyCardsAsync(
        double latitude,
        double longitude,
        int radiusMeters = 3000,
        int take = 20,
        string language = "tr",
        CancellationToken cancellationToken = default)
    {
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            throw RequestValidationException.Single("coordinates", "Geçersiz koordinat.");
        }

        var radius = Math.Clamp(radiusMeters, 100, MaxNearbyRadiusMeters);
        var limit = Math.Clamp(take, 1, MaxNearbyTake);
        var isEnglish = IsEnglish(language);

        var origin = new Point(longitude, latitude) { SRID = 4326 };

        var rows = await context.Places
            .AsNoTracking()
            .Where(x => x.IsActive
                        && x.Category.IsVisible
                        && x.PhotoUrl != null
                        && x.PhotoAuthor != null
                        && x.PhotoLicense != null
                        && x.QualityScore >= PlaceQualityScorer.FeedThreshold
                        && x.Location.IsWithinDistance(origin, radius))
            .OrderBy(x => x.Location.Distance(origin))
            .Take(limit)
            .Select(x => new PlaceRow
            {
                Id = x.Id,
                Name = x.Name,
                Slug = x.Slug,
                CategoryKey = x.Category.Key,
                CategoryNameTr = x.Category.NameTr,
                CategoryNameEn = x.Category.NameEn,
                CategoryIcon = x.Category.Icon,
                PhotoUrl = x.PhotoUrl,
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

        return rows.Select(x => x.ToCard(isEnglish)).ToList();
    }

    private async Task<Place?> LoadAsync(
        System.Linq.Expressions.Expression<Func<Place, bool>> predicate,
        CancellationToken cancellationToken) =>
        await context.Places
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.City)
            .Include(x => x.District)
            .Where(x => x.IsActive)
            .FirstOrDefaultAsync(predicate, cancellationToken);

    private async Task<PlaceDetailDto> BuildDetailAsync(
        Place place,
        string language,
        CancellationToken cancellationToken)
    {
        var isEnglish = IsEnglish(language);

        var nearby = await context.Places
            .AsNoTracking()
            .Where(x => x.Id != place.Id
                        && x.IsActive
                        && x.Category.IsVisible
                        && x.PhotoUrl != null
                        && x.PhotoAuthor != null
                        && x.PhotoLicense != null
                        && x.Location.IsWithinDistance(place.Location, NearbyRadiusMeters))
            .OrderBy(x => x.Location.Distance(place.Location))
            .Take(NearbyLimit)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Slug,
                CategoryNameTr = x.Category.NameTr,
                CategoryNameEn = x.Category.NameEn,
                x.PhotoUrl,
                Distance = x.Location.Distance(place.Location)
            })
            .ToListAsync(cancellationToken);

        return new PlaceDetailDto
        {
            Id = place.Id,
            Name = place.Name,
            NameEn = place.NameEn,
            Slug = place.Slug,
            CategoryKey = place.Category.Key,
            CategoryName = isEnglish ? place.Category.NameEn : place.Category.NameTr,
            CategoryIcon = place.Category.Icon,
            Description = isEnglish
                ? place.DescriptionEn ?? place.DescriptionTr
                : place.DescriptionTr,
            PhotoUrl = place.PhotoUrl,
            PhotoAttribution = Attribution.ForPhoto(place.PhotoAuthor, place.PhotoLicense),
            PhotoSource = place.PhotoSource,
            Latitude = place.Location.Y,
            Longitude = place.Location.X,
            CityName = place.City.Name,
            CitySlug = place.City.Slug,
            DistrictName = place.District?.Name,
            Address = place.Address,
            Website = place.Website,
            OpeningHours = place.OpeningHours,
            WikipediaUrl = BuildWikipediaUrl(place.WikipediaTitle, isEnglish),
            AverageVisitMinutes = place.AvgVisitMinutes,
            QualityScore = place.QualityScore,
            DirectionsUrl = BuildDirectionsUrl(place.Location.Y, place.Location.X),
            Nearby = nearby.Select(x => new NearbyPlaceDto
            {
                Id = x.Id,
                Name = x.Name,
                Slug = x.Slug,
                CategoryName = isEnglish ? x.CategoryNameEn : x.CategoryNameTr,
                PhotoUrl = x.PhotoUrl,
                DistanceMeters = (int)Math.Round(x.Distance)
            }).ToList()
        };
    }

    /// <summary>
    /// Harita uygulamasında yol tarifi bağlantısı üretir.
    /// </summary>
    /// <remarks>
    /// Bağlantı vermek ücretsizdir ve dış servis şartlarına uygundur; veriyi kendi
    /// tarafımızda saklamak ya da kendi haritamızda göstermek olmazdı.
    /// </remarks>
    internal static string BuildDirectionsUrl(double latitude, double longitude) =>
        FormattableString.Invariant(
            $"https://www.google.com/maps/dir/?api=1&destination={latitude},{longitude}");

    internal static string? BuildWikipediaUrl(string? title, bool isEnglish)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var language = isEnglish ? "en" : "tr";

        return $"https://{language}.wikipedia.org/wiki/{Uri.EscapeDataString(title.Replace(' ', '_'))}";
    }

    private static bool IsEnglish(string language) =>
        language.StartsWith("en", StringComparison.OrdinalIgnoreCase);

    /// <summary>Kart üretmek için ortak ara temsil.</summary>
    private sealed record PlaceRow
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
        public required string Slug { get; init; }
        public required string CategoryKey { get; init; }
        public required string CategoryNameTr { get; init; }
        public required string CategoryNameEn { get; init; }
        public string? CategoryIcon { get; init; }
        public string? PhotoUrl { get; init; }
        public string? PhotoAuthor { get; init; }
        public string? PhotoLicense { get; init; }
        public string? PhotoSource { get; init; }
        public string? DescriptionTr { get; init; }
        public string? DescriptionEn { get; init; }
        public required string CityName { get; init; }
        public string? DistrictName { get; init; }
        public required Point Location { get; init; }
        public short? AverageVisitMinutes { get; init; }
        public required short QualityScore { get; init; }

        public PlaceCardDto ToCard(bool isEnglish) => new()
        {
            Id = Id,
            Name = Name,
            Slug = Slug,
            CategoryKey = CategoryKey,
            CategoryName = isEnglish ? CategoryNameEn : CategoryNameTr,
            CategoryIcon = CategoryIcon,
            PhotoUrl = PhotoUrl ?? string.Empty,
            PhotoAttribution = Attribution.ForPhoto(PhotoAuthor, PhotoLicense) ?? string.Empty,
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
