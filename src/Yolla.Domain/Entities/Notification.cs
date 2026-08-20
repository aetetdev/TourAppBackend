using Yolla.Domain.Common;
using Yolla.Domain.Enums;

namespace Yolla.Domain.Entities;

/// <summary>
/// Kullanıcıya iletilecek bildirim.
/// </summary>
/// <remarks>
/// Bildirim önce buraya yazılıyor, telefona iletim bunun üstünde ayrı bir
/// kanal. Sebebi: push teslimi garanti değil — kullanıcı izni reddedebilir,
/// telefon kapalı olabilir, jeton geçersizleşebilir. Kayıt burada durduğu
/// sürece kullanıcı uygulamayı açtığında olan biteni görüyor.
///
/// Metin **yazıldığı anda** üretilip saklanıyor, gösterilirken değil:
/// bildirim o günün olayını anlatıyor ve kurallar sonradan değişse bile
/// geçmiş bildirim aynı kalmalı.
/// </remarks>
public class Notification : BaseEntity
{
    public int UserId { get; set; }

    public NotificationKind Kind { get; set; }

    /// <summary>Bildirimin dili; kullanıcının o anki tercihine göre.</summary>
    public string Language { get; set; } = "tr";

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    /// <summary>İlgili yer; dokunulduğunda oraya gidiliyor.</summary>
    public int? PlaceId { get; set; }

    public DateTimeOffset? ReadAt { get; set; }

    /// <summary>
    /// Telefona iletildiği an.
    /// </summary>
    /// <remarks>
    /// Boş olması iletilmediği anlamına geliyor: ya push yapılandırılmamış,
    /// ya kullanıcının geçerli jetonu yok, ya da gönderim başarısız oldu.
    /// Bildirim yine de listede duruyor.
    /// </remarks>
    public DateTimeOffset? PushedAt { get; set; }
}
