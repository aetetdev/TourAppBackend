namespace Yolla.Infrastructure.Auth;

/// <summary>Jeton üretimi ayarları.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// İmza anahtarı. Geliştirme değeri appsettings.json'da; üretimde
    /// <c>Jwt__Key</c> ortam değişkeniyle ezilmesi zorunludur.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    public string Issuer { get; set; } = "yolla";

    public string Audience { get; set; } = "yolla-app";

    /// <summary>
    /// Cihaz jetonunun ömrü (gün). Kullanıcı kayıt olmadan kullandığı için uzun tutulur;
    /// her açılışta yeniden kayıt istemek deneyimi bozar.
    /// </summary>
    public int DeviceTokenDays { get; set; } = 90;
}
