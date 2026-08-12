using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Yolla.Api.IntegrationTests;

[Collection(PostgisCollection.Name)]
public class AccountEndpointTests(PostgisFixture fixture) : IAsyncLifetime
{
    private const string ValidPassword = "cokGizliSifre1";

    private ApiFactory _api = null!;
    private HttpClient _client = null!;
    private TestData.SeededCity _city = null!;

    public async Task InitializeAsync()
    {
        _api = new ApiFactory(fixture.ConnectionString);
        _client = _api.CreateClient();
        _city = await TestData.EnsureCityWithPlacesAsync(fixture, "hesap-sehri", 200);
    }

    // --- Kayıt ---

    [Fact]
    public async Task Hesap_acilir_ve_jeton_doner()
    {
        var result = await RegisterAsync(NewEmail());

        result.GetProperty("account").GetProperty("userId").GetInt32().ShouldBeGreaterThan(0);
        result.GetProperty("accessToken").GetString().ShouldNotBeNullOrWhiteSpace();
        result.GetProperty("deviceId").GetInt32().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Ayni_eposta_ile_ikinci_hesap_acilmaz()
    {
        var email = NewEmail();

        await RegisterAsync(email);

        var response = await _client.PostAsJsonAsync("/api/v1/account/register", new
        {
            email,
            password = ValidPassword
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("gecersiz-eposta")]
    [InlineData("@example.com")]
    [InlineData("bos@")]
    public async Task Gecersiz_eposta_reddedilir(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/account/register", new
        {
            email,
            password = ValidPassword
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Kisa_sifre_reddedilir()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/account/register", new
        {
            email = NewEmail(),
            password = "kisa"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // --- Anonim geçmişin taşınması ---

    [Fact]
    public async Task Kayit_sirasinda_cihazin_planlari_hesaba_baglanir()
    {
        // Kullanıcı önce anonim kullanıyor, sonra hesap açıyor: hiçbir şey kaybolmamalı
        var deviceUuid = Guid.NewGuid();
        var deviceClient = await CreateDeviceClientAsync(deviceUuid);

        var tripResponse = await deviceClient.PostAsJsonAsync("/api/v1/trips", new
        {
            name = "Anonim Planım",
            mode = "City",
            cityId = _city.CityId,
            placeIds = _city.PlaceIds.Take(2).ToArray()
        });

        tripResponse.EnsureSuccessStatusCode();

        var auth = await RegisterAsync(NewEmail(), deviceUuid);
        var userId = auth.GetProperty("account").GetProperty("userId").GetInt32();

        // Plan artık hesaba bağlı
        await using var context = fixture.CreateDbContext();
        var trips = await context.Trips.Where(x => x.UserId == userId).ToListAsync();

        trips.ShouldContain(x => x.Name == "Anonim Planım");

        // Hesap jetonuyla da erişilebiliyor
        using var accountClient = CreateClientWithToken(auth.GetProperty("accessToken").GetString()!);
        var list = await accountClient.GetFromJsonAsync<JsonElement>("/api/v1/trips");

        list.GetProperty("data").EnumerateArray()
            .ShouldContain(x => x.GetProperty("name").GetString() == "Anonim Planım");
    }

    [Fact]
    public async Task Giris_sirasinda_yeni_cihaz_hesaba_baglanir()
    {
        var email = NewEmail();
        await RegisterAsync(email);

        // Kullanıcı ikinci cihazdan giriyor
        var secondDevice = Guid.NewGuid();
        var login = await LoginAsync(email, ValidPassword, secondDevice);

        var userId = login.GetProperty("account").GetProperty("userId").GetInt32();

        await using var context = fixture.CreateDbContext();
        var device = await context.Devices.FirstAsync(x => x.DeviceUuid == secondDevice);

        device.UserId.ShouldBe(userId);

        login.GetProperty("account").GetProperty("deviceCount").GetInt32().ShouldBe(2);
    }

    // --- Giriş ---

    [Fact]
    public async Task Dogru_bilgilerle_giris_yapilir()
    {
        var email = NewEmail();
        await RegisterAsync(email);

        var login = await LoginAsync(email, ValidPassword);

        login.GetProperty("accessToken").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Yanlis_sifre_reddedilir()
    {
        var email = NewEmail();
        await RegisterAsync(email);

        var response = await _client.PostAsJsonAsync("/api/v1/account/login", new
        {
            email,
            password = "yanlisSifre123"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Olmayan_hesapla_kayitli_hesap_ayni_hatayi_verir()
    {
        // Hata mesajı e-postanın kayıtlı olup olmadığını belli etmemeli;
        // aksi halde hesap listesi çıkarılabilir
        var email = NewEmail();
        await RegisterAsync(email);

        var wrongPassword = await _client.PostAsJsonAsync("/api/v1/account/login",
            new { email, password = "yanlisSifre123" });

        var noAccount = await _client.PostAsJsonAsync("/api/v1/account/login",
            new { email = NewEmail(), password = "yanlisSifre123" });

        wrongPassword.StatusCode.ShouldBe(noAccount.StatusCode);

        var first = await wrongPassword.Content.ReadAsStringAsync();
        var second = await noAccount.Content.ReadAsStringAsync();

        // İzleme kimliği dışında aynı gövde
        ExtractTitle(first).ShouldBe(ExtractTitle(second));
    }

    // --- Hesap bilgisi ---

    [Fact]
    public async Task Hesap_bilgisi_getirilir()
    {
        var auth = await RegisterAsync(NewEmail(), displayName: "Test Kullanıcı");

        using var client = CreateClientWithToken(auth.GetProperty("accessToken").GetString()!);

        var response = await client.GetFromJsonAsync<JsonElement>("/api/v1/account/me");
        var account = response.GetProperty("data");

        account.GetProperty("displayName").GetString().ShouldBe("Test Kullanıcı");
        account.GetProperty("deviceCount").GetInt32().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Cihaz_jetonuyla_hesap_bilgisi_alinamaz()
    {
        // Cihaz jetonu hesaba bağlı değil; hesap uçlarına erişememeli
        var deviceClient = await CreateDeviceClientAsync(Guid.NewGuid());

        var response = await deviceClient.GetAsync("/api/v1/account/me");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Jetonsuz_hesap_bilgisi_alinamaz()
    {
        (await _client.GetAsync("/api/v1/account/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // --- Şifre değiştirme ---

    [Fact]
    public async Task Sifre_degistirilir_ve_yeni_sifreyle_giris_yapilir()
    {
        var email = NewEmail();
        var auth = await RegisterAsync(email);

        using var client = CreateClientWithToken(auth.GetProperty("accessToken").GetString()!);

        var change = await client.PostAsJsonAsync("/api/v1/account/change-password", new
        {
            currentPassword = ValidPassword,
            newPassword = "yeniGizliSifre2"
        });

        change.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var login = await LoginAsync(email, "yeniGizliSifre2");
        login.GetProperty("accessToken").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Yanlis_mevcut_sifreyle_degistirilemez()
    {
        var auth = await RegisterAsync(NewEmail());

        using var client = CreateClientWithToken(auth.GetProperty("accessToken").GetString()!);

        var response = await client.PostAsJsonAsync("/api/v1/account/change-password", new
        {
            currentPassword = "yanlisSifre123",
            newPassword = "yeniGizliSifre2"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // --- Hesap silme ---

    [Fact]
    public async Task Hesap_ve_tum_kisisel_veri_silinir()
    {
        // Uygulama mağazalarının zorunlu kıldığı akış
        var deviceUuid = Guid.NewGuid();
        var deviceClient = await CreateDeviceClientAsync(deviceUuid);

        await deviceClient.PostAsJsonAsync("/api/v1/trips", new
        {
            name = "Silinecek Plan",
            mode = "City",
            cityId = _city.CityId,
            placeIds = _city.PlaceIds.Take(1).ToArray()
        });

        var auth = await RegisterAsync(NewEmail(), deviceUuid);
        var userId = auth.GetProperty("account").GetProperty("userId").GetInt32();

        using var client = CreateClientWithToken(auth.GetProperty("accessToken").GetString()!);

        var response = await client.PostAsJsonAsync("/api/v1/account/delete",
            new { password = ValidPassword });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await using var context = fixture.CreateDbContext();

        (await context.Users.AnyAsync(x => x.Id == userId)).ShouldBeFalse();
        (await context.Trips.AnyAsync(x => x.UserId == userId)).ShouldBeFalse();
        (await context.Devices.AnyAsync(x => x.DeviceUuid == deviceUuid)).ShouldBeFalse();
    }

    [Fact]
    public async Task Sifresiz_hesap_silinemez()
    {
        var auth = await RegisterAsync(NewEmail());

        using var client = CreateClientWithToken(auth.GetProperty("accessToken").GetString()!);

        var response = await client.PostAsJsonAsync("/api/v1/account/delete",
            new { password = "yanlisSifre123" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // --- Yardımcılar ---

    private static string NewEmail() => $"test-{Guid.NewGuid():N}@example.com";

    private static string? ExtractTitle(string problemJson) =>
        JsonDocument.Parse(problemJson).RootElement.GetProperty("title").GetString();

    private async Task<JsonElement> RegisterAsync(
        string email,
        Guid? deviceUuid = null,
        string? displayName = null)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/account/register", new
        {
            email,
            password = ValidPassword,
            displayName,
            deviceUuid
        });

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    }

    private async Task<JsonElement> LoginAsync(string email, string password, Guid? deviceUuid = null)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/account/login", new
        {
            email,
            password,
            deviceUuid
        });

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    }

    private async Task<HttpClient> CreateDeviceClientAsync(Guid deviceUuid)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/devices/register", new
        {
            deviceUuid,
            platform = "ios",
            language = "tr"
        });

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        return CreateClientWithToken(json.GetProperty("data").GetProperty("accessToken").GetString()!);
    }

    private HttpClient CreateClientWithToken(string token)
    {
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
