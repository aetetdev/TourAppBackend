using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Yolla.Api.Extensions;
using Yolla.Application.Auth;
using Yolla.Application.Common;

namespace Yolla.Api.Controllers;

/// <summary>
/// Kullanıcı hesabı.
/// </summary>
/// <remarks>
/// Hesap açmak **zorunlu değildir**; uygulama cihaz oturumuyla tam olarak çalışır.
/// Hesabın tek işlevi verileri cihazlar arasında taşımak: telefon değiştiğinde ya da
/// hem telefon hem web kullanıldığında planlar korunur.
///
/// Kayıt ve giriş sırasında mevcut cihaz kimliği gönderilirse, o cihazda anonim olarak
/// oluşturulmuş planlar ve kaydırmalar hesaba bağlanır; kullanıcı hiçbir şey kaybetmez.
/// </remarks>
[ApiController]
[Route("api/v1/account")]
[Produces("application/json")]
public sealed class AccountController(IAccountService accountService) : ControllerBase
{
    /// <summary>Yeni hesap açar.</summary>
    /// <remarks>
    /// Örnek istek:
    ///
    ///     POST /api/v1/account/register
    ///     {
    ///       "email": "gezgin@example.com",
    ///       "password": "cokGizliSifre1",
    ///       "displayName": "Eren",
    ///       "deviceUuid": "8f14e45f-ea4a-4f6b-9d3c-2a1b7c9e5d20"
    ///     }
    ///
    /// <c>deviceUuid</c> gönderilirse o cihazın planları hesaba taşınır.
    /// Dönen jeton cihaz jetonunun yerini alır.
    /// </remarks>
    /// <response code="200">Hesap ve erişim jetonu.</response>
    /// <response code="400">E-posta kullanımda veya şifre kurallara uymuyor.</response>
    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(ApiResponse<AuthResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await accountService.RegisterAsync(request, cancellationToken);

        return Ok(ApiResponse<AuthResultDto>.Create(result));
    }

    /// <summary>Oturum açar.</summary>
    /// <remarks>
    /// Hatalı giriş denemelerinde e-postanın kayıtlı olup olmadığı belli edilmez;
    /// her iki durumda da aynı mesaj döner.
    /// </remarks>
    /// <response code="200">Hesap ve erişim jetonu.</response>
    /// <response code="400">E-posta veya şifre hatalı.</response>
    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(ApiResponse<AuthResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await accountService.LoginAsync(request, cancellationToken);

        return Ok(ApiResponse<AuthResultDto>.Create(result));
    }

    /// <summary>Oturum açmış kullanıcının bilgilerini döndürür.</summary>
    /// <response code="200">Hesap bilgileri.</response>
    /// <response code="401">Jeton yok veya hesaba bağlı değil.</response>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<AccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        var userId = RequireUserId();

        var account = await accountService.GetAsync(userId, cancellationToken);

        return Ok(ApiResponse<AccountDto>.Create(account));
    }

    /// <summary>Şifre değiştirir.</summary>
    /// <response code="204">Şifre değiştirildi.</response>
    /// <response code="400">Mevcut şifre hatalı veya yeni şifre kurallara uymuyor.</response>
    /// <response code="401">Jeton yok veya hesaba bağlı değil.</response>
    [HttpPost("change-password")]
    [Authorize]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        await accountService.ChangePasswordAsync(RequireUserId(), request, cancellationToken);

        return NoContent();
    }

    /// <summary>Hesabı ve tüm kişisel verileri siler.</summary>
    /// <remarks>
    /// **Bu işlem geri alınamaz.** Gezi planları, kaydırmalar ve cihaz kayıtları
    /// birlikte silinir. Doğrulama için mevcut şifre gerekir.
    ///
    /// Uygulama mağazaları, hesap açtıran uygulamaların hesap silmeyi de sunmasını
    /// zorunlu kılar; istemci bu ucu ayarlar ekranında göstermelidir.
    /// </remarks>
    /// <response code="204">Hesap silindi.</response>
    /// <response code="400">Şifre doğrulanamadı.</response>
    /// <response code="401">Jeton yok veya hesaba bağlı değil.</response>
    [HttpPost("delete")]
    [Authorize]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteAccount(
        [FromBody] DeleteAccountRequest request,
        CancellationToken cancellationToken)
    {
        await accountService.DeleteAccountAsync(RequireUserId(), request, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Jetondaki kullanıcı kimliğini döndürür; cihaz jetonu hesaba bağlı değilse reddeder.
    /// </summary>
    private int RequireUserId() =>
        User.GetUserId()
        ?? throw RequestValidationException.Single(
            "account", "Bu işlem için hesap gerekli. Önce giriş yapın.");
}
