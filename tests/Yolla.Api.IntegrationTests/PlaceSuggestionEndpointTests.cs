using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace Yolla.Api.IntegrationTests;

/// <summary>
/// Kullanıcının önerdiği yerler.
/// </summary>
/// <remarks>
/// Öneri kataloğa doğrudan girmiyor; kuyruğa düşüyor. Buradaki testler
/// kuyruğu koruyan kuralları sabitliyor: Türkiye dışı koordinat, tekrar
/// öneri ve kota. Üçü de gevşediğinde moderasyon kuyruğu kullanılamaz hale
/// geliyor — kuyruk tıkanınca gerçek öneriler de incelenmiyor.
/// </remarks>
[Collection(PostgisCollection.Name)]
public class PlaceSuggestionEndpointTests(PostgisFixture fixture) : IAsyncLifetime
{
    private const string Password = "Yolla!2026parola";

    private ApiFactory _api = null!;
    private HttpClient _client = null!;
    private TestData.SeededCity _city = null!;

    /// <summary>Şehir sınırının içinde kalan bir nokta.</summary>
    private const double InsideLongitude = 41.0;
    private const double InsideLatitude = 1.0;

    public async Task InitializeAsync()
    {
        _api = new ApiFactory(fixture.ConnectionString);
        _client = _api.CreateClient();

        // Sınır kutusu: boylam 40-42, enlem 0-2.
        _city = await TestData.EnsureCityWithPlacesAsync(fixture, "oneri-sehri", 40);
    }

    [Fact]
    public async Task Oneri_kuyruga_dusuyor_katalona_girmiyor()
    {
        var client = await SignedInClientAsync("oneri1@ornek.com");

        var created = await SuggestAsync(client, "Gizli Şelale", InsideLongitude, InsideLatitude);

        created.GetProperty("status").GetString().ShouldBe("Pending");
        created.GetProperty("cityName").GetString().ShouldNotBeNullOrWhiteSpace();

        // Onaylanmadan yer yaratılmıyor: kataloğa girseydi kimliği dönerdi.
        created.GetProperty("placeId").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Kullanici_kendi_onerilerini_goruyor()
    {
        var client = await SignedInClientAsync("oneri2@ornek.com");
        await SuggestAsync(client, "Köy Çeşmesi", InsideLongitude, InsideLatitude);

        var response = await client.GetAsync("/api/v1/places/oneriler/benim");
        response.EnsureSuccessStatusCode();

        var items = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data");

        items.GetArrayLength().ShouldBe(1);
        items[0].GetProperty("name").GetString().ShouldBe("Köy Çeşmesi");
    }

    [Fact]
    public async Task Turkiye_disindaki_konum_reddediliyor()
    {
        // Sınırların dışındaki koordinat kullanıcıya anında söylenmeli;
        // moderatörün önüne düşerse kuyruğu boşuna meşgul ediyor.
        var client = await SignedInClientAsync("oneri3@ornek.com");

        var response = await PostSuggestionAsync(client, "Okyanus Ortası", 0, 0);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Ayni_yer_ikinci_kez_onerilemiyor()
    {
        var client = await SignedInClientAsync("oneri4@ornek.com");
        await SuggestAsync(client, "Tekrar Kalesi", InsideLongitude, InsideLatitude);

        // Aynı ad, birkaç metre öteden: aynı yer sayılmalı.
        var response = await PostSuggestionAsync(
            client, "tekrar kalesı", InsideLongitude + 0.0005, InsideLatitude);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Kota_dolunca_yeni_oneri_alinmiyor()
    {
        var client = await SignedInClientAsync("oneri5@ornek.com");

        // Kotayı doldururken her öneri ayrı bir noktada: tekrar kuralına
        // takılmasınlar, ölçülen şey kota olsun.
        for (var i = 0; i < 5; i++)
        {
            await SuggestAsync(
                client, $"Kota Yeri {i}", InsideLongitude + i * 0.02, InsideLatitude);
        }

        var response = await PostSuggestionAsync(
            client, "Fazladan Yer", InsideLongitude + 0.5, InsideLatitude);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Hesapsiz_oneri_gonderilemiyor()
    {
        var response = await PostSuggestionAsync(
            _client, "Anonim Yer", InsideLongitude, InsideLatitude);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Kategoriler_listeleniyor()
    {
        var client = await SignedInClientAsync("oneri6@ornek.com");

        var response = await client.GetAsync("/api/v1/places/oneriler/kategoriler");
        response.EnsureSuccessStatusCode();

        var items = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data");

        items.GetArrayLength().ShouldBeGreaterThan(0);
        items[0].GetProperty("key").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Moderasyon_kuyrugu_moderator_olmayana_kapali()
    {
        var client = await SignedInClientAsync("oneri7@ornek.com");

        var response = await client.GetAsync("/api/v1/moderation/yer-onerileri");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private async Task<JsonElement> SuggestAsync(
        HttpClient client,
        string name,
        double longitude,
        double latitude)
    {
        var response = await PostSuggestionAsync(client, name, longitude, latitude);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    }

    private static Task<HttpResponseMessage> PostSuggestionAsync(
        HttpClient client,
        string name,
        double longitude,
        double latitude) =>
        client.PostAsJsonAsync("/api/v1/places/oneriler", new
        {
            name,
            categoryKey = "museum",
            latitude,
            longitude,
            description = "Test önerisi"
        });

    private async Task<HttpClient> SignedInClientAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/account/register", new
        {
            email,
            password = Password,
            deviceUuid = Guid.NewGuid()
        });

        response.EnsureSuccessStatusCode();

        var token = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data")
            .GetProperty("accessToken")
            .GetString()!;

        var client = _api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _api.DisposeAsync();
    }
}
