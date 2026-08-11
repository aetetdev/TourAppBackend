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

    public ICollection<Trip> Trips { get; set; } = [];

    public ICollection<Swipe> Swipes { get; set; } = [];
}
