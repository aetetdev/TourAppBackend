using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace Yolla.Api.IntegrationTests;

[Collection(PostgisCollection.Name)]
public class TripEndpointTests(PostgisFixture fixture) : IAsyncLifetime
{
    private ApiFactory _api = null!;
    private HttpClient _client = null!;
    private TestData.SeededCity _city = null!;

    public async Task InitializeAsync()
    {
        _api = new ApiFactory(fixture.ConnectionString);
        _city = await TestData.EnsureCityWithPlacesAsync(fixture, "plan-sehri", 160);
        _client = await CreateAuthenticatedClientAsync();
    }

    [Fact]
    public async Task Plan_olusturulur()
    {
        var trip = await CreateTripAsync("Test Gezisi", _city.PlaceIds.Take(3));

        trip.GetProperty("name").GetString().ShouldBe("Test Gezisi");
        trip.GetProperty("places").GetArrayLength().ShouldBe(3);
    }

    [Fact]
    public async Task Ad_verilmezse_sehir_adindan_uretilir()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/trips", new
        {
            mode = "City",
            travelMode = "Foot",
            cityId = _city.CityId
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var trip = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");

        trip.GetProperty("name").GetString()!.ShouldContain("gezisi");
    }

    [Fact]
    public async Task Sehir_ici_planda_sehir_zorunlu()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/trips", new
        {
            mode = "City",
            travelMode = "Foot"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Rota_planinda_baslangic_ve_varis_zorunlu()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/trips", new
        {
            mode = "Route",
            travelMode = "Car"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Planlar_listelenir()
    {
        await CreateTripAsync("Listelenecek Gezi", _city.PlaceIds.Take(2));

        var response = await _client.GetFromJsonAsync<JsonElement>("/api/v1/trips");
        var trips = response.GetProperty("data").EnumerateArray().ToList();

        trips.ShouldNotBeEmpty();
        trips.ShouldContain(x => x.GetProperty("name").GetString() == "Listelenecek Gezi");
    }

    [Fact]
    public async Task Plan_listesinde_kapak_gorseli_bulunur()
    {
        await CreateTripAsync("Kapakli Gezi", _city.PlaceIds.Take(2));

        var response = await _client.GetFromJsonAsync<JsonElement>("/api/v1/trips");

        var trip = response.GetProperty("data").EnumerateArray()
            .First(x => x.GetProperty("name").GetString() == "Kapakli Gezi");

        trip.GetProperty("coverPhotoUrl").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Plan_adi_degistirilir()
    {
        var trip = await CreateTripAsync("Eski Ad", _city.PlaceIds.Take(2));
        var tripId = trip.GetProperty("id").GetInt32();

        var response = await _client.PatchAsJsonAsync($"/api/v1/trips/{tripId}", new { name = "Yeni Ad" });

        response.EnsureSuccessStatusCode();

        var updated = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        updated.GetProperty("name").GetString().ShouldBe("Yeni Ad");
    }

    [Fact]
    public async Task Bos_ad_reddedilir()
    {
        var trip = await CreateTripAsync("Ad Testi", _city.PlaceIds.Take(1));
        var tripId = trip.GetProperty("id").GetInt32();

        var response = await _client.PatchAsJsonAsync($"/api/v1/trips/{tripId}", new { name = "   " });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Plana_durak_eklenip_cikarilir()
    {
        var trip = await CreateTripAsync("Durak Testi", _city.PlaceIds.Take(2));
        var tripId = trip.GetProperty("id").GetInt32();

        var afterAdd = await ModifyPlacesAsync(tripId, add: _city.PlaceIds.Skip(2).Take(2));
        afterAdd.GetProperty("places").GetArrayLength().ShouldBe(4);

        var afterRemove = await ModifyPlacesAsync(tripId, remove: _city.PlaceIds.Take(1));
        afterRemove.GetProperty("places").GetArrayLength().ShouldBe(3);
    }

    [Fact]
    public async Task Ayni_yer_iki_kez_eklenmez()
    {
        var trip = await CreateTripAsync("Tekrar Testi", _city.PlaceIds.Take(2));
        var tripId = trip.GetProperty("id").GetInt32();

        var result = await ModifyPlacesAsync(tripId, add: _city.PlaceIds.Take(2));

        result.GetProperty("places").GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task Duraklar_degisince_hesaplanmis_rota_temizlenir()
    {
        // Eski rota yeni duraklarla uyuşmuyor; kullanıcıya yanlış mesafe gösterilmemeli
        var trip = await CreateTripAsync("Rota Temizleme", _city.PlaceIds.Take(3));
        var tripId = trip.GetProperty("id").GetInt32();

        await using (var context = fixture.CreateDbContext())
        {
            var entity = context.Trips.First(x => x.Id == tripId);
            entity.TotalDistanceMeters = 1234;
            entity.RouteGeometry = "eski_rota";
            await context.SaveChangesAsync();
        }

        var updated = await ModifyPlacesAsync(tripId, add: _city.PlaceIds.Skip(3).Take(1));

        updated.GetProperty("distanceMeters").ValueKind.ShouldBe(JsonValueKind.Null);
        updated.GetProperty("routeGeometry").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Ulasim_tipi_degisince_rota_temizlenir()
    {
        var trip = await CreateTripAsync("Ulasim Testi", _city.PlaceIds.Take(2));
        var tripId = trip.GetProperty("id").GetInt32();

        await using (var context = fixture.CreateDbContext())
        {
            var entity = context.Trips.First(x => x.Id == tripId);
            entity.TotalDistanceMeters = 500;
            await context.SaveChangesAsync();
        }

        var response = await _client.PatchAsJsonAsync($"/api/v1/trips/{tripId}", new { travelMode = "Car" });
        var updated = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");

        updated.GetProperty("distanceMeters").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Plan_silinir()
    {
        var trip = await CreateTripAsync("Silinecek", _city.PlaceIds.Take(1));
        var tripId = trip.GetProperty("id").GetInt32();

        var deleteResponse = await _client.DeleteAsync($"/api/v1/trips/{tripId}");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var getResponse = await _client.GetAsync($"/api/v1/trips/{tripId}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Baska_cihazin_plani_gorulemez()
    {
        // Sahiplik kontrolü sorgunun içinde: planın varlığı bile sızmamalı
        var trip = await CreateTripAsync("Gizli Plan", _city.PlaceIds.Take(1));
        var tripId = trip.GetProperty("id").GetInt32();

        using var otherClient = await CreateAuthenticatedClientAsync();

        var response = await otherClient.GetAsync($"/api/v1/trips/{tripId}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Baska_cihazin_plani_silinemez()
    {
        var trip = await CreateTripAsync("Korunacak Plan", _city.PlaceIds.Take(1));
        var tripId = trip.GetProperty("id").GetInt32();

        using var otherClient = await CreateAuthenticatedClientAsync();

        var response = await otherClient.DeleteAsync($"/api/v1/trips/{tripId}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Plan hâlâ sahibinde duruyor
        (await _client.GetAsync($"/api/v1/trips/{tripId}")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Jetonsuz_erisim_reddedilir()
    {
        using var anonymous = _api.CreateClient();

        (await anonymous.GetAsync("/api/v1/trips")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Bos_planda_rota_hesaplanamaz()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/trips", new
        {
            name = "Bos Plan",
            mode = "City",
            cityId = _city.CityId
        });

        var trip = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        var tripId = trip.GetProperty("id").GetInt32();

        var optimize = await _client.PostAsync($"/api/v1/trips/{tripId}/optimize", null);

        optimize.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Gezme_suresi_toplami_hesaplanir()
    {
        // Her test yerinin ortalama gezme süresi 30 dakika
        var trip = await CreateTripAsync("Sure Testi", _city.PlaceIds.Take(3));

        trip.GetProperty("visitDurationMinutes").GetInt32().ShouldBe(90);
    }

    private async Task<JsonElement> CreateTripAsync(string name, IEnumerable<int> placeIds)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/trips", new
        {
            name,
            mode = "City",
            travelMode = "Foot",
            cityId = _city.CityId,
            placeIds = placeIds.ToArray()
        });

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    }

    private async Task<JsonElement> ModifyPlacesAsync(
        int tripId,
        IEnumerable<int>? add = null,
        IEnumerable<int>? remove = null)
    {
        var response = await _client.PostAsJsonAsync($"/api/v1/trips/{tripId}/places", new
        {
            add = add?.ToArray(),
            remove = remove?.ToArray()
        });

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        using var registrar = _api.CreateClient();

        var response = await registrar.PostAsJsonAsync("/api/v1/devices/register", new
        {
            deviceUuid = Guid.NewGuid(),
            platform = "ios",
            language = "tr"
        });

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = json.GetProperty("data").GetProperty("accessToken").GetString();

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
