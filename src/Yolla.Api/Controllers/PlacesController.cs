using Microsoft.AspNetCore.Mvc;
using Yolla.Application.Common;
using Yolla.Application.Discovery;
using Yolla.Application.Places;

namespace Yolla.Api.Controllers;

/// <summary>
/// Yer detayları.
/// </summary>
/// <remarks>
/// Kullanıcı bir karta dokunduğunda açılan sayfayı besler: tam açıklama, adres, çalışma
/// saatleri, Wikipedia bağlantısı ve yakındaki diğer yerler.
///
/// Yanıt ayrıca hazır bir yol tarifi bağlantısı içerir; kullanıcı güncel yorumlar ve
/// çalışma saatleri için harita uygulamasına gidebilir.
/// </remarks>
[ApiController]
[Route("api/v1/places")]
[Produces("application/json")]
public sealed class PlacesController(IPlaceService placeService) : ControllerBase
{
    /// <summary>Yerin tüm ayrıntılarını döndürür.</summary>
    /// <param name="placeId">Yer kimliği.</param>
    /// <param name="language">İçerik dili (<c>tr</c> veya <c>en</c>).</param>
    /// <response code="200">Yer detayı ve yakındaki yerler.</response>
    /// <response code="404">Yer bulunamadı.</response>
    [HttpGet("{placeId:int}")]
    [ProducesResponseType(typeof(ApiResponse<PlaceDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        int placeId,
        [FromQuery] string language = "tr",
        CancellationToken cancellationToken = default)
    {
        var place = await placeService.GetByIdAsync(placeId, language, cancellationToken);

        return Ok(ApiResponse<PlaceDetailDto>.Create(place, Attribution.Places));
    }

    /// <summary>Yeri web adresindeki kısa adlarla getirir.</summary>
    /// <remarks>
    /// Web tarafındaki yer sayfaları bu ucu kullanır: <c>/tr/sehir/nevsehir/uchisar-kalesi</c>
    /// adresine karşılık gelir.
    /// </remarks>
    /// <param name="citySlug">Şehrin kısa adı, örn. <c>nevsehir</c>.</param>
    /// <param name="placeSlug">Yerin kısa adı, örn. <c>uchisar-kalesi</c>.</param>
    /// <param name="language">İçerik dili.</param>
    /// <response code="200">Yer detayı.</response>
    /// <response code="404">Yer bulunamadı.</response>
    [HttpGet("by-slug/{citySlug}/{placeSlug}")]
    [ProducesResponseType(typeof(ApiResponse<PlaceDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBySlug(
        string citySlug,
        string placeSlug,
        [FromQuery] string language = "tr",
        CancellationToken cancellationToken = default)
    {
        var place = await placeService.GetBySlugAsync(citySlug, placeSlug, language, cancellationToken);

        return Ok(ApiResponse<PlaceDetailDto>.Create(place, Attribution.Places));
    }

    /// <summary>Bir koordinatın çevresindeki yerleri kart olarak döndürür.</summary>
    /// <remarks>
    /// "Yakınımdakiler" ekranı için. Kullanıcının bulunduğu konumdan yakınlığa göre sıralı gelir.
    /// </remarks>
    /// <param name="latitude">Enlem.</param>
    /// <param name="longitude">Boylam.</param>
    /// <param name="radiusMeters">Arama yarıçapı (100-20000, varsayılan 3000).</param>
    /// <param name="take">Kaç kayıt döneceği (1-50).</param>
    /// <param name="language">İçerik dili.</param>
    /// <response code="200">Yakındaki yerler.</response>
    /// <response code="400">Koordinat geçersiz.</response>
    [HttpGet("nearby")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PlaceCardDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetNearby(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        [FromQuery] int radiusMeters = 3000,
        [FromQuery] int take = 20,
        [FromQuery] string language = "tr",
        CancellationToken cancellationToken = default)
    {
        var places = await placeService.GetNearbyCardsAsync(
            latitude, longitude, radiusMeters, take, language, cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<PlaceCardDto>>.Create(places, Attribution.Places));
    }
}
