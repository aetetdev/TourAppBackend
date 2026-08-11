using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Shouldly;
using Yolla.Domain.Entities;
using Yolla.Domain.Enums;

namespace Yolla.Api.IntegrationTests;

[Collection(PostgisCollection.Name)]
public class DeviceAndSwipeEndpointTests(PostgisFixture fixture) : IAsyncLifetime
{
    private static readonly GeometryFactory Factory = new(new PrecisionModel(), 4326);

    private ApiFactory _api = null!;
    private HttpClient _client = null!;
    private int _cityId;
    private List<int> _placeIds = null!;

    public async Task InitializeAsync()
    {
        _api = new ApiFactory(fixture.ConnectionString);
        _client = _api.CreateClient();
        (_cityId, _placeIds) = await SeedAsync();
    }

    // --- Cihaz oturumu ---

    [Fact]
    public async Task Cihaz_kaydolup_jeton_alir()
    {
        var session = await RegisterDeviceAsync();

        session.DeviceId.ShouldBeGreaterThan(0);
        session.AccessToken.ShouldNotBeNullOrWhiteSpace();
        session.ExpiresAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddDays(80));
    }

    [Fact]
    public async Task Ayni_cihaz_tekrar_kaydolunca_yeni_kayit_acilmaz()
    {
        // İstemci her açılışta kayıt çağırabilmeli; kayıtlar çoğalmamalı
        var uuid = Guid.NewGuid();

        var first = await RegisterDeviceAsync(uuid);
        var second = await RegisterDeviceAsync(uuid, platform: "web", language: "en");

        second.DeviceId.ShouldBe(first.DeviceId);

        await using var context = fixture.CreateDbContext();
        (await context.Devices.CountAsync(x => x.DeviceUuid == uuid)).ShouldBe(1);
    }

    [Theory]
    [InlineData("windows")]
    [InlineData("")]
    public async Task Desteklenmeyen_platform_reddedilir(string platform)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/devices/register", new
        {
            deviceUuid = Guid.NewGuid(),
            platform,
            language = "tr"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Bos_cihaz_kimligi_reddedilir()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/devices/register", new
        {
            deviceUuid = Guid.Empty,
            platform = "ios"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // --- Yetkilendirme ---

    [Fact]
    public async Task Jetonsuz_kaydirma_reddedilir()
    {
        // Aksi halde herkes başkasının cihazı adına kayıt yazabilirdi
        var response = await _client.PostAsJsonAsync("/api/v1/discovery/swipes", new
        {
            swipes = new[] { new { placeId = _placeIds[0], direction = "Like" } }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Gecersiz_jeton_reddedilir()
    {
        using var client = _api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "sahte.jeton.degeri");

        var response = await client.GetAsync("/api/v1/discovery/swipes/liked");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // --- Kaydırma ---

    [Fact]
    public async Task Kaydirmalar_kaydedilir()
    {
        var (client, _) = await CreateAuthenticatedClientAsync();

        var result = await RecordSwipesAsync(client,
            (_placeIds[0], "Like"), (_placeIds[1], "Like"), (_placeIds[2], "Pass"));

        result.Recorded.ShouldBe(3);
        result.TotalLiked.ShouldBe(2);
    }

    [Fact]
    public async Task Kaydirilan_yer_feedde_tekrar_gosterilmez()
    {
        var (client, _) = await CreateAuthenticatedClientAsync();

        var before = await GetFeedIdsAsync(client);
        before.ShouldNotBeEmpty();

        await RecordSwipesAsync(client, before.Select(id => (id, "Pass")).ToArray());

        var after = await GetFeedIdsAsync(client);

        after.ShouldNotContain(x => before.Contains(x));
    }

    [Fact]
    public async Task Ayni_yer_tekrar_kaydirilinca_yon_guncellenir()
    {
        // Kullanıcı fikrini değiştirebilir; ikinci kayıt açılmamalı
        var (client, deviceId) = await CreateAuthenticatedClientAsync();

        await RecordSwipesAsync(client, (_placeIds[0], "Like"));
        var result = await RecordSwipesAsync(client, (_placeIds[0], "Pass"));

        result.TotalLiked.ShouldBe(0);

        await using var context = fixture.CreateDbContext();
        var swipes = await context.Swipes
            .Where(x => x.DeviceId == deviceId && x.PlaceId == _placeIds[0])
            .ToListAsync();

        swipes.Count.ShouldBe(1);
        swipes[0].Direction.ShouldBe(SwipeDirection.Pass);
    }

    [Fact]
    public async Task Begenilen_yerler_listelenir()
    {
        var (client, _) = await CreateAuthenticatedClientAsync();

        await RecordSwipesAsync(client, (_placeIds[0], "Like"), (_placeIds[1], "Pass"));

        var response = await client.GetFromJsonAsync<JsonElement>("/api/v1/discovery/swipes/liked");
        var items = response.GetProperty("data").EnumerateArray().ToList();

        items.Count.ShouldBe(1);
        items[0].GetProperty("id").GetInt32().ShouldBe(_placeIds[0]);
    }

    [Fact]
    public async Task Bos_kaydirma_listesi_reddedilir()
    {
        var (client, _) = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/discovery/swipes",
            new { swipes = Array.Empty<object>() });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cok_buyuk_toplu_istek_reddedilir()
    {
        var (client, _) = await CreateAuthenticatedClientAsync();

        var swipes = Enumerable.Range(1, 201)
            .Select(i => new { placeId = i, direction = "Like" })
            .ToArray();

        var response = await client.PostAsJsonAsync("/api/v1/discovery/swipes", new { swipes });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Bir_cihazin_kaydirmalari_digerini_etkilemez()
    {
        var (first, _) = await CreateAuthenticatedClientAsync();
        var (second, _) = await CreateAuthenticatedClientAsync();

        await RecordSwipesAsync(first, (_placeIds[0], "Like"));

        var secondLiked = await second.GetFromJsonAsync<JsonElement>("/api/v1/discovery/swipes/liked");

        secondLiked.GetProperty("data").GetArrayLength().ShouldBe(0);
    }

    // --- Yardımcılar ---

    private async Task<DeviceSession> RegisterDeviceAsync(
        Guid? uuid = null,
        string platform = "ios",
        string language = "tr")
    {
        var response = await _client.PostAsJsonAsync("/api/v1/devices/register", new
        {
            deviceUuid = uuid ?? Guid.NewGuid(),
            platform,
            appVersion = "1.0.0",
            language
        });

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var data = json.GetProperty("data");

        return new DeviceSession(
            data.GetProperty("deviceId").GetInt32(),
            data.GetProperty("accessToken").GetString()!,
            data.GetProperty("expiresAt").GetDateTimeOffset());
    }

    private async Task<(HttpClient Client, int DeviceId)> CreateAuthenticatedClientAsync()
    {
        var session = await RegisterDeviceAsync();

        var client = _api.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session.AccessToken);

        return (client, session.DeviceId);
    }

    private async Task<SwipeResult> RecordSwipesAsync(
        HttpClient client,
        params (int PlaceId, string Direction)[] swipes)
    {
        var response = await client.PostAsJsonAsync("/api/v1/discovery/swipes", new
        {
            swipes = swipes.Select(x => new { placeId = x.PlaceId, direction = x.Direction, context = "City" })
        });

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var data = json.GetProperty("data");

        return new SwipeResult(
            data.GetProperty("recorded").GetInt32(),
            data.GetProperty("totalLiked").GetInt32());
    }

    private async Task<List<int>> GetFeedIdsAsync(HttpClient client)
    {
        var json = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/discovery/city/{_cityId}/feed?take=5");

        return json.GetProperty("data").GetProperty("items")
            .EnumerateArray()
            .Select(x => x.GetProperty("id").GetInt32())
            .ToList();
    }

    private async Task<(int CityId, List<int> PlaceIds)> SeedAsync()
    {
        await using var context = fixture.CreateDbContext();

        const string slug = "test-swipe-sehri";
        var city = await context.Cities.FirstOrDefaultAsync(x => x.Slug == slug);

        if (city is null)
        {
            var ring = Factory.CreateLinearRing(
            [
                new Coordinate(120, 0), new Coordinate(120, 2),
                new Coordinate(122, 2), new Coordinate(122, 0), new Coordinate(120, 0)
            ]);

            city = new City
            {
                CountryId = 1,
                Name = "Test Swipe Şehri",
                NameNormalized = "test swipe sehri",
                Slug = slug,
                Center = Factory.CreatePoint(new Coordinate(121, 1)),
                Boundary = Factory.CreatePolygon(ring),
                IsActive = true
            };

            context.Cities.Add(city);
            await context.SaveChangesAsync();

            var categoryId = await context.Categories.Where(x => x.Key == "museum")
                .Select(x => x.Id).FirstAsync();

            for (var i = 0; i < 10; i++)
            {
                context.Places.Add(new Place
                {
                    CountryId = 1,
                    CityId = city.Id,
                    CategoryId = categoryId,
                    OsmType = OsmElementType.Node,
                    OsmId = 8000 + i,
                    Name = $"Swipe Test Yeri {i}",
                    Slug = $"swipe-test-yeri-{i}",
                    Location = Factory.CreatePoint(new Coordinate(121, 1)),
                    PhotoUrl = "https://upload.wikimedia.org/test.jpg",
                    PhotoAuthor = "Test Fotoğrafçı",
                    PhotoLicense = "CC BY-SA 4.0",
                    DescriptionTr = "Test açıklaması.",
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

        return (city.Id, placeIds);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _api.DisposeAsync();
    }

    private sealed record DeviceSession(int DeviceId, string AccessToken, DateTimeOffset ExpiresAt);

    private sealed record SwipeResult(int Recorded, int TotalLiked);
}
