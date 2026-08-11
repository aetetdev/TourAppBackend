using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Yolla.Application.Common;
using Yolla.Application.Geo;
using Yolla.Application.Places;
using Yolla.Domain.Entities;
using Yolla.Infrastructure.Persistence;

namespace Yolla.Infrastructure.Services;

/// <inheritdoc cref="IGeoService"/>
public sealed class GeoService(YollaDbContext context) : IGeoService
{
    /// <summary>
    /// Kart olarak gösterilebilir yer koşulu: fotoğrafı, atıf bilgisi ve yeterli puanı olan,
    /// görünür kategorideki kayıtlar. Atıf bilgisi eksik fotoğraf gösterilemez.
    /// </summary>
    private static readonly Expression<Func<Place, bool>> IsReady = place =>
        place.IsActive
        && place.Category.IsVisible
        && place.PhotoUrl != null
        && place.PhotoAuthor != null
        && place.PhotoLicense != null
        && place.QualityScore >= PlaceQualityScorer.FeedThreshold;

    public async Task<IReadOnlyList<CountryDto>> GetCountriesAsync(
        CancellationToken cancellationToken = default)
    {
        var countries = await context.Countries
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.NameTr)
            .Select(x => new { x.Id, x.Iso2, x.NameTr })
            .ToListAsync(cancellationToken);

        // Sayımlar tek sorguda toplanıyor; ülke başına alt sorgu açılmıyor
        var counts = await context.Places
            .AsNoTracking()
            .Where(IsReady)
            .GroupBy(x => x.CountryId)
            .Select(g => new { CountryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CountryId, x => x.Count, cancellationToken);

        return countries
            .Select(x => new CountryDto
            {
                Id = x.Id,
                Iso2 = x.Iso2,
                Name = x.NameTr,
                ReadyPlaceCount = counts.GetValueOrDefault(x.Id)
            })
            .ToList();
    }

    public async Task<IReadOnlyList<CityDto>> GetCitiesAsync(
        string countryIso2,
        string? search = null,
        bool onlyWithContent = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(countryIso2);

        var iso2 = countryIso2.ToUpperInvariant();

        var query = context.Cities
            .AsNoTracking()
            .Where(x => x.IsActive && x.Country.Iso2 == iso2);

        if (!string.IsNullOrWhiteSpace(search))
        {
            // Arama alanı sadeleştirilmiş biçimde saklanıyor: "İzmir" araması "izmir" ile eşleşir
            var normalized = TextNormalizer.Normalize(search);

            query = query.Where(x => x.NameNormalized.Contains(normalized));
        }

        var cities = await query
            .OrderBy(x => x.Name)
            .Select(x => new CityRow(x.Id, x.Name, x.Slug, x.Center))
            .ToListAsync(cancellationToken);

        var counts = await GetReadyCountsAsync(iso2, cancellationToken);

        var result = cities
            .Select(x => x.ToDto(counts.GetValueOrDefault(x.Id)))
            .ToList();

        return onlyWithContent
            ? result.Where(x => x.ReadyPlaceCount > 0).ToList()
            : result;
    }

    public async Task<CityDto> GetCityAsync(int cityId, CancellationToken cancellationToken = default)
    {
        var city = await context.Cities
            .AsNoTracking()
            .Where(x => x.Id == cityId && x.IsActive)
            .Select(x => new CityRow(x.Id, x.Name, x.Slug, x.Center))
            .FirstOrDefaultAsync(cancellationToken);

        if (city is null)
        {
            throw new NotFoundException("Şehir", cityId);
        }

        return city.ToDto(await CountReadyAsync(city.Id, cancellationToken));
    }

    public async Task<CityDto> GetCityBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        var city = await context.Cities
            .AsNoTracking()
            .Where(x => x.Slug == slug && x.IsActive)
            .Select(x => new CityRow(x.Id, x.Name, x.Slug, x.Center))
            .FirstOrDefaultAsync(cancellationToken);

        if (city is null)
        {
            throw new NotFoundException("Şehir", slug);
        }

        return city.ToDto(await CountReadyAsync(city.Id, cancellationToken));
    }

    private async Task<Dictionary<int, int>> GetReadyCountsAsync(
        string iso2,
        CancellationToken cancellationToken) =>
        await context.Places
            .AsNoTracking()
            .Where(IsReady)
            .Where(x => x.Country.Iso2 == iso2)
            .GroupBy(x => x.CityId)
            .Select(g => new { CityId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CityId, x => x.Count, cancellationToken);

    private async Task<int> CountReadyAsync(int cityId, CancellationToken cancellationToken) =>
        await context.Places
            .AsNoTracking()
            .Where(IsReady)
            .CountAsync(x => x.CityId == cityId, cancellationToken);

    /// <remarks>
    /// Koordinat, nokta nesnesi olarak çekilip bellekte açılıyor. PostGIS'te ST_X/ST_Y
    /// yalnızca <c>geometry</c> üzerinde tanımlı; kolonlarımız <c>geography</c> olduğu için
    /// sorgu içinde koordinat okumak dönüştürme gerektirirdi.
    /// </remarks>
    private sealed record CityRow(int Id, string Name, string Slug, Point Center)
    {
        public CityDto ToDto(int readyPlaceCount) => new()
        {
            Id = Id,
            Name = Name,
            Slug = Slug,
            Latitude = Center.Y,
            Longitude = Center.X,
            ReadyPlaceCount = readyPlaceCount
        };
    }
}
