using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yolla.Api.Extensions;
using Yolla.Application.Common;
using Yolla.Application.Places;
using Yolla.Application.Rewards;

namespace Yolla.Api.Controllers;

/// <summary>
/// Fotoğraf gönderilerinin incelenmesi.
/// </summary>
/// <remarks>
/// Yalnızca <c>moderator</c> rolündeki hesaplar erişebilir. Rolü olmayan
/// kullanıcı 403 alır — uç bilerek gizlenmiyor, çünkü varlığını saklamak
/// güvenlik sağlamıyor.
///
/// Onaylanan gönderi <c>PlaceContribution</c> olarak yayına giriyor, gönderene
/// coin yazılıyor ve yerin önbelleği temizleniyor. Reddedilen gönderinin
/// dosyası siliniyor ama kaydı kalıyor.
/// </remarks>
[ApiController]
[Route("api/v1/moderation")]
[Produces("application/json")]
[Authorize(Roles = ModerationController.ModeratorRole)]
public sealed class ModerationController(
    IRewardService rewards,
    IPlaceSuggestionService suggestions) : ControllerBase
{
    internal const string ModeratorRole = "moderator";

    /// <summary>İncelenmeyi bekleyen gönderiler, eskiden yeniye.</summary>
    /// <remarks>
    /// Her satırda gönderenin daha önce kaç gönderisinin onaylandığı ve
    /// reddedildiği de dönüyor: tekrar eden kötüye kullanımı görmek için.
    /// </remarks>
    /// <response code="200">Kuyruk.</response>
    /// <response code="403">Moderatör rolü gerekli.</response>
    [HttpGet("bekleyenler")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ModerationItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPending(
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var items = await rewards.GetPendingAsync(take, cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<ModerationItemDto>>.Create(items));
    }

    /// <summary>Gönderiyi yayına alır ve gönderene coin verir.</summary>
    /// <response code="204">Onaylandı.</response>
    /// <response code="400">Gönderi zaten incelenmiş.</response>
    /// <response code="404">Gönderi yok.</response>
    [HttpPost("{id:int}/onayla")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Approve(int id, CancellationToken cancellationToken)
    {
        await rewards.ApproveAsync(id, RequireUserId(), cancellationToken);

        return NoContent();
    }

    /// <summary>Gönderiyi reddeder. Coin verilmez, dosya silinir.</summary>
    /// <response code="204">Reddedildi.</response>
    /// <response code="400">Sebep boş ya da gönderi zaten incelenmiş.</response>
    [HttpPost("{id:int}/reddet")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Reject(
        int id,
        [FromBody] RejectRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw RequestValidationException.Single(
                "reason", "Red sebebi gerekli; kullanıcıya gösteriliyor.");
        }

        await rewards.RejectAsync(id, RequireUserId(), request.Reason, cancellationToken);

        return NoContent();
    }

    /// <summary>İncelenmeyi bekleyen yer önerileri, eskiden yeniye.</summary>
    /// <remarks>
    /// Her satırda önerinin bir kilometre çevresindeki kayıtlı yerler ve
    /// önerenin daha önce kaç önerisinin onaylandığı da dönüyor: kuyruğun
    /// büyük kısmı tekrar öneri ve bunu ayrı bir ekrana gitmeden görmek
    /// gerekiyor.
    /// </remarks>
    /// <response code="200">Kuyruk.</response>
    /// <response code="403">Moderatör rolü gerekli.</response>
    [HttpGet("yer-onerileri")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PlaceSuggestionModerationDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPendingSuggestions(
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var items = await suggestions.GetPendingAsync(take, cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<PlaceSuggestionModerationDto>>.Create(items));
    }

    /// <summary>Öneriyi kataloğa alır ve önerene coin verir.</summary>
    /// <remarks>
    /// Yaratılan yer fotoğrafsız olduğu için kart destesinin eşiğini geçmiyor;
    /// haritada işaret olarak görünüyor ve fotoğraf katkısını bekliyor.
    /// </remarks>
    /// <response code="204">Onaylandı.</response>
    /// <response code="400">Öneri zaten incelenmiş.</response>
    /// <response code="404">Öneri yok.</response>
    [HttpPost("yer-onerileri/{id:int}/onayla")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ApproveSuggestion(int id, CancellationToken cancellationToken)
    {
        await suggestions.ApproveAsync(id, RequireUserId(), cancellationToken);

        return NoContent();
    }

    /// <summary>Öneriyi reddeder. Coin verilmez.</summary>
    /// <response code="204">Reddedildi.</response>
    /// <response code="400">Sebep boş ya da öneri zaten incelenmiş.</response>
    [HttpPost("yer-onerileri/{id:int}/reddet")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RejectSuggestion(
        int id,
        [FromBody] RejectRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw RequestValidationException.Single(
                "reason", "Red sebebi gerekli; kullanıcıya gösteriliyor.");
        }

        await suggestions.RejectAsync(id, RequireUserId(), request.Reason, cancellationToken);

        return NoContent();
    }

    private int RequireUserId() =>
        User.GetUserId()
        ?? throw RequestValidationException.Single(
            "account", "Bu işlem için hesap gerekli.");
}

/// <summary>Red isteği.</summary>
public sealed class RejectRequest
{
    /// <summary>Kullanıcıya gösterilecek sebep.</summary>
    public string Reason { get; init; } = string.Empty;
}
