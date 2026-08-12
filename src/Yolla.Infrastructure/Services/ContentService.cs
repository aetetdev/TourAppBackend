using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Yolla.Application.Common;
using Yolla.Application.Content;
using Yolla.Application.Places;
using Yolla.Domain.Entities;
using Yolla.Domain.Enums;
using Yolla.Infrastructure.Persistence;

namespace Yolla.Infrastructure.Services;

/// <inheritdoc cref="IContentService"/>
public sealed class ContentService(YollaDbContext context) : IContentService
{
    /// <summary>Kendi ürettiğimiz içeriğin lisans etiketi.</summary>
    public const string OwnLicense = "Yolla";

    private const int MaxTake = 200;

    public async Task<ContributionDto> SubmitAsync(
        int placeId,
        ContributionRequest request,
        string? submittedBy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var place = await context.Places
                        .Include(x => x.Category)
                        .FirstOrDefaultAsync(x => x.Id == placeId, cancellationToken)
                    ?? throw new NotFoundException("Yer", placeId);

        Validate(request);

        var language = NormalizeLanguage(request.Language);

        var existing = await context.PlaceContributions
            .FirstOrDefaultAsync(
                x => x.PlaceId == placeId && x.Type == request.Type && x.Language == language,
                cancellationToken);

        if (existing is null)
        {
            existing = new PlaceContribution
            {
                PlaceId = placeId,
                Type = request.Type,
                Language = language
            };

            context.PlaceContributions.Add(existing);
        }

        existing.Value = request.Value.Trim();
        existing.Author = request.Author?.Trim();
        existing.License = string.IsNullOrWhiteSpace(request.License) ? OwnLicense : request.License.Trim();
        existing.SourceUrl = request.SourceUrl?.Trim();
        existing.SubmittedBy = submittedBy;
        existing.Note = request.Note?.Trim();
        existing.IsPublished = true;

        ApplyToPlace(place, existing);

        await context.SaveChangesAsync(cancellationToken);

        return ToDto(existing, place.Name);
    }

    public async Task<IReadOnlyList<ContributionDto>> GetForPlaceAsync(
        int placeId,
        CancellationToken cancellationToken = default)
    {
        var rows = await context.PlaceContributions
            .AsNoTracking()
            .Where(x => x.PlaceId == placeId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new { Contribution = x, PlaceName = x.Place.Name })
            .ToListAsync(cancellationToken);

        return rows.Select(x => ToDto(x.Contribution, x.PlaceName)).ToList();
    }

    public async Task UnpublishAsync(int contributionId, CancellationToken cancellationToken = default)
    {
        var contribution = await context.PlaceContributions
                               .Include(x => x.Place)
                               .FirstOrDefaultAsync(x => x.Id == contributionId, cancellationToken)
                           ?? throw new NotFoundException("Katkı", contributionId);

        contribution.IsPublished = false;

        // Yerin içeriği otomatik kaynaklara döner; elle girilen değer temizlenir
        RevertFromPlace(contribution.Place, contribution);

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MissingContentDto>> GetMissingContentAsync(
        string? citySlug = null,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var limit = Math.Clamp(take, 1, MaxTake);

        var query = context.Places
            .AsNoTracking()
            .Where(x => x.IsActive && x.Category.IsVisible)
            // Fotoğrafı olmayan ya da atıf bilgisi eksik olduğu için gösterilemeyen kayıtlar
            .Where(x => x.PhotoUrl == null || x.PhotoAuthor == null || x.PhotoLicense == null);

        if (!string.IsNullOrWhiteSpace(citySlug))
        {
            query = query.Where(x => x.City.Slug == citySlug);
        }

        var rows = await query
            // En yüksek puanlılar önce: bir fotoğraf eklendiğinde doğrudan kart destesine girerler
            .OrderByDescending(x => x.QualityScore)
            .ThenByDescending(x => x.WikidataId != null)
            .Take(limit)
            .Select(x => new
            {
                x.Id,
                x.Name,
                CityName = x.City.Name,
                CategoryName = x.Category.NameTr,
                x.QualityScore,
                HasPhoto = x.PhotoUrl != null,
                HasDescription = x.DescriptionTr != null,
                x.WikidataId,
                x.Location
            })
            .ToListAsync(cancellationToken);

        return rows.Select(x => new MissingContentDto
        {
            Id = x.Id,
            Name = x.Name,
            CityName = x.CityName,
            CategoryName = x.CategoryName,
            QualityScore = x.QualityScore,
            HasPhoto = x.HasPhoto,
            HasDescription = x.HasDescription,
            WikidataId = x.WikidataId,
            Latitude = x.Location.Y,
            Longitude = x.Location.X
        }).ToList();
    }

    /// <summary>
    /// Katkıyı yerin gösterilen alanlarına yansıtır ve kalite puanını yeniden hesaplar.
    /// </summary>
    private static void ApplyToPlace(Place place, PlaceContribution contribution)
    {
        switch (contribution.Type)
        {
            case ContributionType.Photo:
                place.PhotoUrl = contribution.Value;
                place.PhotoAuthor = contribution.Author ?? OwnLicense;
                place.PhotoLicense = contribution.License ?? OwnLicense;
                place.PhotoSource = contribution.SourceUrl;
                break;

            case ContributionType.Description:
                if (contribution.Language == "en")
                {
                    place.DescriptionEn = contribution.Value;
                }
                else
                {
                    place.DescriptionTr = contribution.Value;
                }

                break;

            case ContributionType.VisitDuration:
                if (short.TryParse(contribution.Value, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out var minutes))
                {
                    place.AvgVisitMinutes = minutes;
                }

                break;
        }

        Rescore(place);
    }

    private static void RevertFromPlace(Place place, PlaceContribution contribution)
    {
        switch (contribution.Type)
        {
            case ContributionType.Photo when place.PhotoUrl == contribution.Value:
                place.PhotoUrl = null;
                place.PhotoAuthor = null;
                place.PhotoLicense = null;
                place.PhotoSource = null;
                break;

            case ContributionType.Description when contribution.Language == "en":
                place.DescriptionEn = null;
                break;

            case ContributionType.Description:
                place.DescriptionTr = null;
                break;

            case ContributionType.VisitDuration:
                place.AvgVisitMinutes = null;
                break;
        }

        Rescore(place);
    }

    private static void Rescore(Place place) =>
        place.QualityScore = PlaceQualityScorer.Score(new PlaceQualityInput
        {
            Name = place.Name,
            HasWikidata = place.WikidataId is not null,
            HasPhoto = place.PhotoUrl is not null,
            HasWikipedia = place.WikipediaTitle is not null,
            HasDescription = place.DescriptionTr is not null,
            HasNameEn = place.NameEn is not null,
            HasWebsite = place.Website is not null,
            HasOpeningHours = place.OpeningHours is not null,
            CategoryWeight = place.Category?.Weight ?? 0
        });

    private static void Validate(ContributionRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Value))
        {
            errors[nameof(request.Value)] = ["İçerik boş olamaz."];
        }

        switch (request.Type)
        {
            case ContributionType.Photo when !IsValidPhotoUrl(request.Value):
                errors[nameof(request.Value)] = ["Fotoğraf adresi http veya https ile başlamalı."];
                break;

            case ContributionType.Description when request.Value?.Length > 4000:
                errors[nameof(request.Value)] = ["Açıklama en fazla 4000 karakter olabilir."];
                break;

            case ContributionType.VisitDuration
                when !short.TryParse(request.Value, NumberStyles.Integer,
                         CultureInfo.InvariantCulture, out var minutes) || minutes is < 1 or > 1440:
                errors[nameof(request.Value)] = ["Gezme süresi 1-1440 dakika arasında bir sayı olmalı."];
                break;
        }

        // Başkasının içeriği kullanılıyorsa lisans zorunlu: atıfsız görsel yayınlamak
        // telif ihlali olur
        var isOwnContent = string.IsNullOrWhiteSpace(request.License)
                           || string.Equals(request.License, OwnLicense, StringComparison.OrdinalIgnoreCase);

        if (request.Type == ContributionType.Photo && !isOwnContent && string.IsNullOrWhiteSpace(request.Author))
        {
            errors[nameof(request.Author)] =
                ["Başkasına ait fotoğraflarda fotoğrafçı adı zorunludur."];
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }
    }

    private static bool IsValidPhotoUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https";

    private static string NormalizeLanguage(string? language) =>
        language?.Trim().ToLowerInvariant() is "en" ? "en" : "tr";

    private static ContributionDto ToDto(PlaceContribution contribution, string placeName) => new()
    {
        Id = contribution.Id,
        PlaceId = contribution.PlaceId,
        PlaceName = placeName,
        Type = contribution.Type,
        Value = contribution.Value,
        Language = contribution.Language,
        Author = contribution.Author,
        License = contribution.License,
        SourceUrl = contribution.SourceUrl,
        IsPublished = contribution.IsPublished,
        CreatedAt = contribution.CreatedAt
    };
}
