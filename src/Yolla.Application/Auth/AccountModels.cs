namespace Yolla.Application.Auth;

/// <summary>Hesap açma isteği.</summary>
public sealed record RegisterRequest
{
    /// <example>gezgin@example.com</example>
    public required string Email { get; init; }

    /// <summary>En az 8 karakter.</summary>
    public required string Password { get; init; }

    /// <summary>Uygulamada görünecek ad.</summary>
    /// <example>Eren</example>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Kayıt öncesi kullanılan cihaz. Gönderilirse o cihazın planları ve kaydırmaları
    /// hesaba bağlanır; kullanıcı geçmişini kaybetmez.
    /// </summary>
    public Guid? DeviceUuid { get; init; }
}

/// <summary>Giriş isteği.</summary>
public sealed record LoginRequest
{
    public required string Email { get; init; }

    public required string Password { get; init; }

    /// <summary>
    /// Giriş yapılan cihaz. Gönderilirse bu cihaz hesaba bağlanır ve sonraki
    /// isteklerde hesap bilgisi jetonda taşınır.
    /// </summary>
    public Guid? DeviceUuid { get; init; }
}

/// <summary>Şifre değiştirme isteği.</summary>
public sealed record ChangePasswordRequest
{
    public required string CurrentPassword { get; init; }

    public required string NewPassword { get; init; }
}

/// <summary>Hesap silme isteği.</summary>
public sealed record DeleteAccountRequest
{
    /// <summary>Kimlik doğrulaması için mevcut şifre.</summary>
    public required string Password { get; init; }
}

/// <summary>Oturum açmış kullanıcı.</summary>
public sealed record AccountDto
{
    public required int UserId { get; init; }

    public required string Email { get; init; }

    public string? DisplayName { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Hesaba bağlı cihaz sayısı.</summary>
    public required int DeviceCount { get; init; }

    /// <summary>Kaydedilmiş gezi planı sayısı.</summary>
    public required int TripCount { get; init; }
}

/// <summary>Kimlik doğrulama sonucu.</summary>
public sealed record AuthResultDto
{
    public required AccountDto Account { get; init; }

    /// <summary>Sonraki isteklerde <c>Authorization: Bearer</c> başlığında gönderilir.</summary>
    public required string AccessToken { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>Jetonun bağlı olduğu cihaz.</summary>
    public required int DeviceId { get; init; }
}

/// <summary>Kullanıcı hesaplarını yönetir.</summary>
public interface IAccountService
{
    Task<AuthResultDto> RegisterAsync(
        RegisterRequest request, CancellationToken cancellationToken = default);

    Task<AuthResultDto> LoginAsync(
        LoginRequest request, CancellationToken cancellationToken = default);

    Task<AccountDto> GetAsync(int userId, CancellationToken cancellationToken = default);

    Task ChangePasswordAsync(
        int userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hesabı ve bağlı tüm kişisel verileri siler.
    /// </summary>
    /// <remarks>
    /// Uygulama mağazaları, hesap açtıran uygulamaların hesap silmeyi de sunmasını
    /// zorunlu kılıyor. Silme geri alınamaz: planlar, kaydırmalar ve cihaz kayıtları
    /// birlikte kaldırılır.
    /// </remarks>
    Task DeleteAccountAsync(
        int userId, DeleteAccountRequest request, CancellationToken cancellationToken = default);
}
