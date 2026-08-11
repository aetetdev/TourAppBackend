using System.Globalization;
using System.Security.Claims;
using Yolla.Application.Common;
using Yolla.Infrastructure.Auth;

namespace Yolla.Api.Extensions;

/// <summary>Jetondaki bilgilere erişim.</summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Jetondaki cihaz kimliğini döndürür.
    /// </summary>
    /// <exception cref="RequestValidationException">
    /// Jeton cihaz kimliği taşımıyorsa. Yetkilendirme katmanı geçildiği halde bu olursa
    /// jeton eski bir sürümden gelmiş demektir; istemci yeniden kayıt olmalıdır.
    /// </exception>
    public static int GetDeviceId(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var value = principal.FindFirstValue(JwtTokenGenerator.DeviceIdClaim);

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var deviceId))
        {
            throw RequestValidationException.Single(
                "token", "Jeton cihaz bilgisi taşımıyor, cihazı yeniden kaydedin.");
        }

        return deviceId;
    }

    /// <summary>Cihaz bir hesaba bağlıysa kullanıcı kimliğini döndürür.</summary>
    public static int? GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var userId)
            ? userId
            : null;
    }
}
