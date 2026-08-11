using Microsoft.AspNetCore.Mvc;
using Yolla.Application.Common;
using Yolla.Application.Discovery;

namespace Yolla.Api.Controllers;

/// <summary>
/// Kart destesi.
/// </summary>
/// <remarks>
/// Uygulamanın çekirdeği: kullanıcının sağa/sola kaydırdığı kartlar bu uçtan gelir.
///
/// Sıralama kalite puanına göredir; puan, yerin fotoğrafı, Wikipedia makalesi, Wikidata
/// kaydı ve kategorisinden hesaplanır. Ham sıralama tek kategoriye yığıldığı için
/// (Türkiye verisinde camiler ve manzara noktaları sayıca baskın) aynı kategoriden
/// art arda en fazla iki kart gösterilir.
///
/// Fotoğrafı veya fotoğraf atıf bilgisi olmayan yerler kart olarak dönmez: Wikimedia
/// görsellerinin çoğu CC BY-SA lisanslı ve fotoğrafçı adının gösterilmesi zorunludur.
/// </remarks>
[ApiController]
[Route("api/v1/discovery")]
[Produces("application/json")]
public sealed class DiscoveryController(IDiscoveryService discoveryService) : ControllerBase
{
    /// <summary>Bir şehrin kart destesini döndürür.</summary>
    /// <param name="cityId">Şehir kimliği.</param>
    /// <param name="cursor">
    /// Sonraki sayfa imleci. İlk istekte boş bırakılır; yanıttaki <c>nextCursor</c>
    /// değeri bir sonraki istekte gönderilir. Sayfa numarası kullanılmaz, çünkü araya
    /// giren değişiklikler kart atlanmasına yol açardı.
    /// </param>
    /// <param name="take">Kaç kart döneceği (1-50). Varsayılan 20.</param>
    /// <param name="categories">
    /// Virgülle ayrılmış kategori anahtarları, örn. <c>castle,museum,beach</c>.
    /// Verilmezse tüm kategoriler.
    /// </param>
    /// <param name="deviceId">
    /// Cihaz kimliği. Verilirse daha önce kaydırılmış yerler tekrar gösterilmez.
    /// </param>
    /// <param name="language">İçerik dili (<c>tr</c> veya <c>en</c>). Varsayılan <c>tr</c>.</param>
    /// <response code="200">Kart listesi ve sonraki sayfa imleci.</response>
    /// <response code="404">Şehir bulunamadı.</response>
    [HttpGet("city/{cityId:int}/feed")]
    [ProducesResponseType(typeof(ApiResponse<CursorPage<PlaceCardDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCityFeed(
        int cityId,
        [FromQuery] string? cursor = null,
        [FromQuery] int take = 20,
        [FromQuery] string? categories = null,
        [FromQuery] int? deviceId = null,
        [FromQuery] string language = "tr",
        CancellationToken cancellationToken = default)
    {
        var request = new CityFeedRequest
        {
            CityId = cityId,
            Cursor = cursor,
            Take = take,
            CategoryKeys = ParseCategories(categories),
            DeviceId = deviceId,
            Language = language
        };

        var page = await discoveryService.GetCityFeedAsync(request, cancellationToken);

        return Ok(ApiResponse<CursorPage<PlaceCardDto>>.Create(page, Attribution.Places));
    }

    private static IReadOnlyList<string>? ParseCategories(string? categories)
    {
        if (string.IsNullOrWhiteSpace(categories))
        {
            return null;
        }

        var keys = categories
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToLowerInvariant())
            .Distinct()
            .ToList();

        return keys.Count == 0 ? null : keys;
    }
}
