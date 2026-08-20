using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yolla.Api.Extensions;
using Yolla.Application.Admin;
using Yolla.Application.Common;
using Yolla.Application.Rewards;

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
public sealed class AdminController(
    IAdminAnalyticsService analytics,
    IAdminContentService content) : ControllerBase
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

    /// <summary>Fotoğrafı olmayan yerler; içerik ekibinin iş listesi.</summary>
    /// <remarks>Kaliteli olanlar önce: bir fotoğraf eklendiğinde doğrudan
    /// kart destesine giren kayıtlar ekibin zamanını en iyi değerlendiriyor.</remarks>
    /// <response code="200">Yerler.</response>
    [HttpGet("fotografsiz-yerler")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<MissingPhotoDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPlacesMissingPhoto(
        [FromQuery] int? cityId = null,
        [FromQuery] string? search = null,
        [FromQuery] int take = 30,
        CancellationToken cancellationToken = default)
    {
        var yerler = await content.GetPlacesMissingPhotoAsync(
            cityId, search, take, cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<MissingPhotoDto>>.Create(yerler));
    }

    /// <summary>Bir yere doğrudan fotoğraf ekler.</summary>
    /// <remarks>
    /// `multipart/form-data`. Kullanıcı gönderisinden farkı moderasyondan
    /// geçmemesi: ekip zaten moderasyonun kendisi. Fotoğraf sunucuda JPEG'e
    /// çevriliyor, 1920 piksele indiriliyor ve EXIF'i (konum dahil)
    /// siliniyor.
    ///
    /// Fotoğrafı çekenin adı zorunlu; görselin yanında atıf olarak
    /// gösteriliyor.
    /// </remarks>
    /// <response code="204">Eklendi ve yayına girdi.</response>
    /// <response code="400">Dosya okunamadı, çok küçük ya da ad boş.</response>
    /// <response code="404">Yer yok.</response>
    [HttpPost("yerler/{placeId:int}/fotograf")]
    [RequestSizeLimit(RewardRules.MaxPhotoBytes)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddPhoto(
        int placeId,
        IFormFile photo,
        [FromForm] string photographerName,
        [FromForm] string? license = null,
        [FromForm] string? sourceUrl = null,
        CancellationToken cancellationToken = default)
    {
        if (photo is null || photo.Length == 0)
        {
            throw RequestValidationException.Single("photo", "Fotoğraf gerekli.");
        }

        await using var stream = photo.OpenReadStream();

        await content.AddPhotoAsync(
            placeId,
            new AddPhotoRequest
            {
                PhotographerName = photographerName,
                License = license,
                SourceUrl = sourceUrl
            },
            stream,
            RequireUserId(),
            cancellationToken);

        return NoContent();
    }

    /// <summary>Kataloğa doğrudan yeni yer ekler.</summary>
    /// <remarks>
    /// Kullanıcı önerisinden farkı kuyruğa düşmemesi ve kalite puanının
    /// daha yüksek başlaması: ekip kaydı doğrulayarak giriyor.
    /// </remarks>
    /// <response code="201">Eklendi.</response>
    /// <response code="400">Ad, kategori ya da konum geçersiz.</response>
    [HttpPost("yerler")]
    [ProducesResponseType(typeof(ApiResponse<CreatedPlaceDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreatePlace(
        [FromBody] CreatePlaceRequest request,
        CancellationToken cancellationToken)
    {
        var created = await content.CreatePlaceAsync(
            request, RequireUserId(), cancellationToken);

        return CreatedAtAction(
            nameof(GetPlacesMissingPhoto),
            null,
            ApiResponse<CreatedPlaceDto>.Create(created));
    }

    private int RequireUserId() =>
        User.GetUserId()
        ?? throw RequestValidationException.Single(
            "account", "Bu işlem için hesap gerekli.");
}
