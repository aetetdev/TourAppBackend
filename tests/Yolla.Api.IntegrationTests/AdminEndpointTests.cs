using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace Yolla.Api.IntegrationTests;

/// <summary>
/// Yönetim panosunun uçları.
/// </summary>
/// <remarks>
/// İki şeyi sabitliyor:
///
/// 1. **Kişisel veri sızmıyor.** Panonun amacı tercihi anlamak, kişiyi
///    tanımak değil. Bir alan yanlışlıkla eklenirse burada yakalanmalı —
///    yayına çıktıktan sonra geri almak, veriyi çoktan dağıtmış olmak
///    demek.
/// 2. **Yetki kapalı.** Rolü olmayan hesap hiçbir ölçümü göremiyor.
/// </remarks>
[Collection(PostgisCollection.Name)]
public class AdminEndpointTests(PostgisFixture fixture) : IAsyncLifetime
{
    private const string Password = "Yolla!2026parola";
    private const string ModeratorRole = "moderator";

    /// <summary>Yanıtlarda hiç görünmemesi gereken alanlar.</summary>
    private static readonly string[] KisiselAlanlar =
        ["email", "userName", "displayName", "normalizedEmail", "passwordHash", "phoneNumber"];

    private ApiFactory _api = null!;
    private HttpClient _client = null!;
    private HttpClient _moderator = null!;

    public async Task InitializeAsync()
    {
        _api = new ApiFactory(fixture.ConnectionString);
        _client = _api.CreateClient();

        var city = await TestData.EnsureCityWithPlacesAsync(fixture, "admin-sehri", 30);
        await EnsurePhotolessPlacesAsync(city.CityId);

        _moderator = await ModeratorClientAsync("admin-moderator@ornek.com");
    }

    /// <summary>
    /// Fotoğrafsız birkaç yer ekler.
    /// </summary>
    /// <remarks>
    /// Ortak deneme verisindeki yerlerin hepsinin fotoğrafı var; içerik
    /// ekibinin iş listesi bu veriyle boş çıkıyor ve ekleme uçları
    /// denenemiyor. Puanları farklı veriliyor ki sıralama da ölçülebilsin.
    /// </remarks>
    private async Task EnsurePhotolessPlacesAsync(int cityId)
    {
        await using var context = fixture.CreateDbContext();

        if (await context.Places.AnyAsync(x => x.CityId == cityId && x.PhotoUrl == null))
        {
            return;
        }

        var categoryId = await context.Categories
            .Where(x => x.Key == "museum")
            .Select(x => x.Id)
            .FirstAsync();

        for (var i = 0; i < 3; i++)
        {
            context.Places.Add(new Yolla.Domain.Entities.Place
            {
                CountryId = 1,
                CityId = cityId,
                CategoryId = categoryId,
                OsmType = Yolla.Domain.Enums.OsmElementType.Node,
                OsmId = -900_000L - i,
                Name = $"Fotoğrafsız Yer {i}",
                Slug = $"fotografsiz-yer-{i}",
                Location = TestData.Factory.CreatePoint(
                    new NetTopologySuite.Geometries.Coordinate(31.2 + i * 0.01, 1.2)),
                QualityScore = (short)(40 - i * 10),
                IsActive = true
            });
        }

        await context.SaveChangesAsync();
    }

    [Theory]
    [InlineData("/api/v1/admin/ozet")]
    [InlineData("/api/v1/admin/sehirler")]
    [InlineData("/api/v1/admin/yerler")]
    [InlineData("/api/v1/admin/rotalar")]
    [InlineData("/api/v1/admin/uyelik")]
    [InlineData("/api/v1/admin/fotografsiz-yerler")]
    public async Task Rolsuz_hesap_panoyu_goremez(string yol)
    {
        var sade = await SignedInClientAsync($"rolsuz{Guid.NewGuid():N}@ornek.com");

        var response = await sade.GetAsync(yol);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("/api/v1/admin/ozet")]
    [InlineData("/api/v1/admin/sehirler")]
    [InlineData("/api/v1/admin/yerler")]
    [InlineData("/api/v1/admin/rotalar")]
    [InlineData("/api/v1/admin/uyelik")]
    public async Task Pano_kisisel_veri_dondurmuyor(string yol)
    {
        var response = await _moderator.GetAsync(yol);
        response.EnsureSuccessStatusCode();

        var govde = await response.Content.ReadAsStringAsync();

        foreach (var alan in KisiselAlanlar)
        {
            govde.ShouldNotContain(
                $"\"{alan}\"",
                Case.Insensitive,
                $"{yol} yanıtında '{alan}' alanı var; pano yalnızca toplam döndürmeli.");
        }
    }

    [Fact]
    public async Task Ozet_sayilari_donuyor()
    {
        var ozet = await OkuAsync("/api/v1/admin/ozet");

        ozet.GetProperty("placeCount").GetInt32().ShouldBeGreaterThan(0);
        ozet.GetProperty("cityCount").GetInt32().ShouldBeGreaterThan(0);
        ozet.GetProperty("platforms").ValueKind.ShouldBe(JsonValueKind.Array);

        // Eğri her zaman otuz gün: veri olmayan günler eksik değil sıfır
        // olmalı, yoksa grafik kopuk çiziliyor.
        ozet.GetProperty("tripsByDay").GetArrayLength().ShouldBe(30);
    }

    [Fact]
    public async Task Fotografsiz_yerler_kaliteliden_baslıyor()
    {
        // Ekibin zamanı sınırlı: fotoğraf eklendiğinde doğrudan kart
        // destesine girecek kayıtlar başta olmalı.
        var liste = await OkuAsync("/api/v1/admin/fotografsiz-yerler?take=10");

        liste.GetArrayLength().ShouldBeGreaterThan(0);

        var puanlar = liste.EnumerateArray()
            .Select(x => x.GetProperty("qualityScore").GetInt32())
            .ToList();

        puanlar.ShouldBe(puanlar.OrderByDescending(x => x).ToList());
    }

    [Fact]
    public async Task Ekip_yer_ekleyince_katalogda_beliriyor()
    {
        var response = await _moderator.PostAsJsonAsync("/api/v1/admin/yerler", new
        {
            name = "Ekip Deneme Terası",
            categoryKey = "museum",
            // Şehir sınırı: boylam 30-32, enlem 0-2.
            latitude = 1.0,
            longitude = 31.0,
            description = "Test kaydı."
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var eklenen = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data");

        eklenen.GetProperty("placeId").GetInt32().ShouldBeGreaterThan(0);
        // Şehir koordinattan bulunuyor; istekte gönderilmiyor.
        eklenen.GetProperty("cityName").GetString().ShouldNotBeNullOrWhiteSpace();
        eklenen.GetProperty("slug").GetString().ShouldContain("ekip-deneme-terasi");
    }

    [Fact]
    public async Task Turkiye_disindaki_yer_eklenemiyor()
    {
        var response = await _moderator.PostAsJsonAsync("/api/v1/admin/yerler", new
        {
            name = "Okyanus Ortası",
            categoryKey = "museum",
            latitude = 0.0,
            longitude = 0.0
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Fotografci_adi_olmadan_fotograf_eklenemiyor()
    {
        // Atıfsız görsel yayınlamak hem lisansa aykırı hem de emeği
        // görünmez kılıyor.
        var yerId = await FotografsizYerIdAsync();

        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(TestPhoto()), "photo", "test.png");
        form.Add(new StringContent(string.Empty), "photographerName");

        var response = await _moderator.PostAsync(
            $"/api/v1/admin/yerler/{yerId}/fotograf", form);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Ekip_fotografi_yere_isleniyor_ve_puan_yukseliyor()
    {
        // Katkı satırı yazmak yetmiyordu: kart destesi ve yer detayı
        // `Place` üzerindeki alanlara bakıyor. Katkı işlenmezse fotoğraf
        // hiçbir ekranda görünmüyor.
        var yerId = await FotografsizYerIdAsync();

        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(TestPhoto()), "photo", "test.png");
        form.Add(new StringContent("Ayşe Yılmaz"), "photographerName");
        form.Add(new StringContent("CC BY-SA 4.0"), "license");

        var response = await _moderator.PostAsync(
            $"/api/v1/admin/yerler/{yerId}/fotograf", form);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await using var kontrol = fixture.CreateDbContext();
        var yer = await kontrol.Places.AsNoTracking().FirstAsync(x => x.Id == yerId);

        // Asıl mesele bu: katkı satırı yazmak yetmiyor, alanların yere
        // işlenmesi gerekiyor. Yazılmazsa fotoğraf hiçbir ekranda görünmüyor.
        yer.PhotoUrl.ShouldNotBeNullOrWhiteSpace();
        yer.PhotoAuthor.ShouldBe("Ayşe Yılmaz");
        yer.PhotoLicense.ShouldBe("CC BY-SA 4.0");

        // Ekibin iş listesinden düşmüş olmalı; hâlâ görünüyorsa aynı yere
        // ikinci kez fotoğraf aranıyor demektir.
        var isListesi = await OkuAsync("/api/v1/admin/fotografsiz-yerler?take=50");
        isListesi.EnumerateArray()
            .Select(x => x.GetProperty("placeId").GetInt32())
            .ShouldNotContain(yerId);
    }

    [Fact]
    public async Task Onaylanan_kullanici_fotografi_yere_isleniyor()
    {
        // Gerileme testi. Onay yalnızca `PlaceContribution` satırı yazıyordu
        // ama o satırı hiçbir sorgu okumuyor: kart destesi ve yer detayı
        // `Place` alanlarına bakıyor. Sonuç: kullanıcı coinini alıyor,
        // fotoğrafı hiçbir yerde görünmüyor, yer fotoğrafsız sayılmaya devam
        // ediyordu — katkının bütün amacı buydu.
        var yerId = await FotografsizYerIdAsync();
        var gonderen = await SignedInClientAsync($"katkici{Guid.NewGuid():N}@ornek.com");

        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(TestPhoto()), "photo", "test.png");
        form.Add(new StringContent(yerId.ToString()), "placeId");

        var gonderim = await gonderen.PostAsync("/api/v1/rewards/fotograf", form);
        gonderim.EnsureSuccessStatusCode();

        var gonderiId = (await gonderim.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").GetProperty("id").GetInt32();

        var onay = await _moderator.PostAsync(
            $"/api/v1/moderation/{gonderiId}/onayla", null);

        onay.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await using var context = fixture.CreateDbContext();
        var yer = await context.Places.AsNoTracking().FirstAsync(x => x.Id == yerId);

        yer.PhotoUrl.ShouldNotBeNullOrWhiteSpace();
        yer.PhotoAuthor.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Onay_kullaniciya_bildirim_birakiyor()
    {
        // Bildirim önce veritabanına yazılıyor; telefona iletim ayrı bir
        // kanal ve teslimi garanti değil. Kayıt durduğu için kullanıcı
        // uygulamayı açtığında olan biteni görüyor — asıl güvence bu.
        var yerId = await FotografsizYerIdAsync();
        var eposta = $"bildirim{Guid.NewGuid():N}@ornek.com";
        var gonderen = await SignedInClientAsync(eposta);

        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(TestPhoto()), "photo", "test.png");
        form.Add(new StringContent(yerId.ToString()), "placeId");

        var gonderim = await gonderen.PostAsync("/api/v1/rewards/fotograf", form);
        gonderim.EnsureSuccessStatusCode();

        var gonderiId = (await gonderim.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").GetProperty("id").GetInt32();

        var oncekiSayi = await OkunmamisSayisiAsync(gonderen);

        var onay = await _moderator.PostAsync($"/api/v1/moderation/{gonderiId}/onayla", null);
        onay.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await OkunmamisSayisiAsync(gonderen)).ShouldBe(oncekiSayi + 1);

        var liste = await gonderen.GetAsync("/api/v1/bildirimler");
        liste.EnsureSuccessStatusCode();

        var bildirimler = (await liste.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data");

        bildirimler.GetArrayLength().ShouldBeGreaterThan(0);

        var ilk = bildirimler[0];
        ilk.GetProperty("kind").GetString().ShouldBe("PhotoApproved");
        ilk.GetProperty("isRead").GetBoolean().ShouldBeFalse();
        // Coin de metne girmiş olmalı; kullanıcı ne kazandığını görmeli.
        ilk.GetProperty("body").GetString().ShouldContain("10");

        // Liste açılınca rozet sıfırlanıyor.
        var okundu = await gonderen.PostAsJsonAsync(
            "/api/v1/bildirimler/okundu", new { ids = (int[]?)null });
        okundu.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await OkunmamisSayisiAsync(gonderen)).ShouldBe(0);
    }

    [Fact]
    public async Task Bildirimler_hesap_istiyor()
    {
        var response = await _client.GetAsync("/api/v1/bildirimler");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static async Task<int> OkunmamisSayisiAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/bildirimler/okunmamis");
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").GetInt32();
    }

    private async Task<int> FotografsizYerIdAsync()
    {
        var liste = await OkuAsync("/api/v1/admin/fotografsiz-yerler?take=1");

        return liste[0].GetProperty("placeId").GetInt32();
    }

    /// <summary>
    /// Küçük ama geçerli bir PNG.
    /// </summary>
    /// <remarks>
    /// Kısa kenar sınırını (640 px) geçmesi gerekiyor; tek renk olduğu için
    /// sıkıştırılınca birkaç kilobayt kalıyor.
    /// </remarks>
    private static byte[] TestPhoto()
    {
        using var image = new Image<Rgba32>(800, 600);
        using var buffer = new MemoryStream();
        image.Save(buffer, new PngEncoder());

        return buffer.ToArray();
    }

    private async Task<JsonElement> OkuAsync(string yol)
    {
        var response = await _moderator.GetAsync(yol);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    }

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
            .GetProperty("data").GetProperty("accessToken").GetString()!;

        return ClientWithToken(token);
    }

    /// <summary>
    /// Moderatör rolü verilmiş bir hesapla giriş yapar.
    /// </summary>
    /// <remarks>
    /// Rol jetona yazıldığı için kayıttan sonra **yeniden giriş** yapmak
    /// şart: kayıt sırasında alınan jeton rolü taşımıyor.
    /// </remarks>
    private async Task<HttpClient> ModeratorClientAsync(string email)
    {
        // Aynı veritabanı bütün testlerde paylaşılıyor; hesap ilk testte
        // açılıyor, sonrakiler onu tekrar kaydetmeye çalışıp 400 alıyor.
        // Var olması sorun değil, girişte kullanılacak.
        var kayit = await _client.PostAsJsonAsync("/api/v1/account/register", new
        {
            email,
            password = Password,
            deviceUuid = Guid.NewGuid()
        });

        if (kayit.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.Created
            or HttpStatusCode.BadRequest or HttpStatusCode.Conflict))
        {
            kayit.EnsureSuccessStatusCode();
        }

        await using (var context = fixture.CreateDbContext())
        {
            var role = await context.Roles.FirstOrDefaultAsync(x => x.Name == ModeratorRole);
            if (role is null)
            {
                role = new IdentityRole<int>
                {
                    Name = ModeratorRole,
                    NormalizedName = ModeratorRole.ToUpperInvariant()
                };

                context.Roles.Add(role);
                await context.SaveChangesAsync();
            }

            var userId = await context.Users
                .Where(x => x.Email == email)
                .Select(x => x.Id)
                .FirstAsync();

            var varMi = await context.UserRoles
                .AnyAsync(x => x.UserId == userId && x.RoleId == role.Id);

            if (!varMi)
            {
                context.UserRoles.Add(new IdentityUserRole<int>
                {
                    UserId = userId,
                    RoleId = role.Id
                });

                await context.SaveChangesAsync();
            }
        }

        var login = await _client.PostAsJsonAsync("/api/v1/account/login", new
        {
            email,
            password = Password
        });

        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").GetProperty("accessToken").GetString()!;

        return ClientWithToken(token);
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        _moderator.Dispose();
        await _api.DisposeAsync();
    }
}
