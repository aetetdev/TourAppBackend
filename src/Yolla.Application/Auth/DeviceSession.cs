namespace Yolla.Application.Auth;

/// <summary>Cihaz kaydı isteği.</summary>
public sealed record DeviceRegistrationRequest
{
    /// <summary>
    /// İstemcinin ürettiği ve kalıcı olarak sakladığı cihaz kimliği.
    /// Aynı değerle tekrar kayıt yapılırsa mevcut cihaz oturumu sürer.
    /// </summary>
    public required Guid DeviceUuid { get; init; }

    /// <summary>ios, android veya web.</summary>
    /// <example>ios</example>
    public required string Platform { get; init; }

    /// <example>1.0.0</example>
    public string? AppVersion { get; init; }

    /// <summary>Tercih edilen içerik dili.</summary>
    /// <example>tr</example>
    public string Language { get; init; } = "tr";
}

/// <summary>Cihaz oturumu.</summary>
public sealed record DeviceSessionDto
{
    public required int DeviceId { get; init; }

    /// <summary>
    /// Sonraki isteklerde <c>Authorization: Bearer &lt;token&gt;</c> başlığında gönderilir.
    /// </summary>
    public required string AccessToken { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>Cihaz bir hesaba bağlıysa kullanıcı kimliği.</summary>
    public int? UserId { get; init; }
}

/// <summary>Kayıt olmadan kullanım için cihaz oturumlarını yönetir.</summary>
public interface IDeviceSessionService
{
    /// <summary>
    /// Cihazı kaydeder veya mevcut kaydı günceller ve erişim jetonu üretir.
    /// </summary>
    Task<DeviceSessionDto> RegisterAsync(
        DeviceRegistrationRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Erişim jetonu üretir.</summary>
public interface ITokenGenerator
{
    /// <param name="roles">
    /// Kullanıcının rolleri. Jetona yazılmazsa <c>[Authorize(Roles = ...)]</c>
    /// ile korunan uçlar rolü veritabanında olan hesabı bile reddeder —
    /// yetkilendirme jetondaki taleplere bakıyor.
    /// </param>
    (string Token, DateTimeOffset ExpiresAt) CreateDeviceToken(
        int deviceId,
        Guid deviceUuid,
        int? userId,
        IEnumerable<string>? roles = null);
}
