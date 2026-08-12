using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Yolla.Api.IntegrationTests;

[Collection(PostgisCollection.Name)]
public class ContentEndpointTests(PostgisFixture fixture) : IAsyncLifetime
{
    private const string AdminKey = "gelistirme-icerik-anahtari";

    private ApiFactory _api = null!;
    private HttpClient _client = null!;
    private TestData.SeededCity _city = null!;

    public async Task InitializeAsync()
    {
        _api = new ApiFactory(fixture.ConnectionString);
        _city = await TestData.EnsureCityWithPlacesAsync(fixture, "icerik-sehri", 180);

        _client = _api.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Admin-Key", AdminKey);
    }

    [Fact]
    public async Task Anahtarsiz_erisim_reddedilir()
    {
        // İçerik uçları veriyi değiştiriyor; açıkta bırakılamaz
        using var anonymous = _api.CreateClient();

        var response = await anonymous.GetAsync("/api/v1/content/missing");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Yanlis_anahtar_reddedilir()
    {
        using var wrong = _api.CreateClient();
        wrong.DefaultRequestHeaders.Add("X-Admin-Key", "yanlis-anahtar");

        var response = await wrong.GetAsync("/api/v1/content/missing");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Fotograf_eklenince_yer_karta_hazir_hale_gelir()
    {
        var placeId = await CreatePlaceWithoutPhotoAsync("Fotografsiz Test Yeri");

        var response = await SubmitAsync(placeId, new
        {
            type = "Photo",
            value = "https://cdn.yolla.travel/test.jpg",
            author = "Yolla ekibi",
            license = "Yolla"
        });

        response.EnsureSuccessStatusCode();

        await using var context = fixture.CreateDbContext();
        var place = await context.Places.FirstAsync(x => x.Id == placeId);

        place.PhotoUrl.ShouldBe("https://cdn.yolla.travel/test.jpg");
        place.PhotoAuthor.ShouldBe("Yolla ekibi");
        place.PhotoLicense.ShouldBe("Yolla");
        // Fotoğraf 25 puan getiriyor; kayıt artık feed eşiğini geçmeli
        place.QualityScore.ShouldBeGreaterThanOrEqualTo((short)25);
    }

    [Fact]
    public async Task Aciklama_eklenir()
    {
        var placeId = await CreatePlaceWithoutPhotoAsync("Aciklamasiz Test Yeri");

        await SubmitAsync(placeId, new
        {
            type = "Description",
            value = "Elle yazılmış açıklama metni.",
            language = "tr"
        });

        await using var context = fixture.CreateDbContext();
        var place = await context.Places.FirstAsync(x => x.Id == placeId);

        place.DescriptionTr.ShouldBe("Elle yazılmış açıklama metni.");
    }

    [Fact]
    public async Task Ayni_tur_tekrar_gonderilince_yeni_kayit_acilmaz()
    {
        var placeId = await CreatePlaceWithoutPhotoAsync("Guncelleme Test Yeri");

        await SubmitAsync(placeId, new { type = "Photo", value = "https://cdn.yolla.travel/ilk.jpg" });
        await SubmitAsync(placeId, new { type = "Photo", value = "https://cdn.yolla.travel/ikinci.jpg" });

        await using var context = fixture.CreateDbContext();

        (await context.PlaceContributions.CountAsync(x => x.PlaceId == placeId)).ShouldBe(1);
        (await context.Places.FirstAsync(x => x.Id == placeId)).PhotoUrl
            .ShouldBe("https://cdn.yolla.travel/ikinci.jpg");
    }

    [Fact]
    public async Task Gecersiz_fotograf_adresi_reddedilir()
    {
        var placeId = await CreatePlaceWithoutPhotoAsync("Gecersiz Adres Yeri");

        var response = await SubmitAsync(placeId, new { type = "Photo", value = "sadece-metin" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Baskasinin_fotografinda_fotografci_adi_zorunlu()
    {
        // Atıfsız görsel yayınlamak telif ihlali
        var placeId = await CreatePlaceWithoutPhotoAsync("Atif Test Yeri");

        var response = await SubmitAsync(placeId, new
        {
            type = "Photo",
            value = "https://example.com/baskasinin.jpg",
            license = "CC BY-SA 4.0"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Gecersiz_gezme_suresi_reddedilir()
    {
        var placeId = await CreatePlaceWithoutPhotoAsync("Sure Test Yeri");

        var response = await SubmitAsync(placeId, new { type = "VisitDuration", value = "9999" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Katki_yayindan_kaldirilinca_yer_eski_haline_doner()
    {
        var placeId = await CreatePlaceWithoutPhotoAsync("Geri_Alma Test Yeri");

        var submitResponse = await SubmitAsync(placeId, new
        {
            type = "Photo",
            value = "https://cdn.yolla.travel/kaldirilacak.jpg"
        });

        var contributionId = (await submitResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").GetProperty("id").GetInt32();

        var deleteResponse = await _client.DeleteAsync($"/api/v1/content/{contributionId}");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await using var context = fixture.CreateDbContext();
        var place = await context.Places.FirstAsync(x => x.Id == placeId);

        place.PhotoUrl.ShouldBeNull();

        // Kayıt silinmiyor, yalnızca yayından kalkıyor
        var contribution = await context.PlaceContributions.FirstAsync(x => x.Id == contributionId);
        contribution.IsPublished.ShouldBeFalse();
    }

    [Fact]
    public async Task Eksik_icerik_listesi_kaliteliden_baslar()
    {
        var response = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/content/missing?citySlug={_city.CitySlug}&take=20");

        var items = response.GetProperty("data").EnumerateArray().ToList();

        if (items.Count > 1)
        {
            var scores = items.Select(x => x.GetProperty("qualityScore").GetInt16()).ToList();
            scores.ShouldBe(scores.OrderByDescending(x => x));
        }
    }

    [Fact]
    public async Task Bir_yerin_katkilari_listelenir()
    {
        var placeId = await CreatePlaceWithoutPhotoAsync("Listeleme Test Yeri");

        await SubmitAsync(placeId, new { type = "Photo", value = "https://cdn.yolla.travel/a.jpg" });
        await SubmitAsync(placeId, new { type = "Description", value = "Açıklama." });

        var response = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/content/places/{placeId}");

        response.GetProperty("data").GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task Olmayan_yere_icerik_eklenemez()
    {
        var response = await SubmitAsync(99999999, new
        {
            type = "Photo",
            value = "https://cdn.yolla.travel/test.jpg"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private Task<HttpResponseMessage> SubmitAsync(int placeId, object body) =>
        _client.PostAsJsonAsync($"/api/v1/content/places/{placeId}", body);

    private async Task<int> CreatePlaceWithoutPhotoAsync(string name)
    {
        await using var context = fixture.CreateDbContext();

        var existing = await context.Places.FirstOrDefaultAsync(x => x.Name == name);

        if (existing is not null)
        {
            return existing.Id;
        }

        var categoryId = await context.Categories.Where(x => x.Key == "museum")
            .Select(x => x.Id).FirstAsync();

        var place = new Domain.Entities.Place
        {
            CountryId = 1,
            CityId = _city.CityId,
            CategoryId = categoryId,
            OsmType = Domain.Enums.OsmElementType.Node,
            OsmId = Random.Shared.NextInt64(500_000, 600_000),
            Name = name,
            Slug = name.ToLowerInvariant().Replace(' ', '-'),
            Location = TestData.Factory.CreatePoint(new NetTopologySuite.Geometries.Coordinate(181, 1)),
            QualityScore = 10,
            IsActive = true
        };

        context.Places.Add(place);
        await context.SaveChangesAsync();

        return place.Id;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _api.DisposeAsync();
    }
}
