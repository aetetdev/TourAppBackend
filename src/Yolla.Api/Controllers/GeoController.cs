using Microsoft.AspNetCore.Mvc;
using Yolla.Application.Common;
using Yolla.Application.Geo;

namespace Yolla.Api.Controllers;

/// <summary>
/// Ülke ve şehir bilgileri.
/// </summary>
/// <remarks>
/// Uygulama açılışında şehir seçimi bu uçlardan beslenir. Her şehir, kart olarak
/// gösterilebilecek yer sayısını da döndürür; istemci içeriği olmayan şehirleri
/// gizleyebilir veya kullanıcıyı uyarabilir.
/// </remarks>
[ApiController]
[Route("api/v1/geo")]
[Produces("application/json")]
public sealed class GeoController(IGeoService geoService) : ControllerBase
{
    /// <summary>Veri toplanmış ülkeleri listeler.</summary>
    /// <remarks>
    /// Şu an yalnızca Türkiye etkin. Yeni ülkeler veri toplandıkça bu listeye eklenir.
    /// </remarks>
    /// <response code="200">Ülke listesi.</response>
    [HttpGet("countries")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CountryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCountries(CancellationToken cancellationToken)
    {
        var countries = await geoService.GetCountriesAsync(cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<CountryDto>>.Create(countries, Attribution.GeoOnly));
    }

    /// <summary>Bir ülkenin şehirlerini listeler; arama metnine göre süzer.</summary>
    /// <param name="country">Ülke kodu (ISO 3166-1 alpha-2), örn. <c>TR</c>.</param>
    /// <param name="search">
    /// Arama metni. Türkçe karakter farkı gözetilmez: <c>izmir</c> araması <c>İzmir</c> ile eşleşir.
    /// </param>
    /// <param name="onlyWithContent">
    /// <c>true</c> ise yalnızca gösterilebilir içeriği olan şehirler döner.
    /// </param>
    /// <response code="200">Şehir listesi.</response>
    [HttpGet("cities")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CityDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCities(
        [FromQuery] string country = "TR",
        [FromQuery] string? search = null,
        [FromQuery] bool onlyWithContent = false,
        CancellationToken cancellationToken = default)
    {
        var cities = await geoService.GetCitiesAsync(country, search, onlyWithContent, cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<CityDto>>.Create(cities, Attribution.GeoOnly));
    }

    /// <summary>Şehri kimliğine göre getirir.</summary>
    /// <param name="cityId">Şehir kimliği.</param>
    /// <response code="200">Şehir bilgisi.</response>
    /// <response code="404">Şehir bulunamadı.</response>
    [HttpGet("cities/{cityId:int}")]
    [ProducesResponseType(typeof(ApiResponse<CityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCity(int cityId, CancellationToken cancellationToken)
    {
        var city = await geoService.GetCityAsync(cityId, cancellationToken);

        return Ok(ApiResponse<CityDto>.Create(city, Attribution.GeoOnly));
    }

    /// <summary>Şehri web adresindeki kısa adına göre getirir.</summary>
    /// <param name="slug">Kısa ad, örn. <c>nevsehir</c>.</param>
    /// <remarks>Web tarafındaki şehir sayfaları bu ucu kullanır.</remarks>
    /// <response code="200">Şehir bilgisi.</response>
    /// <response code="404">Şehir bulunamadı.</response>
    [HttpGet("cities/by-slug/{slug}")]
    [ProducesResponseType(typeof(ApiResponse<CityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCityBySlug(string slug, CancellationToken cancellationToken)
    {
        var city = await geoService.GetCityBySlugAsync(slug, cancellationToken);

        return Ok(ApiResponse<CityDto>.Create(city, Attribution.GeoOnly));
    }
}
