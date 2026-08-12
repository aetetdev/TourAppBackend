using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Yolla.Api.IntegrationTests;

/// <summary>
/// API'yi test veritabanına bağlı olarak ayağa kaldırır.
/// </summary>
/// <remarks>
/// Uygulamanın kendi yapılandırması ezilerek Testcontainers'ın başlattığı PostGIS örneğine
/// yönlendirilir; böylece uçlar gerçek veritabanı ve gerçek PostGIS fonksiyonlarıyla test edilir.
/// Sahte veriyle çalışmak burada işe yaramaz, çünkü sorunların çoğu (geography/geometry
/// dönüşümü gibi) yalnızca gerçek sunucuda ortaya çıkıyor.
/// </remarks>
/// <param name="settings">
/// Teste özel yapılandırma; belirli bir ayarın davranışını sınamak için.
/// </param>
public sealed class ApiFactory(
    string connectionString,
    IReadOnlyDictionary<string, string?>? settings = null) : WebApplicationFactory<Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        var configuration = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = connectionString,
            // Test ortamında Redis kullanılmıyor
            ["ConnectionStrings:Redis"] = null
        };

        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
        {
            configuration[key] = value;
        }

        builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(configuration));

        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureServices(_ => { });
}
