using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yolla.Application.Admin;
using Yolla.Application.Common;

namespace Yolla.Api.Controllers;

/// <summary>
/// Yönetim panosunun ölçüm uçları.
/// </summary>
/// <remarks>
/// **Hiçbiri kişisel veri döndürmüyor:** ne e-posta, ne ad, ne kullanıcı
/// kimliği. Yalnızca toplamlar dönüyor — "Ankara'da 340 beğeni" sorusu
/// cevaplanıyor, "kim beğendi" sorusu cevaplanmıyor. Bu bir tercih değil,
/// panelin amacı tercihi anlamak; kişiyi tanımak değil.
///
/// Yalnızca <c>moderator</c> rolü erişebiliyor.
/// </remarks>
[ApiController]
[Route("api/v1/admin")]
[Produces("application/json")]
[Authorize(Roles = ModerationController.ModeratorRole)]
public sealed class AdminController(IAdminAnalyticsService analytics) : ControllerBase
{
    /// <summary>Panonun üst şeridi: kullanıcı, içerik, kullanım ve kuyruk sayıları.</summary>
    /// <response code="200">Özet.</response>
    /// <response code="403">Moderatör rolü gerekli.</response>
    [HttpGet("ozet")]
    [ProducesResponseType(typeof(ApiResponse<AdminOverviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOverview(CancellationToken cancellationToken)
    {
        var ozet = await analytics.GetOverviewAsync(cancellationToken);

        return Ok(ApiResponse<AdminOverviewDto>.Create(ozet));
    }

    /// <summary>Şehir bazlı kullanım, beğeniye göre sıralı.</summary>
    /// <remarks>
    /// Kullanıcının nerede olduğu değil, neyi gezdiği ölçülüyor: konum
    /// toplanmıyor, ilgi kaydırılan yerlerin şehrinden çıkarılıyor.
    /// </remarks>
    /// <response code="200">Şehirler.</response>
    [HttpGet("sehirler")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CityUsageDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCities(
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        var sehirler = await analytics.GetCityUsageAsync(take, cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<CityUsageDto>>.Create(sehirler));
    }

    /// <summary>En çok ilgi gören yerler.</summary>
    /// <param name="cityId">Verilirse yalnızca o şehir.</param>
    /// <param name="take">Kaç kayıt.</param>
    /// <response code="200">Yerler.</response>
    [HttpGet("yerler")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PopularPlaceDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPlaces(
        [FromQuery] int? cityId = null,
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        var yerler = await analytics.GetPopularPlacesAsync(cityId, take, cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<PopularPlaceDto>>.Create(yerler));
    }

    /// <summary>Rota tercihleri: şehir içi planlar, koridorlar, ulaşım dağılımı.</summary>
    /// <response code="200">Rotalar.</response>
    [HttpGet("rotalar")]
    [ProducesResponseType(typeof(ApiResponse<RouteInsightsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRoutes(
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        var rotalar = await analytics.GetRouteInsightsAsync(take, cancellationToken);

        return Ok(ApiResponse<RouteInsightsDto>.Create(rotalar));
    }

    /// <summary>Üyelik dağılımı ve coin ekonomisi.</summary>
    /// <response code="200">Üyelik.</response>
    [HttpGet("uyelik")]
    [ProducesResponseType(typeof(ApiResponse<MembershipDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMembership(CancellationToken cancellationToken)
    {
        var uyelik = await analytics.GetMembershipAsync(cancellationToken);

        return Ok(ApiResponse<MembershipDto>.Create(uyelik));
    }
}
