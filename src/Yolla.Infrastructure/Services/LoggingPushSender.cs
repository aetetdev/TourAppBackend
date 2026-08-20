using Microsoft.Extensions.Logging;
using Yolla.Application.Notifications;

namespace Yolla.Infrastructure.Services;

/// <summary>
/// Push sağlayıcısı yapılandırılmadığında devreye giren gönderici.
/// </summary>
/// <remarks>
/// Firebase projesi olmadan telefona bildirim gitmiyor ama bunun uygulamayı
/// durdurması için bir sebep yok: bildirim veritabanına yazılıyor, kullanıcı
/// uygulamayı açtığında görüyor. Burası yalnızca "gitmiş olsaydı ne
/// giderdi"yi günlüğe yazıyor.
///
/// Sessizce hiçbir şey yapmamak yerine günlüğe yazmasının sebebi: kimse
/// bildirimlerin neden gelmediğini merak ederek saatler harcamasın.
/// </remarks>
public sealed class LoggingPushSender(ILogger<LoggingPushSender> logger) : IPushSender
{
    public bool IsConfigured => false;

    public Task<IReadOnlyList<string>> SendAsync(
        IReadOnlyList<string> tokens,
        PushMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(message);

        logger.LogInformation(
            "Push yapılandırılmadı; {Count} cihaza gidecekti: {Title} — {Body}",
            tokens.Count,
            message.Title,
            message.Body);

        return Task.FromResult<IReadOnlyList<string>>([]);
    }
}
