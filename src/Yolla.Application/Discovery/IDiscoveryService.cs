using Yolla.Application.Common;

namespace Yolla.Application.Discovery;

/// <summary>Kart destesini besleyen servis.</summary>
public interface IDiscoveryService
{
    /// <summary>
    /// Bir şehirdeki turistik yerleri kart destesi sırasıyla döndürür.
    /// </summary>
    Task<CursorPage<PlaceCardDto>> GetCityFeedAsync(
        CityFeedRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Şehir içi kart destesi isteği.</summary>
public sealed record CityFeedRequest
{
    /// <summary>Şehir kimliği.</summary>
    public required int CityId { get; init; }

    /// <summary>Sonraki sayfa imleci. İlk istekte boş bırakılır.</summary>
    public string? Cursor { get; init; }

    /// <summary>Kaç kart döndürüleceği. 1-50 arası, varsayılan 20.</summary>
    public int Take { get; init; } = 20;

    /// <summary>Verilirse yalnızca bu kategorilerdeki yerler döner.</summary>
    public IReadOnlyList<string>? CategoryKeys { get; init; }

    /// <summary>
    /// Kartları daha önce görmüş cihaz. Verilirse kaydırılmış yerler tekrar gösterilmez.
    /// </summary>
    public int? DeviceId { get; init; }

    /// <summary>İçerik dili.</summary>
    public string Language { get; init; } = "tr";
}
