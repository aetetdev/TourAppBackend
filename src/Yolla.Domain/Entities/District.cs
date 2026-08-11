using NetTopologySuite.Geometries;
using Yolla.Domain.Common;

namespace Yolla.Domain.Entities;

// İlçe
public class District : BaseEntity
{
    public int CityId { get; set; }

    public City City { get; set; } = null!;

    // OSM'deki idari sınır ilişkisi (admin_level=6)
    public long? OsmRelationId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string NameNormalized { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public Geometry? Boundary { get; set; }

    public ICollection<Place> Places { get; set; } = [];
}
