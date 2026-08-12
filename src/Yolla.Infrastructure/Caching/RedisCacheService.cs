using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Yolla.Application.Common;

namespace Yolla.Infrastructure.Caching;

/// <inheritdoc cref="ICacheService"/>
public sealed class RedisCacheService(
    IConnectionMultiplexer connection,
    ILogger<RedisCacheService> logger) : ICacheService
{
    /// <summary>Anahtar öneki: aynı Redis örneği başka uygulamalarla paylaşılabilir.</summary>
    private const string KeyPrefix = "yolla:";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        TimeSpan duration,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);

        var fullKey = KeyPrefix + key;

        try
        {
            var cached = await GetDatabase().StringGetAsync(fullKey);

            if (cached.HasValue)
            {
                var value = JsonSerializer.Deserialize<T>(cached.ToString(), SerializerOptions);

                if (value is not null)
                {
                    return value;
                }
            }
        }
        catch (Exception exception) when (IsCacheFailure(exception))
        {
            // Önbellek erişilemezse istek düşmemeli; veri kaynağından okunur
            logger.LogWarning(exception, "Önbellek okunamadı: {Key}", key);
        }

        var produced = await factory(cancellationToken);

        await SetAsync(fullKey, produced, duration, key);

        return produced;
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await GetDatabase().KeyDeleteAsync(KeyPrefix + key);
        }
        catch (Exception exception) when (IsCacheFailure(exception))
        {
            logger.LogWarning(exception, "Önbellek anahtarı silinemedi: {Key}", key);
        }
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        try
        {
            var pattern = $"{KeyPrefix}{prefix}*";

            foreach (var endpoint in connection.GetEndPoints())
            {
                var server = connection.GetServer(endpoint);

                if (!server.IsConnected || server.IsReplica)
                {
                    continue;
                }

                // KEYS yerine SCAN: KEYS tüm veritabanını tarayana kadar sunucuyu kilitler
                await foreach (var key in server.KeysAsync(pattern: pattern, pageSize: 250)
                                   .WithCancellation(cancellationToken))
                {
                    await GetDatabase().KeyDeleteAsync(key);
                }
            }
        }
        catch (Exception exception) when (IsCacheFailure(exception))
        {
            logger.LogWarning(exception, "Önbellek öneki temizlenemedi: {Prefix}", prefix);
        }
    }

    private async Task SetAsync<T>(string fullKey, T value, TimeSpan duration, string logKey)
    {
        try
        {
            var payload = JsonSerializer.Serialize(value, SerializerOptions);

            await GetDatabase().StringSetAsync(fullKey, payload, duration);
        }
        catch (Exception exception) when (IsCacheFailure(exception))
        {
            logger.LogWarning(exception, "Önbelleğe yazılamadı: {Key}", logKey);
        }
    }

    private IDatabase GetDatabase() => connection.GetDatabase();

    /// <summary>
    /// Önbellekten kaynaklanan, isteği düşürmemesi gereken hatalar.
    /// </summary>
    private static bool IsCacheFailure(Exception exception) =>
        exception is RedisException
            or RedisTimeoutException
            or RedisConnectionException
            or ObjectDisposedException
            or JsonException;
}

/// <summary>
/// Redis yapılandırılmadığında kullanılan önbellek: her istek doğrudan veri kaynağına gider.
/// </summary>
/// <remarks>
/// Geliştirmede Redis çalıştırmak zorunda kalmamak için. Uygulamanın davranışı aynı
/// kalır, yalnızca yavaşlar.
/// </remarks>
public sealed class NoOpCacheService : ICacheService
{
    public Task<T> GetOrCreateAsync<T>(
        string key,
        TimeSpan duration,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken = default) => factory(cancellationToken);

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
