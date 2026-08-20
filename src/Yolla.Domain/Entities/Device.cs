using Yolla.Domain.Common;

namespace Yolla.Domain.Entities;

// Cihaz kaydı. Kullanıcı hesap açmadan da plan yapabilsin diye sahiplik önce cihaza bağlanır;
// sonradan kayıt olunca UserId doldurulur ve geçmiş planlar hesaba taşınır.
public class Device : BaseEntity
{
    public Guid DeviceUuid { get; set; }

    // Identity kullanıcısı (Infrastructure katmanında tanımlı) - anonimken null
    public int? UserId { get; set; }

    // "ios", "android", "web"
    public string Platform { get; set; } = string.Empty;

    public string? AppVersion { get; set; }

    // Tercih edilen içerik dili
    public string Language { get; set; } = "tr";

    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Telefona bildirim göndermek için kullanılan jeton.
    /// </summary>
    /// <remarks>
    /// Uygulama izin verildikten sonra gönderiyor; izin verilmezse boş
    /// kalıyor. Jeton uygulama silinince ya da yenilenince geçersizleşiyor,
    /// gönderim başarısız olunca temizleniyor — ölü jetona her bildirimde
    /// boşuna istek atmamak için.
    /// </remarks>
    public string? PushToken { get; set; }

    public DateTimeOffset? PushTokenUpdatedAt { get; set; }

    public ICollection<Trip> Trips { get; set; } = [];

    public ICollection<Swipe> Swipes { get; set; } = [];
}
