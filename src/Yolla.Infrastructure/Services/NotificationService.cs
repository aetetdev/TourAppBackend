using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Yolla.Application.Notifications;
using Yolla.Domain.Entities;
using Yolla.Domain.Enums;
using Yolla.Infrastructure.Persistence;

namespace Yolla.Infrastructure.Services;

/// <inheritdoc cref="INotificationService"/>
public sealed class NotificationService(
    YollaDbContext context,
    IPushSender push,
    ILogger<NotificationService> logger) : INotificationService
{
    public async Task CreateAsync(
        int userId,
        NotificationKind kind,
        NotificationContext notificationContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notificationContext);

        // Dil kullanıcının cihazından: bildirim telefon kapalıyken de
        // gelebiliyor ve o anda uygulamanın metinleri devrede değil, metnin
        // sunucuda ve doğru dilde kurulması gerekiyor.
        var cihazlar = await context.Devices
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.LastSeenAt)
            .Select(x => new { x.Language, x.PushToken })
            .ToListAsync(cancellationToken);

        var language = cihazlar.FirstOrDefault()?.Language ?? "tr";
        var (title, body) = NotificationTexts.Build(kind, notificationContext, language);

        var notification = new Notification
        {
            UserId = userId,
            Kind = kind,
            Language = language,
            Title = title,
            Body = body,
            PlaceId = notificationContext.PlaceId
        };

        context.Notifications.Add(notification);
        await context.SaveChangesAsync(cancellationToken);

        var tokens = cihazlar
            .Select(x => x.PushToken)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct()
            .ToList();

        if (tokens.Count == 0)
        {
            return;
        }

        // İletim başarısız olsa da bildirim listede duruyor: kullanıcı
        // uygulamayı açtığında görüyor. Bu yüzden hata yükseltilmiyor,
        // yalnızca günlüğe yazılıyor — moderatörün onayı push yüzünden
        // düşmemeli.
        try
        {
            var olu = await push.SendAsync(
                tokens,
                new PushMessage
                {
                    Title = title,
                    Body = body,
                    Data = notificationContext.PlaceId is { } placeId
                        ? new Dictionary<string, string> { ["placeId"] = placeId.ToString() }
                        : null
                },
                cancellationToken);

            notification.PushedAt = DateTimeOffset.UtcNow;

            if (olu.Count > 0)
            {
                await CleanTokensAsync(olu, cancellationToken);
            }

            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception hata)
        {
            logger.LogWarning(
                hata,
                "Bildirim telefona iletilemedi (kullanıcı {UserId}); kayıt listede duruyor.",
                userId);
        }
    }

    public async Task<IReadOnlyList<NotificationDto>> GetMineAsync(
        int userId,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        return await context.Notifications
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(Math.Clamp(take, 1, 200))
            .Select(x => new NotificationDto
            {
                Id = x.Id,
                Kind = x.Kind,
                Title = x.Title,
                Body = x.Body,
                PlaceId = x.PlaceId,
                IsRead = x.ReadAt != null,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public Task<int> GetUnreadCountAsync(
        int userId,
        CancellationToken cancellationToken = default) =>
        context.Notifications
            .AsNoTracking()
            .CountAsync(x => x.UserId == userId && x.ReadAt == null, cancellationToken);

    public async Task MarkReadAsync(
        int userId,
        IReadOnlyList<int>? notificationIds = null,
        CancellationToken cancellationToken = default)
    {
        var sorgu = context.Notifications
            .Where(x => x.UserId == userId && x.ReadAt == null);

        // Kimlik verilmezse hepsi: kullanıcı listeyi açtığında rozetin
        // sıfırlanması beklenen davranış.
        if (notificationIds is { Count: > 0 })
        {
            sorgu = sorgu.Where(x => notificationIds.Contains(x.Id));
        }

        await sorgu.ExecuteUpdateAsync(
            x => x.SetProperty(n => n.ReadAt, DateTimeOffset.UtcNow),
            cancellationToken);
    }

    /// <summary>Teslim edilemeyen jetonları siler.</summary>
    /// <remarks>
    /// Cihaz uygulamayı silmiş ya da jeton yenilenmiş olabilir. Ölü jetonu
    /// tutmak her bildirimde boşuna bir istek demek.
    /// </remarks>
    private async Task CleanTokensAsync(
        IReadOnlyList<string> tokens,
        CancellationToken cancellationToken)
    {
        await context.Devices
            .Where(x => x.PushToken != null && tokens.Contains(x.PushToken))
            .ExecuteUpdateAsync(
                x => x.SetProperty(d => d.PushToken, (string?)null),
                cancellationToken);
    }
}
