using Microsoft.AspNetCore.Mvc;
using Yolla.Application.Auth;
using Yolla.Application.Common;

namespace Yolla.Api.Controllers;

/// <summary>
/// Cihaz oturumu.
/// </summary>
/// <remarks>
/// Kullanıcı hesap açmadan uygulamayı kullanabilir. İstemci ilk açılışta bir cihaz kimliği
/// üretip saklar ve bu uçtan jeton alır; kaydırmalar ve gezi planları o cihaza bağlanır.
/// Kullanıcı sonradan hesap açarsa aynı cihaz hesaba bağlanır ve geçmiş planlar korunur.
/// </remarks>
[ApiController]
[Route("api/v1/devices")]
[Produces("application/json")]
public sealed class DevicesController(IDeviceSessionService deviceSessionService) : ControllerBase
{
    /// <summary>Cihazı kaydeder ve erişim jetonu döndürür.</summary>
    /// <remarks>
    /// Aynı cihaz kimliğiyle tekrar çağrılabilir: yeni kayıt açılmaz, mevcut oturum tazelenir.
    /// Dönen jeton sonraki isteklerde <c>Authorization: Bearer &lt;token&gt;</c> başlığında gönderilir.
    ///
    /// Örnek istek:
    ///
    ///     POST /api/v1/devices/register
    ///     {
    ///       "deviceUuid": "8f14e45f-ea4a-4f6b-9d3c-2a1b7c9e5d20",
    ///       "platform": "ios",
    ///       "appVersion": "1.0.0",
    ///       "language": "tr"
    ///     }
    /// </remarks>
    /// <response code="200">Cihaz oturumu ve jeton.</response>
    /// <response code="400">Cihaz kimliği geçersiz veya platform desteklenmiyor.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<DeviceSessionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(
        [FromBody] DeviceRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        var session = await deviceSessionService.RegisterAsync(request, cancellationToken);

        return Ok(ApiResponse<DeviceSessionDto>.Create(session));
    }
}
