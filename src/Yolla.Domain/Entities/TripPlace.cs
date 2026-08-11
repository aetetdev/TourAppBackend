using Yolla.Domain.Common;

namespace Yolla.Domain.Entities;

// Plana eklenen yer ve rota içindeki sırası
public class TripPlace : BaseEntity
{
    public int TripId { get; set; }

    public Trip Trip { get; set; } = null!;

    public int PlaceId { get; set; }

    public Place Place { get; set; } = null!;

    // Rota optimizasyonu (OSRM /trip) sonrası ziyaret sırası
    public int OrderIndex { get; set; }

    // Çok günlük planlarda hangi güne düştüğü - 0 tabanlı
    public int DayIndex { get; set; }

    public bool IsVisited { get; set; }

    public string? Note { get; set; }
}
