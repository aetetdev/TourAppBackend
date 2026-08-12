using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Yolla.Api.Extensions;
using Yolla.Infrastructure.Caching;

namespace Yolla.Api.Middleware;

/// <summary>
/// Sunucular arasında paylaşılan hız sınırı.
/// </summary>
/// <remarks>
/// .NET'in yerleşik sınırlayıcısı yalnızca tek örnek için doğru çalışır; sayaçları
/// bellekte tuttuğu için ikinci bir API örneği açıldığında sınır iki katına çıkar.
/// Bu ara katman sayacı Redis'te tutar. Redis yapılandırılmamışsa devreye girmez ve
/// yerleşik sınırlayıcı iş görmeye devam eder.
/// </remarks>
public sealed class RateLimitingMiddleware(
    RequestDelegate next,
    RedisRateLimiter limiter,
    ILogger<RateLimitingMiddleware> logger)
{
    /// <summary>Yol öneki ve o yola uygulanacak kural.</summary>
    private static readonly (string Prefix, string Policy, int Limit, int WindowSeconds)[] Policies =
    [
        // Kimlik doğrulama en dar sınırda: şifre deneme saldırılarını yavaşlatır
        ("/api/v1/account", "auth", 10, 300),
        // Rota hesabı motora gerçek iş yüklüyor
        ("/api/v1/routes", "routing", 20, 60),
        ("/api/v1/trips", "routing", 40, 60)
    ];

    private const int DefaultLimit = 120;
    private const int DefaultWindowSeconds = 60;

    /// <summary>İsteği sayar ve sınır aşıldıysa 429 döndürür.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        // Sağlık kontrolü ve dokümantasyon sınırlanmaz
        if (IsExempt(context.Request.Path))
        {
            await next(context);
            return;
        }

        var (policy, limit, windowSeconds) = ResolvePolicy(context.Request.Path);

        var decision = await limiter.TryAcquireAsync(
            GetPartitionKey(context), policy, limit, TimeSpan.FromSeconds(windowSeconds));

        context.Response.Headers["X-RateLimit-Limit"] = limit.ToString(CultureInfo.InvariantCulture);
        context.Response.Headers["X-RateLimit-Remaining"] =
            decision.Remaining.ToString(CultureInfo.InvariantCulture);

        if (decision.IsAllowed)
        {
            await next(context);
            return;
        }

        logger.LogInformation("Hız sınırı aşıldı: {Policy} {Path}", policy, context.Request.Path);

        var retryAfter = (int)Math.Ceiling(decision.RetryAfter.TotalSeconds);

        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers.RetryAfter = retryAfter.ToString(CultureInfo.InvariantCulture);
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Çok fazla istek",
            Detail = $"Sınıra ulaşıldı. {retryAfter} saniye sonra tekrar deneyin.",
            Instance = context.Request.Path
        });
    }

    private static bool IsExempt(PathString path) =>
        path.StartsWithSegments("/health")
        || path.StartsWithSegments("/scalar")
        || path.StartsWithSegments("/openapi");

    private static (string Policy, int Limit, int WindowSeconds) ResolvePolicy(PathString path)
    {
        foreach (var (prefix, policy, limit, window) in Policies)
        {
            if (path.StartsWithSegments(prefix))
            {
                return (policy, limit, window);
            }
        }

        return ("general", DefaultLimit, DefaultWindowSeconds);
    }

    /// <summary>
    /// İstemci kimliği: kimliği doğrulanmışsa cihaz, değilse IP adresi.
    /// </summary>
    /// <remarks>
    /// Yalnızca IP kullanılsaydı ortak ağdaki (otel, üniversite) kullanıcılar birbirinin
    /// hakkını tüketirdi - turistik bir uygulamada bu sık karşılaşılacak bir durum.
    /// </remarks>
    private static string GetPartitionKey(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            try
            {
                return $"device:{context.User.GetDeviceId()}";
            }
            catch
            {
                // Jeton cihaz bilgisi taşımıyorsa IP'ye düşülür
            }
        }

        return $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "bilinmeyen"}";
    }
}
