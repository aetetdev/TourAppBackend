using NetTopologySuite.Geometries;
using Yolla.Domain.Common;
using Yolla.Domain.Enums;

namespace Yolla.Domain.Entities;

// Kullanıcının gezi planı
public class Trip : BaseEntity
{
    public int DeviceId { get; set; }

    public Device Device { get; set; } = null!;

    // Kayıtlı kullanıcıya ait plan - anonimken null
    public int? UserId { get; set; }

    public TripMode Mode { get; set; }

    public TravelMode TravelMode { get; set; }

    public string? Name { get; set; }

    // --- Şehir içi mod ---

    public int? CityId { get; set; }

    public City? City { get; set; }

    // --- Şehirlerarası rota modu ---

    public Point? StartPoint { get; set; }

    public Point? EndPoint { get; set; }

    // Rota çizgisinin kaç metre çevresindeki yerler taranacak (varsayılan 15 km)
    public int CorridorBufferMeters { get; set; } = 15_000;

    // Son hesaplanan rotanın özeti - her açılışta OSRM'e gitmemek için saklanır
    public double? TotalDistanceMeters { get; set; }

    public double? TotalDurationSeconds { get; set; }

    // OSRM'den dönen encoded polyline
    public string? RouteGeometry { get; set; }

    public ICollection<TripPlace> Places { get; set; } = [];
}
