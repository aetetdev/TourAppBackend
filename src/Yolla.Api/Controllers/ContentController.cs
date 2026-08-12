using Microsoft.AspNetCore.Mvc;
using Yolla.Api.Security;
using Yolla.Application.Common;
using Yolla.Application.Content;

namespace Yolla.Api.Controllers;

/// <summary>
/// İçerik yönetimi.
/// </summary>
/// <remarks>
/// Otomatik kaynaklar 54 bin yerin ancak 3 binine fotoğraf bulabildi; kalanı elle
/// doldurulacak. Bu uçlar kendi çektiğimiz fotoğrafları ve yazdığımız açıklamaları
/// eklemeye yarar.
///
/// Eklenen içerik ayrı bir tabloda tutulur: veri toplayıcı yeniden çalıştığında kendi
/// içeriğimizin üzerine yazmaz ve içeriğin kaynağı her zaman izlenebilir kalır.
///
/// Uçlar <c>X-Admin-Key</c> başlığıyla korunur. Kullanıcı hesapları geldiğinde
/// yönetici rolüne geçilecek.
/// </remarks>
[ApiController]
[Route("api/v1/content")]
[Produces("application/json")]
[AdminKey]
public sealed class ContentController(IContentService contentService) : ControllerBase
{
    /// <summary>İçerik girilmesi en çok işe yarayacak yerleri listeler.</summary>
    /// <remarks>
    /// Kaliteli ama fotoğrafsız kayıtlar önce gelir: bir fotoğraf eklendiğinde doğrudan
    /// kart destesine girerler. Çalışma listesi olarak kullanılır.
    /// </remarks>
    /// <param name="citySlug">Belirli bir şehirle sınırlamak için, örn. <c>kirikkale</c>.</param>
    /// <param name="take">Kaç kayıt döneceği (1-200).</param>
    /// <response code="200">İçerik bekleyen yerler.</response>
    /// <response code="401">Yönetim anahtarı geçersiz.</response>
    /// <response code="503">Yönetim anahtarı yapılandırılmamış.</response>
    [HttpGet("missing")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<MissingContentDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMissing(
        [FromQuery] string? citySlug = null,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var places = await contentService.GetMissingContentAsync(citySlug, take, cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<MissingContentDto>>.Create(places));
    }

    /// <summary>Bir yerin elle eklenmiş içeriklerini listeler.</summary>
    /// <param name="placeId">Yer kimliği.</param>
    /// <response code="200">Katkı listesi.</response>
    [HttpGet("places/{placeId:int}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ContributionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetForPlace(int placeId, CancellationToken cancellationToken)
    {
        var contributions = await contentService.GetForPlaceAsync(placeId, cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<ContributionDto>>.Create(contributions));
    }

    /// <summary>Bir yere fotoğraf, açıklama veya gezme süresi ekler.</summary>
    /// <remarks>
    /// Aynı tür ve dildeki mevcut katkı güncellenir, yeni kayıt açılmaz. Eklenen içerik
    /// yerin gösterilen alanlarına yansır ve kalite puanı yeniden hesaplanır.
    ///
    /// Örnek istek:
    ///
    ///     POST /api/v1/content/places/1234
    ///     {
    ///       "type": "Photo",
    ///       "value": "https://cdn.yolla.travel/kirikkale-kalesi.jpg",
    ///       "author": "Yolla ekibi",
    ///       "license": "Yolla"
    ///     }
    ///
    /// Başkasına ait bir fotoğraf ekleniyorsa fotoğrafçı adı ve gerçek lisans zorunludur;
    /// atıfsız görsel yayınlamak telif ihlalidir.
    /// </remarks>
    /// <param name="placeId">Yer kimliği.</param>
    /// <param name="request">Katkı bilgileri.</param>
    /// <response code="200">Kaydedilen katkı.</response>
    /// <response code="400">Adres geçersiz veya atıf bilgisi eksik.</response>
    /// <response code="404">Yer bulunamadı.</response>
    [HttpPost("places/{placeId:int}")]
    [ProducesResponseType(typeof(ApiResponse<ContributionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Submit(
        int placeId,
        [FromBody] ContributionRequest request,
        CancellationToken cancellationToken)
    {
        var submittedBy = Request.Headers["X-Submitted-By"].ToString();

        var contribution = await contentService.SubmitAsync(
            placeId,
            request,
            string.IsNullOrWhiteSpace(submittedBy) ? null : submittedBy,
            cancellationToken);

        return Ok(ApiResponse<ContributionDto>.Create(contribution));
    }

    /// <summary>Katkıyı yayından kaldırır.</summary>
    /// <remarks>
    /// Kayıt silinmez; yerin içeriği otomatik kaynaklara geri döner ve kalite puanı
    /// yeniden hesaplanır.
    /// </remarks>
    /// <param name="contributionId">Katkı kimliği.</param>
    /// <response code="204">Yayından kaldırıldı.</response>
    /// <response code="404">Katkı bulunamadı.</response>
    [HttpDelete("{contributionId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unpublish(int contributionId, CancellationToken cancellationToken)
    {
        await contentService.UnpublishAsync(contributionId, cancellationToken);

        return NoContent();
    }
}
