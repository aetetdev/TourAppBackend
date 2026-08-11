using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Shouldly;
using Yolla.Domain.Entities;
using Yolla.Domain.Enums;

namespace Yolla.Api.IntegrationTests;

[Collection(PostgisCollection.Name)]
public class DiscoveryEndpointTests(PostgisFixture fixture) : IAsyncLifetime
{
    private static readonly GeometryFactory Factory = new(new PrecisionModel(), 4326);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private ApiFactory _api = null!;
    private HttpClient _client = null!;
    private int _cityId;

    public async Task InitializeAsync()
    {
        _api = new ApiFactory(fixture.ConnectionString);
        _client = _api.CreateClient();
        _cityId = await SeedAsync();
    }

    [Fact]
    public async Task Sehir_feedi_kart_dondurur()
    {
        var page = await GetFeedAsync($"/api/v1/discovery/city/{_cityId}/feed?take=10");

        page.Items.ShouldNotBeEmpty();
        page.Items.ShouldAllBe(x => !string.IsNullOrWhiteSpace(x.PhotoUrl));
    }

    [Fact]
    public async Task Fotografsiz_yer_kart_olarak_donmez()
    {
        var page = await GetFeedAsync($"/api/v1/discovery/city/{_cityId}/feed?take=50");

        page.Items.ShouldNotContain(x => x.Name == "Fotografsiz Yer");
    }

    [Fact]
    public async Task Atif_bilgisi_eksik_fotograf_gosterilmez()
    {
        // Wikimedia görsellerinin çoğu CC BY-SA; fotoğrafçı adı olmadan gösterim lisans ihlali
        var page = await GetFeedAsync($"/api/v1/discovery/city/{_cityId}/feed?take=50");

        page.Items.ShouldNotContain(x => x.Name == "Atifsiz Yer");
        page.Items.ShouldAllBe(x => !string.IsNullOrWhiteSpace(x.PhotoAttribution));
    }

    [Fact]
    public async Task Feed_esiginin_altindaki_yer_gosterilmez()
    {
        var page = await GetFeedAsync($"/api/v1/discovery/city/{_cityId}/feed?take=50");

        page.Items.ShouldNotContain(x => x.Name == "Dusuk Puanli Yer");
    }

    [Fact]
    public async Task Kartlar_kalite_puanina_gore_siralanir()
    {
        var page = await GetFeedAsync($"/api/v1/discovery/city/{_cityId}/feed?take=50");

        // Çeşitlendirme eşit puanlıların sırasını değiştirebilir; genel eğilim azalan olmalı
        page.Items.First().QualityScore.ShouldBeGreaterThanOrEqualTo(page.Items.Last().QualityScore);
    }

    [Fact]
    public async Task Kategori_cesitliligi_varken_ayni_turden_kartlar_yiginmaz()
    {
        // Sondaki kartlar dışarıda bırakılıyor: listenin sonunda tek kategoriden kayıt
        // kaldığında serpiştirilecek başka kart olmuyor, bu kaçınılmaz
        var page = await GetFeedAsync($"/api/v1/discovery/city/{_cityId}/feed?take=10");

        var consecutive = 1;

        for (var i = 1; i < page.Items.Count; i++)
        {
            consecutive = page.Items[i].CategoryKey == page.Items[i - 1].CategoryKey
                ? consecutive + 1
                : 1;

            consecutive.ShouldBeLessThanOrEqualTo(2);
        }
    }

    [Fact]
    public async Task Cok_sayida_ayni_kategori_olsa_da_ilk_kartlar_cesitli_gelir()
    {
        // Ham puan sıralamasında ilk üç kart da cami olurdu (90, 89, 88);
        // çeşitlendirme araya farklı kategori sokmalı
        var page = await GetFeedAsync($"/api/v1/discovery/city/{_cityId}/feed?take=6");

        page.Items.Select(x => x.CategoryKey).Distinct().Count().ShouldBeGreaterThan(1);
    }

    [Fact]
    public async Task Kategori_suzgeci_uygulanir()
    {
        var page = await GetFeedAsync($"/api/v1/discovery/city/{_cityId}/feed?take=50&categories=museum");

        page.Items.ShouldNotBeEmpty();
        page.Items.ShouldAllBe(x => x.CategoryKey == "museum");
    }

    [Fact]
    public async Task Imlec_ile_sonraki_sayfa_alinir()
    {
        var first = await GetFeedAsync($"/api/v1/discovery/city/{_cityId}/feed?take=3");

        first.HasMore.ShouldBeTrue();
        first.NextCursor.ShouldNotBeNull();

        var second = await GetFeedAsync(
            $"/api/v1/discovery/city/{_cityId}/feed?take=3&cursor={Uri.EscapeDataString(first.NextCursor)}");

        // İki sayfada aynı kayıt tekrarlamamalı
        var firstIds = first.Items.Select(x => x.Id).ToHashSet();
        second.Items.ShouldAllBe(x => !firstIds.Contains(x.Id));
    }

    [Fact]
    public async Task Bozuk_imlec_ilk_sayfayi_dondurur()
    {
        var page = await GetFeedAsync($"/api/v1/discovery/city/{_cityId}/feed?cursor=bozuk-deger");

        page.Items.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Olmayan_sehir_icin_404_doner()
    {
        var response = await _client.GetAsync("/api/v1/discovery/city/999999/feed");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("title").GetString().ShouldBe("Kayıt bulunamadı");
    }

    [Fact]
    public async Task Yanit_zorunlu_atiflari_tasir()
    {
        var response = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/discovery/city/{_cityId}/feed?take=5");

        var attributions = response.GetProperty("attributions")
            .EnumerateArray()
            .Select(x => x.GetString())
            .ToList();

        attributions.ShouldContain("© OpenStreetMap katkıcıları");
    }

    private async Task<FeedPage> GetFeedAsync(string url)
    {
        var response = await _client.GetAsync(url);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        return json.GetProperty("data").Deserialize<FeedPage>(JsonOptions)!;
    }

    private async Task<int> SeedAsync()
    {
        await using var context = fixture.CreateDbContext();

        const string slug = "test-feed-sehri";
        var existing = await context.Cities.FirstOrDefaultAsync(x => x.Slug == slug);

        if (existing is not null)
        {
            return existing.Id;
        }

        var ring = Factory.CreateLinearRing(
        [
            new Coordinate(100, 0), new Coordinate(100, 2),
            new Coordinate(102, 2), new Coordinate(102, 0), new Coordinate(100, 0)
        ]);

        var city = new City
        {
            CountryId = 1,
            Name = "Test Feed Şehri",
            NameNormalized = "test feed sehri",
            Slug = slug,
            Center = Factory.CreatePoint(new Coordinate(101, 1)),
            Boundary = Factory.CreatePolygon(ring),
            IsActive = true
        };

        context.Cities.Add(city);
        await context.SaveChangesAsync();

        var categories = await context.Categories
            .Where(x => x.Key == "museum" || x.Key == "castle" || x.Key == "mosque")
            .ToDictionaryAsync(x => x.Key, x => x.Id);

        var places = new List<Place>();

        // Aynı kategoriden çok sayıda kayıt: çeşitlendirme kuralını sınamak için
        for (var i = 0; i < 6; i++)
        {
            places.Add(NewPlace(city.Id, categories["mosque"], $"Test Camii {i}", (short)(90 - i), 9000 + i));
        }

        for (var i = 0; i < 4; i++)
        {
            places.Add(NewPlace(city.Id, categories["museum"], $"Test Müzesi {i}", (short)(88 - i), 9100 + i));
        }

        for (var i = 0; i < 3; i++)
        {
            places.Add(NewPlace(city.Id, categories["castle"], $"Test Kalesi {i}", (short)(85 - i), 9200 + i));
        }

        // Gösterilmemesi gereken kayıtlar
        var withoutPhoto = NewPlace(city.Id, categories["museum"], "Fotografsiz Yer", 80, 9300);
        withoutPhoto.PhotoUrl = null;
        withoutPhoto.PhotoAuthor = null;
        withoutPhoto.PhotoLicense = null;
        places.Add(withoutPhoto);

        var withoutAttribution = NewPlace(city.Id, categories["museum"], "Atifsiz Yer", 80, 9301);
        withoutAttribution.PhotoAuthor = null;
        withoutAttribution.PhotoLicense = null;
        places.Add(withoutAttribution);

        places.Add(NewPlace(city.Id, categories["museum"], "Dusuk Puanli Yer", 10, 9302));

        context.Places.AddRange(places);
        await context.SaveChangesAsync();

        return city.Id;
    }

    private static Place NewPlace(int cityId, int categoryId, string name, short score, long osmId) => new()
    {
        CountryId = 1,
        CityId = cityId,
        CategoryId = categoryId,
        OsmType = OsmElementType.Node,
        OsmId = osmId,
        Name = name,
        Slug = name.ToLowerInvariant().Replace(' ', '-'),
        Location = Factory.CreatePoint(new Coordinate(101, 1)),
        PhotoUrl = "https://upload.wikimedia.org/test.jpg",
        PhotoAuthor = "Test Fotoğrafçı",
        PhotoLicense = "CC BY-SA 4.0",
        DescriptionTr = "Test açıklaması.",
        QualityScore = score,
        IsActive = true
    };

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _api.DisposeAsync();
    }

    private sealed record FeedPage
    {
        public required List<FeedItem> Items { get; init; }
        public string? NextCursor { get; init; }
        public bool HasMore { get; init; }
    }

    private sealed record FeedItem
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
        public required string CategoryKey { get; init; }
        public required string PhotoUrl { get; init; }
        public required string PhotoAttribution { get; init; }
        public required short QualityScore { get; init; }
    }
}
