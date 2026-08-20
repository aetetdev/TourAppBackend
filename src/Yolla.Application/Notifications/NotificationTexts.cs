using Yolla.Domain.Enums;

namespace Yolla.Application.Notifications;

/// <summary>
/// Bildirim metinleri.
/// </summary>
/// <remarks>
/// Metin sunucuda kuruluyor, istemcide değil: bildirim telefon kapalıyken de
/// gelmek zorunda ve o anda uygulamanın metin dosyaları devrede değil. Bu
/// yüzden dil de gönderi anında biliniyor olmalı — cihazın kayıtlı dili
/// kullanılıyor.
///
/// Metinler yazıldığı anda üretilip saklanıyor: kurallar sonradan değişse
/// bile geçmiş bildirim o günün olayını anlatmaya devam etmeli.
/// </remarks>
public static class NotificationTexts
{
    public static (string Title, string Body) Build(
        NotificationKind kind,
        NotificationContext context,
        string language)
    {
        ArgumentNullException.ThrowIfNull(context);

        var english = language.StartsWith("en", StringComparison.OrdinalIgnoreCase);

        return kind switch
        {
            NotificationKind.PhotoApproved => english
                ? ("Your photo is live",
                    Coins(context, $"Your photo of {context.PlaceName} is now published.",
                        c => $"Your photo of {context.PlaceName} is now published. You earned {c} coins."))
                : ("Fotoğrafın yayında",
                    Coins(context, $"{context.PlaceName} için gönderdiğin fotoğraf yayına girdi.",
                        c => $"{context.PlaceName} için gönderdiğin fotoğraf yayına girdi. {c} coin kazandın.")),

            NotificationKind.PhotoRejected => english
                ? ("Your photo wasn't published",
                    Reason(context, $"Your photo of {context.PlaceName} wasn't published."))
                : ("Fotoğrafın yayınlanmadı",
                    Reason(context, $"{context.PlaceName} için gönderdiğin fotoğraf yayınlanmadı.")),

            NotificationKind.SuggestionApproved => english
                ? ("Your suggestion is in the catalogue",
                    Coins(context, $"{context.PlaceName} has been added.",
                        c => $"{context.PlaceName} has been added. You earned {c} coins."))
                : ("Önerin kataloğa girdi",
                    Coins(context, $"{context.PlaceName} eklendi.",
                        c => $"{context.PlaceName} eklendi. {c} coin kazandın.")),

            NotificationKind.SuggestionRejected => english
                ? ("Your suggestion wasn't added",
                    Reason(context, $"{context.PlaceName} wasn't added to the catalogue."))
                : ("Önerin eklenmedi",
                    Reason(context, $"{context.PlaceName} kataloğa eklenmedi.")),

            _ => ("Yolla", context.PlaceName)
        };
    }

    /// <summary>Coin verildiyse metne ekler.</summary>
    private static string Coins(
        NotificationContext context,
        string plain,
        Func<int, string> withCoins) =>
        context.Coins is { } coins && coins > 0 ? withCoins(coins) : plain;

    /// <summary>
    /// Red sebebini metne ekler.
    /// </summary>
    /// <remarks>
    /// Sebep olmadan "reddedildi" demek kullanıcıyı aynı hatayı tekrar
    /// yapmaya bırakıyor; moderasyon zaten sebep girmeden reddedemiyor.
    /// </remarks>
    private static string Reason(NotificationContext context, string plain) =>
        string.IsNullOrWhiteSpace(context.Reason) ? plain : $"{plain} {context.Reason}";
}
