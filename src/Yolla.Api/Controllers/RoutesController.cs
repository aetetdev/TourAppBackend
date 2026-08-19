using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Yolla.Api.Extensions;
using Yolla.Application.Common;
using Yolla.Application.Routing;

namespace Yolla.Api.Controllers;

/// <summary>
/// Rota hesaplama ve yol üstü keşif.
/// </summary>
/// <remarks>
/// Uygulamanın iki modu da bu uçlara dayanır:
///
/// **Şehir içi mod** - beğenilen yerler <c>optimize</c> ucuyla en kısa yürüme sırasına dizilir.
///
/// **Şehirlerarası mod** - <c>corridor</c> ucu iki şehir arasındaki yol koridorunda bulunan
/// yerleri, yol boyunca ilerleme sırasına göre döndürür. Kullanıcı yolun başındaki yerleri
/// önce görür.
///
/// Rota hesabı her istekte rota motoruna gerçek iş yüklediği için bu uçlar ayrı ve daha dar
/// bir hız sınırına tabidir.
/// </remarks>
[ApiController]
[Route("api/v1/routes")]
[Produces("application/json")]
[EnableRateLimiting("routing")]
public sealed class RoutesController(IRouteService routeService) : ControllerBase
{
    /// <summary>Seçilen yerleri en kısa sırayla dizer.</summary>
    /// <remarks>
    /// Gezgin satıcı problemini çözer: 10 durağı yanlış sırayla gezmek, doğru sıraya göre
    /// saatler fazla sürebilir. Dönen <c>stops</c> listesi uğrama sırasındadır.
    ///
    /// Örnek istek:
    ///
    ///     POST /api/v1/routes/optimize
    ///     {
    ///       "placeIds": [93, 104, 87, 112],
    ///       "startPoint": { "latitude": 38.6431, "longitude": 34.8286 },
    ///       "travelMode": "Foot",
    ///       "roundTrip": false
    ///     }
    ///
    /// <c>startPoint</c> kullanıcının bulunduğu yer ya da oteli olabilir; verilmezse ilk
    /// yer başlangıç kabul edilir.
    /// </remarks>
    /// <response code="200">Sıralanmış duraklar ve rota çizgisi.</response>
    /// <response code="400">Yer listesi boş, çok uzun veya koordinatlar geçersiz.</response>
    /// <response code="404">Gönderilen yerlerin hiçbiri bulunamadı.</response>
    /// <response code="502">Rota motoruna ulaşılamadı.</response>
    [HttpPost("optimize")]
    [ProducesResponseType(typeof(ApiResponse<OptimizedRouteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Optimize(
        [FromBody] RouteOptimizeRequest request,
        CancellationToken cancellationToken)
    {
        var route = await routeService.OptimizeAsync(request, cancellationToken);

        return Ok(ApiResponse<OptimizedRouteDto>.Create(route, Attribution.Places));
    }

    /// <summary>İki nokta arasındaki yol koridorunda bulunan yerleri döndürür.</summary>
    /// <remarks>
    /// Önce iki nokta arası araç rotası hesaplanır, sonra o çizginin çevresindeki turistik
    /// yerler aranır. Kartlar yol boyunca ilerleme sırasına göre gelir: kullanıcı yola
    /// çıktığında önce yakınındaki yerleri görür.
    ///
    /// Her kartta <c>routeProgress</c> (yolun neresinde, 0-1 arası) ve <c>detourMeters</c>
    /// (ana yoldan sapma mesafesi) alanları dolu gelir.
    ///
    /// Örnek istek:
    ///
    ///     POST /api/v1/routes/corridor
    ///     {
    ///       "start": { "latitude": 41.0082, "longitude": 28.9784 },
    ///       "end":   { "latitude": 36.8969, "longitude": 30.7133 },
    ///       "bufferKm": 15,
    ///       "take": 20
    ///     }
    /// </remarks>
    /// <response code="200">Koridordaki yerler ve ana rota.</response>
    /// <response code="400">Koordinatlar geçersiz.</response>
    /// <response code="502">Rota motoruna ulaşılamadı veya iki nokta arasında yol yok.</response>
    [HttpPost("corridor")]
    [ProducesResponseType(typeof(ApiResponse<CorridorFeedDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetCorridor(
        [FromBody] CorridorFeedRequest request,
        CancellationToken cancellationToken)
    {
        // Jeton varsa kaydırılmış yerler koridorda da tekrar gösterilmez
        var effective = request with
        {
            DeviceId = User.Identity?.IsAuthenticated == true ? User.GetDeviceId() : request.DeviceId
        };

        var feed = await routeService.GetCorridorFeedAsync(effective, cancellationToken);

        return Ok(ApiResponse<CorridorFeedDto>.Create(feed, Attribution.Places));
    }

    /// <summary>Koridorun geçtiği şehirleri listeler.</summary>
    /// <remarks>
    /// Kullanıcı yola çıkmadan önce "hangi şehirlere uğrayayım" sorusunu
    /// cevaplasın diye. Dönen liste yol boyunca sıralı: ilk sıradaki şehir
    /// başlangıca en yakın olanı.
    ///
    /// Eleme koşulları <c>corridor</c> ucuyla birebir aynı, dolayısıyla burada
    /// görünen <c>placeCount</c> ile o şehir seçilip kart akışına geçildiğinde
    /// çıkan yer sayısı tutuyor. Seçim <c>corridor</c> ucuna <c>cityIds</c>
    /// olarak geçiliyor.
    ///
    /// Örnek istek:
    ///
    ///     POST /api/v1/routes/corridor/cities
    ///     {
    ///       "start": { "latitude": 41.0082, "longitude": 28.9784 },
    ///       "end":   { "latitude": 36.8969, "longitude": 30.7133 },
    ///       "bufferKm": 15
    ///     }
    /// </remarks>
    /// <response code="200">Koridordaki şehirler ve ana rota.</response>
    /// <response code="400">Koordinatlar geçersiz.</response>
    /// <response code="502">Rota motoruna ulaşılamadı veya iki nokta arasında yol yok.</response>
    [HttpPost("corridor/cities")]
    [ProducesResponseType(typeof(ApiResponse<CorridorCitiesDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetCorridorCities(
        [FromBody] CorridorCitiesRequest request,
        CancellationToken cancellationToken)
    {
        var cities = await routeService.GetCorridorCitiesAsync(request, cancellationToken);

        return Ok(ApiResponse<CorridorCitiesDto>.Create(cities, Attribution.Places));
    }
}
