using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Yolla.Infrastructure.Routing;

namespace Yolla.Infrastructure.HealthChecks;

/// <summary>
/// Redis bağlantısını denetler.
/// </summary>
/// <remarks>
/// Önbellek çökerse ürün çalışmaya devam eder, yalnızca yavaşlar. Bu yüzden sonuç
/// "sağlıksız" değil "uyarı" olarak bildirilir: yük dengeleyici örneği trafikten
/// çıkarmamalı, ama ekip durumdan haberdar olmalı.
/// </remarks>
public sealed class RedisHealthCheck(IConnectionMultiplexer connection) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var latency = await connection.GetDatabase().PingAsync();

            return HealthCheckResult.Healthy(
                $"Redis yanıt veriyor ({latency.TotalMilliseconds:N0} ms)");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Degraded("Redis erişilemiyor; önbellek devre dışı.", exception);
        }
    }
}

/// <summary>
/// Rota motorlarını denetler.
/// </summary>
/// <remarks>
/// OSRM çökerse rota ve koridor uçları çalışmaz ama kart destesi, yer detayı ve plan
/// yönetimi çalışmaya devam eder. Bu yüzden sonuç "uyarı" seviyesinde tutulur.
/// </remarks>
public sealed class OsrmHealthCheck(HttpClient httpClient, IOptions<OsrmOptions> options) : IHealthCheck
{
    private readonly OsrmOptions _options = options.Value;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var failures = new List<string>();

        foreach (var (name, baseUrl, profile) in new[]
                 {
                     ("araç", _options.CarBaseUrl, "driving"),
                     ("yürüme", _options.FootBaseUrl, "foot")
                 })
        {
            // İstanbul'da çok kısa bir rota: motorun ayakta ve grafiğin yüklü olduğunu gösterir
            var url = $"{baseUrl.TrimEnd('/')}/route/v1/{profile}/28.9784,41.0082;28.9800,41.0090?overview=false";

            try
            {
                using var response = await httpClient.GetAsync(url, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    failures.Add($"{name}: HTTP {(int)response.StatusCode}");
                }
            }
            catch (Exception exception)
            {
                failures.Add($"{name}: {exception.GetType().Name}");
            }
        }

        return failures.Count == 0
            ? HealthCheckResult.Healthy("Rota motorları yanıt veriyor")
            : HealthCheckResult.Degraded($"Rota motoru sorunlu — {string.Join(", ", failures)}");
    }
}
