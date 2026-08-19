using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Yolla.Api.Extensions;
using Yolla.Application.Common;
using Yolla.Application.Rewards;
using Yolla.Application.Trips;

namespace Yolla.Api.Controllers;

/// <summary>
/// Gezi planları.
/// </summary>
/// <remarks>
/// Kullanıcının kaydettiği geziler. Planlar cihaza bağlıdır; hesap açmak gerekmez.
/// Her uç yalnızca isteği yapan cihazın kendi planlarına erişir.
///
/// Tipik akış: kart destesinde beğenilen yerler bir plana eklenir, ardından
/// <c>optimize</c> ile duraklar en kısa sıraya dizilir ve rota haritada gösterilir.
/// </remarks>
[ApiController]
[Route("api/v1/trips")]
[Produces("application/json")]
[Authorize]
public sealed class TripsController(
    ITripService tripService,
    IRewardService rewards) : ControllerBase
{
    /// <summary>Kullanıcının planlarını listeler.</summary>
    /// <response code="200">Plan listesi, en son güncellenen başta.</response>
    /// <response code="401">Cihaz jetonu gerekli.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TripSummaryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var trips = await tripService.GetAllAsync(User.GetDeviceId(), cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<TripSummaryDto>>.Create(trips));
    }

    /// <summary>Yeni plan oluşturur.</summary>
    /// <remarks>
    /// Örnek istek:
    ///
    ///     POST /api/v1/trips
    ///     {
    ///       "name": "Kapadokya Hafta Sonu",
    ///       "mode": "City",
    ///       "travelMode": "Foot",
    ///       "cityId": 106,
    ///       "placeIds": [93, 104, 87]
    ///     }
    ///
    /// Şehirlerarası planda <c>cityId</c> yerine <c>startPoint</c> ve <c>endPoint</c> gönderilir.
    /// </remarks>
    /// <response code="201">Oluşturulan plan.</response>
    /// <response code="400">Mod ile gönderilen alanlar uyuşmuyor.</response>
    /// <response code="401">Cihaz jetonu gerekli.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<TripDetailDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTripRequest request,
        CancellationToken cancellationToken)
    {
        // Ücretsiz hesap ayda RewardRules.FreeMonthlyTripLimit plan kaydedebiliyor;
        // premiumda sınır yok. Kota takvim ayı başında sıfırlanıyor.
        if (User.GetUserId() is { } userId
            && !await rewards.CanCreateTripAsync(userId, cancellationToken))
        {
            throw RequestValidationException.Single(
                "trip",
                $"Ücretsiz hesapla ayda {RewardRules.FreeMonthlyTripLimit} plan "
                + "kaydedebilirsin. Premium'a geçerek sınırı kaldırabilirsin.");
        }

        var trip = await tripService.CreateAsync(User.GetDeviceId(), request, cancellationToken);

        return CreatedAtAction(
            nameof(Get),
            new { tripId = trip.Id },
            ApiResponse<TripDetailDto>.Create(trip, Attribution.Places));
    }

    /// <summary>Planın ayrıntılarını getirir.</summary>
    /// <param name="tripId">Plan kimliği.</param>
    /// <param name="language">İçerik dili.</param>
    /// <response code="200">Plan ve durakları.</response>
    /// <response code="401">Cihaz jetonu gerekli.</response>
    /// <response code="404">Plan bulunamadı veya bu cihaza ait değil.</response>
    [HttpGet("{tripId:int}")]
    [ProducesResponseType(typeof(ApiResponse<TripDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        int tripId,
        [FromQuery] string language = "tr",
        CancellationToken cancellationToken = default)
    {
        var trip = await tripService.GetAsync(User.GetDeviceId(), tripId, language, cancellationToken);

        return Ok(ApiResponse<TripDetailDto>.Create(trip, Attribution.Places));
    }

    /// <summary>Planın adını veya ulaşım tipini değiştirir.</summary>
    /// <remarks>
    /// Ulaşım tipi değiştirilirse hesaplanmış rota temizlenir; yürüyerek ve araçla
    /// gidilen yol farklıdır.
    /// </remarks>
    /// <response code="200">Güncellenmiş plan.</response>
    /// <response code="400">Ad geçersiz.</response>
    /// <response code="404">Plan bulunamadı.</response>
    [HttpPatch("{tripId:int}")]
    [ProducesResponseType(typeof(ApiResponse<TripDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        int tripId,
        [FromBody] UpdateTripRequest request,
        CancellationToken cancellationToken)
    {
        var trip = await tripService.UpdateAsync(User.GetDeviceId(), tripId, request, cancellationToken);

        return Ok(ApiResponse<TripDetailDto>.Create(trip, Attribution.Places));
    }

    /// <summary>Planı siler.</summary>
    /// <response code="204">Silindi.</response>
    /// <response code="404">Plan bulunamadı.</response>
    [HttpDelete("{tripId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int tripId, CancellationToken cancellationToken)
    {
        await tripService.DeleteAsync(User.GetDeviceId(), tripId, cancellationToken);

        return NoContent();
    }

    /// <summary>Plana durak ekler veya çıkarır.</summary>
    /// <remarks>
    /// Duraklar değiştiğinde hesaplanmış rota temizlenir; yeniden <c>optimize</c>
    /// çağrılması gerekir.
    ///
    /// Örnek istek:
    ///
    ///     POST /api/v1/trips/12/places
    ///     { "add": [93, 104], "remove": [87] }
    /// </remarks>
    /// <response code="200">Güncellenmiş plan.</response>
    /// <response code="400">Durak sınırı aşıldı.</response>
    /// <response code="404">Plan veya yerler bulunamadı.</response>
    [HttpPost("{tripId:int}/places")]
    [ProducesResponseType(typeof(ApiResponse<TripDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ModifyPlaces(
        int tripId,
        [FromBody] ModifyTripPlacesRequest request,
        CancellationToken cancellationToken)
    {
        var trip = await tripService.ModifyPlacesAsync(
            User.GetDeviceId(), tripId, request, cancellationToken);

        return Ok(ApiResponse<TripDetailDto>.Create(trip, Attribution.Places));
    }

    /// <summary>Planın duraklarını en kısa sıraya dizer ve rotayı kaydeder.</summary>
    /// <remarks>
    /// Gezgin satıcı problemini çözer; sonuç plana kaydedilir, böylece uygulama her
    /// açılışta yeniden hesaplamaz.
    ///
    /// Rota motoruna iş yüklediği için bu uç dar hız sınırına tabidir.
    /// </remarks>
    /// <response code="200">Sıralanmış duraklar ve rota.</response>
    /// <response code="400">Planda durak yok.</response>
    /// <response code="404">Plan bulunamadı.</response>
    /// <response code="502">Rota motoruna ulaşılamadı.</response>
    [HttpPost("{tripId:int}/optimize")]
    [EnableRateLimiting("routing")]
    [ProducesResponseType(typeof(ApiResponse<TripDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Optimize(int tripId, CancellationToken cancellationToken)
    {
        var trip = await tripService.OptimizeAsync(User.GetDeviceId(), tripId, cancellationToken);

        return Ok(ApiResponse<TripDetailDto>.Create(trip, Attribution.Places));
    }
}
