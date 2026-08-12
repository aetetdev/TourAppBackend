using System.Globalization;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Yolla.Infrastructure.Caching;

/// <summary>
/// Sunucular arasında paylaşılan istek sayacı.
/// </summary>
/// <remarks>
/// .NET'in yerleşik hız sınırlayıcısı sayaçları bellekte tutar. İki API örneği
/// çalıştırıldığında her biri kendi sayacını tuttuğu için toplam sınır ikiye katlanır -
/// yani sınır fiilen uygulanmaz. Bu sınıf sayacı Redis'te tutarak sorunu çözer.
///
/// Redis erişilemezse istek <b>reddedilmez</b>: hız sınırı bir koruma önlemi, hizmetin
/// kendisi değil. Önbellek çöktüğü için ürünün durması daha büyük zarar olurdu.
/// </remarks>
public sealed class RedisRateLimiter(
    IConnectionMultiplexer connection,
    ILogger<RedisRateLimiter> logger)
{
    private const string KeyPrefix = "yolla:rate:";

    /// <summary>
    /// Sayacı artırır ve sınırın aşılıp aşılmadığını döndürür.
    /// </summary>
    /// <param name="partitionKey">İstemci kimliği (kullanıcı, cihaz ya da IP).</param>
    /// <param name="policyName">Kural adı; her kuralın kendi sayacı olur.</param>
    /// <param name="permitLimit">Pencere başına izin verilen istek sayısı.</param>
    /// <param name="window">Zaman penceresi.</param>
    public async Task<RateLimitDecision> TryAcquireAsync(
        string partitionKey,
        string policyName,
        int permitLimit,
        TimeSpan window)
    {
        var key = $"{KeyPrefix}{policyName}:{partitionKey}";

        try
        {
            var database = connection.GetDatabase();

            var count = await database.StringIncrementAsync(key);

            // İlk istekte pencere başlatılır; sonraki istekler süreyi uzatmaz,
            // aksi halde sürekli istek gönderen istemci hiç sıfırlanmazdı
            if (count == 1)
            {
                await database.KeyExpireAsync(key, window);
            }

            if (count <= permitLimit)
            {
                return RateLimitDecision.Allowed((int)(permitLimit - count));
            }

            var ttl = await database.KeyTimeToLiveAsync(key) ?? window;

            return RateLimitDecision.Rejected(ttl);
        }
        catch (Exception exception) when (exception is RedisException or ObjectDisposedException)
        {
            logger.LogWarning(exception, "Hız sınırı sayacı okunamadı: {Policy}", policyName);

            // Sayaç çalışmıyorsa istek geçirilir
            return RateLimitDecision.Allowed(permitLimit);
        }
    }

    /// <summary>Kalan hakkı okur; sayacı artırmaz.</summary>
    public async Task<long> GetUsageAsync(string partitionKey, string policyName)
    {
        try
        {
            var value = await connection.GetDatabase()
                .StringGetAsync($"{KeyPrefix}{policyName}:{partitionKey}");

            return value.HasValue && long.TryParse(value.ToString(), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var count)
                ? count
                : 0;
        }
        catch (Exception exception) when (exception is RedisException or ObjectDisposedException)
        {
            return 0;
        }
    }
}

/// <summary>Hız sınırı kararı.</summary>
public readonly record struct RateLimitDecision(bool IsAllowed, int Remaining, TimeSpan RetryAfter)
{
    public static RateLimitDecision Allowed(int remaining) => new(true, remaining, TimeSpan.Zero);

    public static RateLimitDecision Rejected(TimeSpan retryAfter) => new(false, 0, retryAfter);
}
