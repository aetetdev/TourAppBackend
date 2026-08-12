using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace Yolla.Api.IntegrationTests;

[Collection(PostgisCollection.Name)]
public class PlaceDetailEndpointTests(PostgisFixture fixture) : IAsyncLifetime
{
    private ApiFactory _api = null!;
    private HttpClient _client = null!;
    private TestData.SeededCity _city = null!;

    public async Task InitializeAsync()
    {
        _api = new ApiFactory(fixture.ConnectionString);
        _client = _api.CreateClient();
        _city = await TestData.EnsureCityWithPlacesAsync(fixture, "detay-sehri", 140);
    }

    [Fact]
    public async Task Yer_detayi_getirilir()
    {
        var place = await GetDetailAsync($"/api/v1/places/{_city.PlaceIds[0]}");

        place.GetProperty("name").GetString().ShouldNotBeNullOrWhiteSpace();
        place.GetProperty("address").GetString().ShouldBe("Test Caddesi No:1");
        place.GetProperty("openingHours").GetString().ShouldBe("Tu-Su 09:00-17:00");
        place.GetProperty("website").GetString().ShouldBe("https://example.com");
    }

    [Fact]
    public async Task Detayda_yol_tarifi_baglantisi_bulunur()
    {
        // Kullanıcı güncel yorumlar ve yol tarifi için harita uygulamasına gidebilmeli
        var place = await GetDetailAsync($"/api/v1/places/{_city.PlaceIds[0]}");

        var url = place.GetProperty("directionsUrl").GetString();

        url.ShouldNotBeNullOrWhiteSpace();
        url.ShouldStartWith("https://");
        url.ShouldContain("destination=");
    }

    [Fact]
    public async Task Wikipedia_baglantisi_uretilir()
    {
        var place = await GetDetailAsync($"/api/v1/places/{_city.PlaceIds[0]}");

        var url = place.GetProperty("wikipediaUrl").GetString();

        url.ShouldNotBeNull();
        url.ShouldContain("tr.wikipedia.org");
        url.ShouldContain("Test_Makalesi");
    }

    [Fact]
    public async Task Makalesi_olmayan_yerde_wikipedia_baglantisi_bos_kalir()
    {
        var place = await GetDetailAsync($"/api/v1/places/{_city.PlaceIds[1]}");

        place.GetProperty("wikipediaUrl").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Detayda_yakindaki_yerler_listelenir()
    {
        var place = await GetDetailAsync($"/api/v1/places/{_city.PlaceIds[0]}");

        var nearby = place.GetProperty("nearby").EnumerateArray().ToList();

        nearby.ShouldNotBeEmpty();
        // Kendisi listede olmamalı
        nearby.ShouldAllBe(x => x.GetProperty("id").GetInt32() != _city.PlaceIds[0]);
        // Mesafeye göre artan sırada
        var distances = nearby.Select(x => x.GetProperty("distanceMeters").GetInt32()).ToList();
        distances.ShouldBe(distances.OrderBy(x => x));
    }

    [Fact]
    public async Task Yer_kisa_adiyla_getirilir()
    {
        // Web sayfaları bu ucu kullanıyor: /tr/sehir/{sehir}/{yer}
        var place = await GetDetailAsync(
            $"/api/v1/places/by-slug/{_city.CitySlug}/{_city.CitySlug}-yeri-0");

        place.GetProperty("id").GetInt32().ShouldBe(_city.PlaceIds[0]);
        place.GetProperty("citySlug").GetString().ShouldBe(_city.CitySlug);
    }

    [Fact]
    public async Task Yanlis_sehirle_eslesen_kisa_ad_bulunamaz()
    {
        var response = await _client.GetAsync(
            $"/api/v1/places/by-slug/olmayan-sehir/{_city.CitySlug}-yeri-0");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Olmayan_yer_icin_404_doner()
    {
        var response = await _client.GetAsync("/api/v1/places/99999999");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Yakindakiler_ucu_mesafeye_gore_siralar()
    {
        var response = await _client.GetFromJsonAsync<JsonElement>(
            "/api/v1/places/nearby?latitude=1&longitude=140.5&radiusMeters=20000&take=5");

        response.GetProperty("data").GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Theory]
    [InlineData(91, 30)]
    [InlineData(41, 181)]
    public async Task Yakindakiler_gecersiz_koordinati_reddeder(double latitude, double longitude)
    {
        var response = await _client.GetAsync(
            $"/api/v1/places/nearby?latitude={latitude}&longitude={longitude}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Ingilizce_istekte_ingilizce_kategori_adi_doner()
    {
        var place = await GetDetailAsync($"/api/v1/places/{_city.PlaceIds[0]}?language=en");

        place.GetProperty("categoryName").GetString().ShouldBe("Museum");
    }

    private async Task<JsonElement> GetDetailAsync(string url)
    {
        var response = await _client.GetAsync(url);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        return json.GetProperty("data");
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _api.DisposeAsync();
    }
}
