using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Yolla.Domain.Entities;
using Yolla.Domain.Enums;

namespace Yolla.Api.IntegrationTests;

/// <summary>
/// Testler için ortak veri kurulumu.
/// </summary>
/// <remarks>
/// Her test sınıfı kendi coğrafi bölgesinde çalışır; aynı alanı paylaşsalardı bir testin
/// noktası diğerinin şehir sınırına düşer ve içe aktarma yanlış şehre bağlardı.
/// </remarks>
public static class TestData
{
    public static readonly GeometryFactory Factory = new(new PrecisionModel(), 4326);

    /// <summary>Verilen bölgede bir şehir ve içinde yerler oluşturur.</summary>
    public static async Task<SeededCity> EnsureCityWithPlacesAsync(
        PostgisFixture fixture,
        string slug,
        double originLongitude,
        int placeCount = 6,
        string categoryKey = "museum")
    {
        await using var context = fixture.CreateDbContext();

        var city = await context.Cities.FirstOrDefaultAsync(x => x.Slug == slug);

        if (city is null)
        {
            var ring = Factory.CreateLinearRing(
            [
                new Coordinate(originLongitude, 0),
                new Coordinate(originLongitude, 2),
                new Coordinate(originLongitude + 2, 2),
                new Coordinate(originLongitude + 2, 0),
                new Coordinate(originLongitude, 0)
            ]);

            city = new City
            {
                CountryId = 1,
                Name = $"Test {slug}",
                NameNormalized = $"test {slug}",
                Slug = slug,
                Center = Factory.CreatePoint(new Coordinate(originLongitude + 1, 1)),
                Boundary = Factory.CreatePolygon(ring),
                IsActive = true
            };

            context.Cities.Add(city);
            await context.SaveChangesAsync();

            var categoryId = await context.Categories
                .Where(x => x.Key == categoryKey)
                .Select(x => x.Id)
                .FirstAsync();

            for (var i = 0; i < placeCount; i++)
            {
                // Yerler birbirinden ayrı: rota hesabının anlamlı olması için
                context.Places.Add(new Place
                {
                    CountryId = 1,
                    CityId = city.Id,
                    CategoryId = categoryId,
                    OsmType = OsmElementType.Node,
                    OsmId = Math.Abs(slug.GetHashCode()) % 100000 * 100L + i,
                    Name = $"{slug} Yeri {i}",
                    Slug = $"{slug}-yeri-{i}",
                    // Yaklaşık 1 km aralıklarla: şehir içi yerler gerçekte de bu ölçekte
                    // birbirine yakın, "yakındakiler" sorgusu ancak böyle anlamlı olur
                    Location = Factory.CreatePoint(
                        new Coordinate(originLongitude + 0.5 + i * 0.01, 1 + i * 0.004)),
                    PhotoUrl = "https://upload.wikimedia.org/test.jpg",
                    PhotoAuthor = "Test Fotoğrafçı",
                    PhotoLicense = "CC BY-SA 4.0",
                    DescriptionTr = "Test açıklaması.",
                    Address = "Test Caddesi No:1",
                    Website = "https://example.com",
                    OpeningHours = "Tu-Su 09:00-17:00",
                    WikipediaTitle = i == 0 ? "Test Makalesi" : null,
                    AvgVisitMinutes = 30,
                    QualityScore = (short)(90 - i),
                    IsActive = true
                });
            }

            await context.SaveChangesAsync();
        }

        var placeIds = await context.Places
            .Where(x => x.CityId == city.Id)
            .OrderByDescending(x => x.QualityScore)
            .Select(x => x.Id)
            .ToListAsync();

        return new SeededCity(city.Id, city.Slug, placeIds);
    }

    public sealed record SeededCity(int CityId, string CitySlug, List<int> PlaceIds);
}
