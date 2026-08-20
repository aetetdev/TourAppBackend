using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yolla.Api.Extensions;
using Yolla.Application.Common;
using Yolla.Application.Notifications;

namespace Yolla.Api.Controllers;

/// <summary>
/// Kullanıcının bildirimleri.
/// </summary>
/// <remarks>
/// Bildirim önce veritabanına yazılıyor, telefona iletim ayrı bir kanal.
/// Push teslimi garanti değil — izin reddedilebilir, telefon kapalı olabilir,
/// jeton geçersizleşebilir — ama kayıt burada durduğu için kullanıcı
/// uygulamayı açtığında olan biteni görüyor.
/// </remarks>
[ApiController]
[Route("api/v1/bildirimler")]
[Produces("application/json")]
[Authorize]
public sealed class NotificationsController(INotificationService notifications) : ControllerBase
{
    /// <summary>Bildirimler, yeniden eskiye.</summary>
    /// <response code="200">Bildirimler.</response>
    /// <response code="401">Hesap gerekli.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<NotificationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine(
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var liste = await notifications.GetMineAsync(RequireUserId(), take, cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<NotificationDto>>.Create(liste));
    }

    /// <summary>Okunmamış bildirim sayısı; sekmedeki rozet.</summary>
    /// <response code="200">Sayı.</response>
    [HttpGet("okunmamis")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
    {
        var sayi = await notifications.GetUnreadCountAsync(RequireUserId(), cancellationToken);

        return Ok(ApiResponse<int>.Create(sayi));
    }

    /// <summary>Bildirimleri okundu işaretler.</summary>
    /// <remarks>
    /// Kimlik verilmezse hepsi okundu sayılıyor: kullanıcı listeyi açtığında
    /// rozetin sıfırlanması beklenen davranış.
    /// </remarks>
    /// <response code="204">İşaretlendi.</response>
    [HttpPost("okundu")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkRead(
        [FromBody] MarkReadRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        await notifications.MarkReadAsync(
            RequireUserId(), request?.Ids, cancellationToken);

        return NoContent();
    }

    private int RequireUserId() =>
        User.GetUserId()
        ?? throw RequestValidationException.Single(
            "account", "Bu işlem için hesap gerekli.");
}

/// <summary>Okundu işaretleme isteği.</summary>
public sealed class MarkReadRequest
{
    /// <summary>Boş bırakılırsa bütün bildirimler okundu sayılıyor.</summary>
    public IReadOnlyList<int>? Ids { get; init; }
}
