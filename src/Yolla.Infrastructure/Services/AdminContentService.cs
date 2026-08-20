using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Yolla.Application.Admin;
using Yolla.Application.Common;
using Yolla.Application.Rewards;
using Yolla.Domain.Entities;
using Yolla.Domain.Enums;
using Yolla.Infrastructure.Persistence;

namespace Yolla.Infrastructure.Services;

/// <inheritdoc cref="IAdminContentService"/>
public sealed class AdminContentService(YollaDbContext context, IPhotoStorage storage)
    : IAdminContentService
{
    /// <summary>
    /// Ekibin eklediği yerlerin sahte OSM kimliği tabanı.
    /// </summary>
    /// <remarks>
    /// Kullanıcı önerilerinden ayrı bir aralık: kaydın nereden geldiği
    /// kimliğine bakınca anlaşılıyor. İkisi de negatif, gerçek OSM
    /// kimlikleri pozitif olduğu için çakışma yok.
    /// </remarks>
    private const long EditorOsmIdBase = -2_000_000L;

    /// <summary>Ekibin eklediği yerin kalite puanı.</summary>
    /// <remarks>
    /// Kullanıcı önerisinden yüksek (30): ekip kaydı doğrulayarak giriyor.
    /// Fotoğraf da aynı anda eklenirse kart destesine girebiliyor.
    /// </remarks>
    private const short EditorQualityScore = 55;

    public async Task<IReadOnlyList<MissingPhotoDto>> GetPlacesMissingPhotoAsync(
        int? cityId = null,
        string? search = null,
        int take = 30,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 100);

        var sorgu = context.Places
            .AsNoTracking()
            .Where(x => x.IsActive && x.PhotoUrl == null);

        if (cityId is { } sehir)
        {
            sorgu = sorgu.Where(x => x.CityId == sehir);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var arama = search.Trim();
            sorgu = sorgu.Where(x => EF.Functions.ILike(x.Name, $"%{arama}%"));
        }

        var satirlar = await sorgu
            // Kaliteli olan önce: bir fotoğraf eklendiğinde doğrudan kart
            // destesine giren kayıtlar ekibin zamanını en iyi değerlendiren
            // kayıtlar.
            .OrderByDescending(x => x.QualityScore)
            .Take(take)
            .Select(x => new
            {
                x.Id,
                x.Name,
                CityName = x.City.Name,
                DistrictName = x.District != null ? x.District.Name : null,
                CategoryName = x.Category.NameTr,
                x.QualityScore,
                HasDescription = x.DescriptionTr != null,
                x.Location,
                x.WikidataId
            })
            .ToListAsync(cancellationToken);

        return satirlar
            .Select(x => new MissingPhotoDto
            {
                PlaceId = x.Id,
                Name = x.Name,
                CityName = x.CityName,
                DistrictName = x.DistrictName,
                CategoryName = x.CategoryName,
                QualityScore = x.QualityScore,
                HasDescription = x.HasDescription,
                Latitude = x.Location.Y,
                Longitude = x.Location.X,
                WikidataId = x.WikidataId
            })
            .ToList();
    }

    public async Task AddPhotoAsync(
        int placeId,
        AddPhotoRequest request,
        Stream content,
        int editorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var photographer = (request.PhotographerName ?? string.Empty).Trim();

        if (photographer.Length == 0)
        {
            throw RequestValidationException.Single(
                nameof(request.PhotographerName),
                "Fotoğrafı çekenin adı gerekli; görselin yanında atıf olarak gösteriliyor.");
        }

        var place = await context.Places
            .Include(x => x.Category)
            .FirstOrDefaultAsync(x => x.Id == placeId && x.IsActive, cancellationToken)
            ?? throw new NotFoundException("Yer", placeId);

        using var image = await PhotoNormalizer.LoadAsync(content, cancellationToken);
        using var buffer = await PhotoNormalizer.ToJpegAsync(image, cancellationToken);

        var relativePath = $"editorial/{place.Id}/{Guid.NewGuid():N}.jpg";
        await storage.SaveAsync(relativePath, buffer, cancellationToken);

        // Doğrudan yayına giriyor: ekip zaten moderasyonun kendisi, kendi
        // eklediğini kendine onaylatması anlamsız.
        var contribution = new PlaceContribution
        {
            PlaceId = place.Id,
            Type = ContributionType.Photo,
            Value = storage.UrlFor(relativePath),
            Author = photographer,
            License = string.IsNullOrWhiteSpace(request.License)
                ? ContributionApplier.OwnLicense
                : request.License.Trim(),
            SourceUrl = string.IsNullOrWhiteSpace(request.SourceUrl) ? null : request.SourceUrl.Trim(),
            SubmittedBy = $"editör #{editorUserId}",
            IsPublished = true
        };

        context.PlaceContributions.Add(contribution);

        // Katkı yere işleniyor ve kalite puanı yeniden hesaplanıyor: yalnızca
        // katkı satırı yazmak fotoğrafı hiçbir ekranda göstermiyor.
        ContributionApplier.Apply(place, contribution);

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<CreatedPlaceDto> CreatePlaceAsync(
        CreatePlaceRequest request,
        int editorUserId,
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

        var category = await context.Categories
            .FirstOrDefaultAsync(x => x.Key == request.CategoryKey, cancellationToken)
            ?? throw RequestValidationException.Single(
                nameof(request.CategoryKey), "Böyle bir kategori yok.");

        var location = new Point(request.Longitude, request.Latitude) { SRID = 4326 };

        var city = await context.Cities
            .Where(x => x.Boundary != null && x.Boundary.Contains(location))
            .Select(x => new { x.Id, x.Name, x.CountryId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw RequestValidationException.Single(
                nameof(request.Latitude),
                "Bu konum Türkiye sınırlarının dışında görünüyor.");

        var districtId = await context.Districts
            .Where(x => x.CityId == city.Id && x.Boundary != null && x.Boundary.Contains(location))
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var place = new Place
        {
            CountryId = city.CountryId,
            CityId = city.Id,
            DistrictId = districtId,
            CategoryId = category.Id,
            OsmType = OsmElementType.Node,
            // Kimlik kaydedildikten sonra belli olduğu için önce geçici bir
            // değer veriliyor; benzersizlik indeksi boş geçmeye izin vermiyor.
            OsmId = EditorOsmIdBase - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Name = name,
            Slug = TextNormalizer.SlugifyWithSuffix(name, city.Name),
            Location = location,
            Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            DescriptionTr = string.IsNullOrWhiteSpace(request.Description)
                ? null
                : request.Description.Trim(),
            QualityScore = EditorQualityScore,
            IsActive = true
        };

        context.Places.Add(place);
        await context.SaveChangesAsync(cancellationToken);

        return new CreatedPlaceDto
        {
            PlaceId = place.Id,
            Name = place.Name,
            CityName = city.Name,
            Slug = place.Slug
        };
    }
}
