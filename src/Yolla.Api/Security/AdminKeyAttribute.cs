using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Yolla.Api.Security;

/// <summary>
/// İçerik yönetimi uçlarını paylaşılan bir anahtarla korur.
/// </summary>
/// <remarks>
/// Kullanıcı hesapları ve rol yönetimi henüz yazılmadı. Bu uçlar veriyi değiştirdiği için
/// açıkta bırakılamaz; geçici olarak <c>X-Admin-Key</c> başlığıyla korunuyor.
/// Hesap sistemi geldiğinde yönetici rolüyle değiştirilecek.
///
/// Anahtar yapılandırmada tanımlı değilse uçlar tamamen kapalıdır - varsayılan bir
/// anahtarla açık kalmasındansa erişilemez olması güvenli.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AdminKeyAttribute : Attribute, IAsyncActionFilter
{
    /// <summary>Anahtarın gönderildiği istek başlığı.</summary>
    public const string HeaderName = "X-Admin-Key";

    /// <summary>İsteği yalnızca geçerli yönetim anahtarıyla geçirir.</summary>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var expected = configuration["Admin:ApiKey"];

        if (string.IsNullOrWhiteSpace(expected))
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "İçerik yönetimi kapalı",
                Detail = "Admin:ApiKey yapılandırılmadığı için bu uçlar devre dışı."
            })
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable
            };

            return;
        }

        var provided = context.HttpContext.Request.Headers[HeaderName].ToString();

        // Sabit süreli karşılaştırma: anahtar uzunluğu üzerinden tahmin yürütmeyi zorlaştırır
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(provided),
                Encoding.UTF8.GetBytes(expected)))
        {
            context.Result = new UnauthorizedResult();

            return;
        }

        await next();
    }
}
