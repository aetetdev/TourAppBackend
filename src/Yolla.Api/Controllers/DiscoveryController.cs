using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yolla.Api.Extensions;
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
public sealed class DiscoveryController(
    IDiscoveryService discoveryService,
    ISwipeService swipeService) : ControllerBase
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
    /// Jetonla istek yapılıyorsa bu parametreye gerek yoktur; cihaz jetondan okunur.
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
            // Jeton varsa cihaz oradan okunur; sorgu parametresi yalnızca jetonsuz
            // kullanım (web önizleme, test) için geçerlidir
            DeviceId = ResolveDeviceId(deviceId),
            Language = language
        };

        var page = await discoveryService.GetCityFeedAsync(request, cancellationToken);

        return Ok(ApiResponse<CursorPage<PlaceCardDto>>.Create(page, Attribution.Places));
    }

    /// <summary>Kart kaydırmalarını kaydeder.</summary>
    /// <remarks>
    /// Kaydırmalar iki işe yarar: aynı yerin tekrar gösterilmemesi ve önerilerin
    /// kişiselleştirilmesi. Aynı yer daha önce kaydırılmışsa yön güncellenir, yeni kayıt
    /// açılmaz - kullanıcı fikrini değiştirebilir.
    ///
    /// Çevrimdışı biriken kaydırmalar tek istekte toplu gönderilebilir (en fazla 200 adet).
    ///
    /// Örnek istek:
    ///
    ///     POST /api/v1/discovery/swipes
    ///     {
    ///       "swipes": [
    ///         { "placeId": 93, "direction": "Like", "context": "City" },
    ///         { "placeId": 94, "direction": "Pass", "context": "City" }
    ///       ]
    ///     }
    /// </remarks>
    /// <response code="200">Kaydedilen kaydırma sayısı ve toplam beğeni.</response>
    /// <response code="400">Liste boş, çok uzun veya yerler bulunamadı.</response>
    /// <response code="401">Cihaz jetonu gerekli.</response>
    [HttpPost("swipes")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<SwipeResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RecordSwipes(
        [FromBody] SwipeBatchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await swipeService.RecordAsync(
            User.GetDeviceId(), request.Swipes, cancellationToken);

        return Ok(ApiResponse<SwipeResultDto>.Create(result));
    }

    /// <summary>Bir kaydırmayı geri alır.</summary>
    /// <remarks>
    /// Kullanıcı yanlışlıkla kaydırdığında dönüş yolu: kayıt silinir ve yer kart
    /// destesine geri döner. Beğeniyse beğeni listesinden de çıkar.
    ///
    /// Kayıt bulunamazsa hata dönmez, <c>removed: false</c> ile başarılı yanıt verilir.
    /// Kaydırmalar toplu gönderildiği için istemci, henüz gönderilmemiş bir kaydırma
    /// için de bu ucu çağırabilir; bunu hata saymak istemciyi 404'ü başarı gibi ele
    /// almaya zorlardı.
    ///
    /// Yalnızca yön değiştirmek için bu uca gerek yok: aynı yeri farklı yönle tekrar
    /// göndermek kaydı günceller.
    /// </remarks>
    /// <param name="placeId">Kaydırması geri alınacak yerin kimliği.</param>
    /// <response code="200">Kaydın silinip silinmediği ve güncel beğeni sayısı.</response>
    /// <response code="401">Cihaz jetonu gerekli.</response>
    [HttpDelete("swipes/{placeId:int}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<SwipeUndoResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UndoSwipe(
        int placeId,
        CancellationToken cancellationToken)
    {
        var result = await swipeService.UndoAsync(User.GetDeviceId(), placeId, cancellationToken);

        return Ok(ApiResponse<SwipeUndoResultDto>.Create(result));
    }

    /// <summary>Cihazın beğendiği yerleri döndürür.</summary>
    /// <remarks>
    /// Kullanıcının sağa kaydırdığı yerler, en son beğenilen başta olacak şekilde.
    /// Rota oluştururken bu liste kullanılır.
    /// </remarks>
    /// <param name="language">İçerik dili (<c>tr</c> veya <c>en</c>).</param>
    /// <response code="200">Beğenilen yerler.</response>
    /// <response code="401">Cihaz jetonu gerekli.</response>
    [HttpGet("swipes/liked")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PlaceCardDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetLikedPlaces(
        [FromQuery] string language = "tr",
        CancellationToken cancellationToken = default)
    {
        var places = await swipeService.GetLikedPlacesAsync(
            User.GetDeviceId(), language, cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<PlaceCardDto>>.Create(places, Attribution.Places));
    }

    /// <summary>
    /// Cihazı belirler: jeton varsa oradan, yoksa sorgu parametresinden.
    /// </summary>
    private int? ResolveDeviceId(int? fromQuery) =>
        User.Identity?.IsAuthenticated == true ? User.GetDeviceId() : fromQuery;

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
