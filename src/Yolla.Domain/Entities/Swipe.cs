using Yolla.Domain.Common;
using Yolla.Domain.Enums;

namespace Yolla.Domain.Entities;

// Kart destesindeki her kaydırma kaydı.
// İki işi var: aynı yeri tekrar göstermemek ve öneri sıralamasını kişiselleştirmek.
public class Swipe : BaseEntity
{
    public int DeviceId { get; set; }

    public Device Device { get; set; } = null!;

    public int? UserId { get; set; }

    public int PlaceId { get; set; }

    public Place Place { get; set; } = null!;

    public SwipeDirection Direction { get; set; }

    // Kaydırmanın hangi modda yapıldığı - şehir içi ve rota tercihleri farklı olabilir
    public TripMode Context { get; set; }

    // Varsa ilgili plan
    public int? TripId { get; set; }
}
