using Yolla.Domain.Enums;

namespace Yolla.Application.Notifications;

/// <summary>
/// Kullanıcıya olan biteni haber verir.
/// </summary>
/// <remarks>
/// Bildirim önce veritabanına yazılıyor, telefona iletim ayrı bir kanal
/// (<see cref="IPushSender"/>). Push teslimi garanti değil — izin
/// reddedilebilir, telefon kapalı olabilir, jeton geçersizleşebilir — ama
/// kayıt durduğu için kullanıcı uygulamayı açtığında görüyor.
/// </remarks>
public interface INotificationService
{
    /// <summary>Bildirim yazar ve iletmeyi dener.</summary>
    Task CreateAsync(
        int userId,
        NotificationKind kind,
        NotificationContext context,
        CancellationToken cancellationToken = default);

    /// <summary>Kullanıcının bildirimleri, yeniden eskiye.</summary>
    Task<IReadOnlyList<NotificationDto>> GetMineAsync(
        int userId,
        int take = 50,
        CancellationToken cancellationToken = default);

    /// <summary>Okunmamış bildirim sayısı; sekmedeki rozet.</summary>
    Task<int> GetUnreadCountAsync(
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>Bildirimleri okundu işaretler.</summary>
    Task MarkReadAsync(
        int userId,
        IReadOnlyList<int>? notificationIds = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Bildirim metnini kurmak için gereken bilgiler.</summary>
public sealed record NotificationContext
{
    /// <summary>Konu olan yerin adı.</summary>
    public required string PlaceName { get; init; }

    /// <summary>Varsa yerin kimliği; bildirime dokununca oraya gidiliyor.</summary>
    public int? PlaceId { get; init; }

    /// <summary>Onay karşılığı yazılan coin.</summary>
    public int? Coins { get; init; }

    /// <summary>Reddedildiyse sebebi.</summary>
    public string? Reason { get; init; }
}

public sealed record NotificationDto
{
    public required int Id { get; init; }

    public required NotificationKind Kind { get; init; }

    public required string Title { get; init; }

    public required string Body { get; init; }

    public int? PlaceId { get; init; }

    public required bool IsRead { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// Bildirimi telefona iletir.
/// </summary>
/// <remarks>
/// Arayüz olarak ayrılması, sağlayıcının (bugün Firebase) uygulamanın
/// içine sızmasını engelliyor. Yapılandırma yokken günlüğe yazan bir
/// uygulaması devrede: bildirim yine veritabanına düşüyor, yalnızca
/// telefona gitmiyor.
/// </remarks>
public interface IPushSender
{
    /// <summary>Sağlayıcı yapılandırılmış mı.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Gönderir; teslim edilemeyen jetonları döndürür.
    /// </summary>
    /// <remarks>
    /// Geçersiz jetonlar temizlenmeli: cihaz uygulamayı silmiş olabilir ve
    /// ölü jetona gönderim denemek her seferinde boşuna istek demek.
    /// </remarks>
    Task<IReadOnlyList<string>> SendAsync(
        IReadOnlyList<string> tokens,
        PushMessage message,
        CancellationToken cancellationToken = default);
}

/// <summary>Telefona gidecek bildirim.</summary>
public sealed record PushMessage
{
    public required string Title { get; init; }

    public required string Body { get; init; }

    /// <summary>Uygulamanın açılışta kullanacağı veri; örn. yer kimliği.</summary>
    public IReadOnlyDictionary<string, string>? Data { get; init; }
}
