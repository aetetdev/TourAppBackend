using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Yolla.Api.Extensions;
using Yolla.Application.Common;
using Yolla.Application.Rewards;

namespace Yolla.Api.Controllers;

/// <summary>
/// Fotoğraf katkısı, coin ve premium.
/// </summary>
/// <remarks>
/// Kullanıcı fotoğrafsız bir yere fotoğraf gönderiyor; gönderi moderasyondan
/// geçince yayına giriyor ve gönderene coin yazılıyor. Biriken coin premium
/// hakkına çevrilebiliyor.
///
/// Otomatik kaynaklar tükendiği için (feed'e girmeye layık 4.003 kayıt hâlâ
/// fotoğrafsız) bu akış içerik boşluğunu kapatmanın tek yolu.
/// </remarks>
[ApiController]
[Route("api/v1/rewards")]
[Produces("application/json")]
[Authorize]
public sealed class RewardsController(IRewardService rewards) : ControllerBase
{
    /// <summary>Coin ve premium kurallarını döndürür.</summary>
    /// <remarks>
    /// İstemci bu sayıları kendi içine gömmesin diye açıldı; ekonomi
    /// değiştiğinde uygulama güncellemesi gerekmiyor.
    /// </remarks>
    /// <response code="200">Kurallar.</response>
    [HttpGet("kurallar")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<RewardRulesDto>), StatusCodes.Status200OK)]
    public IActionResult GetRules() =>
        Ok(ApiResponse<RewardRulesDto>.Create(new RewardRulesDto
        {
            CoinsPerApprovedPhoto = RewardRules.CoinsPerApprovedPhoto,
            CoinsForOneMonth = RewardRules.CoinsForOneMonth,
            CoinsForTwoMonths = RewardRules.CoinsForTwoMonths,
            CoinsForUnlimited = RewardRules.CoinsForUnlimited,
            FreeMonthlyTripLimit = RewardRules.FreeMonthlyTripLimit
        }));

    /// <summary>Coin bakiyesi, premium durumu ve aylık plan kotası.</summary>
    /// <response code="200">Durum.</response>
    /// <response code="401">Hesap gerekli.</response>
    [HttpGet("durum")]
    [ProducesResponseType(typeof(ApiResponse<RewardStatusDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        var status = await rewards.GetStatusAsync(RequireUserId(), cancellationToken);

        return Ok(ApiResponse<RewardStatusDto>.Create(status));
    }

    /// <summary>Coin defteri, yeniden eskiye.</summary>
    /// <response code="200">Hareketler.</response>
    [HttpGet("coin-gecmisi")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CoinEntryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCoinHistory(
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var entries = await rewards.GetCoinHistoryAsync(RequireUserId(), take, cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<CoinEntryDto>>.Create(entries));
    }

    /// <summary>Bir yer için fotoğraf gönderir.</summary>
    /// <remarks>
    /// `multipart/form-data` ile gönderilir. Sunucu fotoğrafı EXIF yönüne göre
    /// çeviriyor, en fazla 1920 pikselе indiriyor ve **konum bilgisini
    /// siliyor** — kullanıcının EXIF'teki koordinatı yayına çıkmasın.
    ///
    /// Gönderi doğrudan yayına girmiyor; moderasyon kuyruğuna düşüyor.
    /// </remarks>
    /// <response code="200">Gönderi alındı, incelemeyi bekliyor.</response>
    /// <response code="400">Dosya okunamadı, çok küçük ya da kota doldu.</response>
    [HttpPost("fotograf")]
    [EnableRateLimiting("auth")]
    [RequestSizeLimit(RewardRules.MaxPhotoBytes)]
    [ProducesResponseType(typeof(ApiResponse<PhotoSubmissionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitPhoto(
        [FromForm] int placeId,
        IFormFile photo,
        CancellationToken cancellationToken)
    {
        if (photo is null || photo.Length == 0)
        {
            throw RequestValidationException.Single("photo", "Fotoğraf gerekli.");
        }

        if (photo.Length > RewardRules.MaxPhotoBytes)
        {
            throw RequestValidationException.Single(
                "photo",
                $"Fotoğraf en fazla {RewardRules.MaxPhotoBytes / (1024 * 1024)} MB olabilir.");
        }

        await using var stream = photo.OpenReadStream();

        var result = await rewards.SubmitPhotoAsync(
            RequireUserId(),
            User.GetDeviceId(),
            placeId,
            stream,
            photo.ContentType,
            cancellationToken);

        return Ok(ApiResponse<PhotoSubmissionDto>.Create(result));
    }

    /// <summary>Kullanıcının kendi gönderileri ve durumları.</summary>
    /// <response code="200">Gönderiler.</response>
    [HttpGet("fotograflarim")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PhotoSubmissionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMySubmissions(CancellationToken cancellationToken)
    {
        var items = await rewards.GetMySubmissionsAsync(RequireUserId(), cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<PhotoSubmissionDto>>.Create(items));
    }

    /// <summary>Coini premium hakkına çevirir.</summary>
    /// <remarks>
    /// Mevcut bir hak varsa süre onun bitişinden devam ediyor; kalan günler
    /// yanmıyor.
    /// </remarks>
    /// <response code="200">Yeni durum.</response>
    /// <response code="400">Bakiye yetersiz ya da paket geçersiz.</response>
    [HttpPost("premium-al")]
    [ProducesResponseType(typeof(ApiResponse<RewardStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Redeem(
        [FromBody] RedeemRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var status = await rewards.RedeemAsync(
            RequireUserId(), request.Package, cancellationToken);

        return Ok(ApiResponse<RewardStatusDto>.Create(status));
    }

    private int RequireUserId() =>
        User.GetUserId()
        ?? throw RequestValidationException.Single(
            "account", "Bu işlem için hesap gerekli. Önce giriş yapın.");
}

/// <summary>Premium alma isteği.</summary>
public sealed class RedeemRequest
{
    public PremiumPackage Package { get; init; }
}
