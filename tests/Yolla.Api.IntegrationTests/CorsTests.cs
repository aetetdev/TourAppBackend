using System.Net;
using Shouldly;

namespace Yolla.Api.IntegrationTests;

/// <summary>
/// Tarayıcıdan çalışan web istemcisinin isteklerinin geçtiğini doğrular.
/// </summary>
/// <remarks>
/// CORS ayarı bozulduğunda mobil istemci hiçbir şey hissetmez, web istemcisi ise tamamen
/// çalışmaz durumdan çıkar; üstelik tarayıcı hatası asıl sebebi göstermez. Bu yüzden
/// davranış testle sabitlendi.
/// </remarks>
[Collection(PostgisCollection.Name)]
public class CorsTests(PostgisFixture fixture)
{
    private const string CorsHeader = "Access-Control-Allow-Origin";

    [Theory]
    [InlineData("http://localhost:64321")]
    [InlineData("http://127.0.0.1:8080")]
    public async Task Gelistirmede_yerel_adreslere_izin_verilir(string origin)
    {
        // `flutter run -d chrome` her çalıştırmada başka bir port seçiyor; adresleri
        // tek tek yazmak yerine tüm yerel adresler açık
        await using var api = new ApiFactory(fixture.ConnectionString);
        using var client = api.CreateClient();

        var response = await SendAsync(client, HttpMethod.Get, "/api/v1/geo/countries", origin);

        response.Headers.GetValues(CorsHeader).ShouldContain(origin);
    }

    [Fact]
    public async Task On_kontrol_istegi_yanitlanir()
    {
        // Ön kontrol jeton taşımaz; kimlik doğrulama ya da hız sınırı buna takılırsa
        // tarayıcı asıl isteği hiç göndermez
        await using var api = new ApiFactory(fixture.ConnectionString);
        using var client = api.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/discovery/swipes");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        response.Headers.GetValues(CorsHeader).ShouldContain("http://localhost:5173");
        response.Headers.GetValues("Access-Control-Allow-Methods").ShouldContain("POST");
    }

    [Fact]
    public async Task Yapilandirma_verildiginde_yalnizca_o_adres_gecer()
    {
        // Üretimdeki yol: Cors__AllowedOrigins__0 ile verilen adres dışında hiçbiri
        await using var api = new ApiFactory(fixture.ConnectionString, new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins:0"] = "https://yolla.travel"
        });

        using var client = api.CreateClient();

        var allowed = await SendAsync(client, HttpMethod.Get, "/api/v1/geo/countries", "https://yolla.travel");
        var rejected = await SendAsync(client, HttpMethod.Get, "/api/v1/geo/countries", "https://kotu.example");

        allowed.Headers.GetValues(CorsHeader).ShouldContain("https://yolla.travel");
        rejected.Headers.Contains(CorsHeader).ShouldBeFalse();
    }

    [Fact]
    public async Task Yapilandirma_verildiginde_yerel_adres_de_kapanir()
    {
        // Yerel adres muafiyeti yalnızca yapılandırma boşken geçerli; aksi halde
        // üretimde açık bir kapı kalırdı
        await using var api = new ApiFactory(fixture.ConnectionString, new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins:0"] = "https://yolla.travel"
        });

        using var client = api.CreateClient();

        var response = await SendAsync(client, HttpMethod.Get, "/api/v1/geo/countries", "http://localhost:64321");

        response.Headers.Contains(CorsHeader).ShouldBeFalse();
    }

    [Fact]
    public async Task Adresin_sonundaki_egik_cizgi_eslesmeyi_bozmaz()
    {
        // Tarayıcı Origin başlığını "https://yolla.travel" biçiminde gönderir; yapılandırmada
        // kalan bir "/" hiçbir uyarı vermeden tüm istekleri düşürürdü
        await using var api = new ApiFactory(fixture.ConnectionString, new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins:0"] = "https://yolla.travel/"
        });

        using var client = api.CreateClient();

        var response = await SendAsync(client, HttpMethod.Get, "/api/v1/geo/countries", "https://yolla.travel");

        response.Headers.GetValues(CorsHeader).ShouldContain("https://yolla.travel");
    }

    [Fact]
    public async Task Bos_yapilandirma_degeri_tanimsiz_sayilir()
    {
        // Tanımsız ortam değişkeni (Cors__AllowedOrigins__0=) boş dizge olarak geliyor;
        // süzülmezse "adres verilmiş" sanılır ve web istemcisi tamamen kapanırdı
        await using var api = new ApiFactory(fixture.ConnectionString, new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins:0"] = ""
        });

        using var client = api.CreateClient();

        var response = await SendAsync(client, HttpMethod.Get, "/api/v1/geo/countries", "http://localhost:64321");

        response.Headers.GetValues(CorsHeader).ShouldContain("http://localhost:64321");
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        string origin)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Origin", origin);

        return await client.SendAsync(request);
    }
}
