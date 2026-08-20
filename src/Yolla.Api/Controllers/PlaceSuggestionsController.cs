using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yolla.Api.Extensions;
using Yolla.Application.Common;
using Yolla.Application.Places;

namespace Yolla.Api.Controllers;

/// <summary>
/// Kullanıcıların önerdiği yerler.
/// </summary>
/// <remarks>
/// Katalog OpenStreetMap'ten geliyor; orada olmayan yer bizde de yok. Köy
/// çeşmesi, yeni açılmış müze, yalnızca yerlinin bildiği manzara noktası bu
/// uçtan giriyor.
///
/// Öneri doğrudan kataloğa girmiyor: moderasyon kuyruğuna düşüyor, onaylanınca
/// gerçek bir yer olarak yaratılıyor ve önerene coin yazılıyor. Onaylanan yer
/// fotoğrafsız başlıyor — haritada işaret olarak görünüyor ve fotoğraf
/// katkısını bekliyor.
/// </remarks>
[ApiController]
[Route("api/v1/places/oneriler")]
[Produces("application/json")]
[Authorize]
public sealed class PlaceSuggestionsController(IPlaceSuggestionService suggestions)
    : ControllerBase
{
    /// <summary>Öneri kurulurken seçilebilecek kategoriler.</summary>
    /// <response code="200">Kategoriler.</response>
    [HttpGet("kategoriler")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SuggestionCategoryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories(
        [FromQuery] string language = "tr",
        CancellationToken cancellationToken = default)
    {
        var items = await suggestions.GetCategoriesAsync(language, cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<SuggestionCategoryDto>>.Create(items));
    }

    /// <summary>Yeni bir yer önerir.</summary>
    /// <remarks>
    /// Konumun düştüğü şehir sunucuda sınırlardan bulunuyor; Türkiye dışındaki
    /// koordinat burada reddediliyor, moderatörün önüne düşmüyor.
    ///
    /// Aynı adla aynı çevrede kayıtlı ya da bekleyen bir yer varsa öneri
    /// alınmıyor: kuyruğun büyük kısmı tekrar önerilerden oluşuyor.
    /// </remarks>
    /// <response code="201">Öneri alındı, incelemeyi bekliyor.</response>
    /// <response code="400">Ad, kategori ya da konum geçersiz; kota dolu; tekrar öneri.</response>
    /// <response code="401">Hesap gerekli.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<PlaceSuggestionDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Suggest(
        [FromBody] CreatePlaceSuggestionRequest request,
        [FromQuery] string language = "tr",
        CancellationToken cancellationToken = default)
    {
        var created = await suggestions.SuggestAsync(
            RequireUserId(),
            User.GetDeviceId(),
            request,
            language,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetMine),
            null,
            ApiResponse<PlaceSuggestionDto>.Create(created));
    }

    /// <summary>Kullanıcının kendi önerileri, yeniden eskiye.</summary>
    /// <response code="200">Öneriler ve durumları.</response>
    /// <response code="401">Hesap gerekli.</response>
    [HttpGet("benim")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PlaceSuggestionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine(
        [FromQuery] string language = "tr",
        CancellationToken cancellationToken = default)
    {
        var items = await suggestions.GetMineAsync(RequireUserId(), language, cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<PlaceSuggestionDto>>.Create(items));
    }

    private int RequireUserId() =>
        User.GetUserId()
        ?? throw RequestValidationException.Single(
            "account", "Bu işlem için hesap gerekli.");
}
