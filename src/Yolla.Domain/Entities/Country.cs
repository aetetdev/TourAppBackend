using Yolla.Domain.Common;

namespace Yolla.Domain.Entities;

// Ülke - veri Türkiye ile başlıyor, model baştan çok ülkeli
public class Country : BaseEntity
{
    // ISO 3166-1 alpha-2 (TR, GR, IT ...)
    public string Iso2 { get; set; } = string.Empty;

    public string NameTr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    // Ülkenin OSM'deki idari sınır ilişkisi - harvester bunu kullanır
    public long? OsmRelationId { get; set; }

    // Veri toplanmamış ülkeler uygulamada listelenmez
    public bool IsActive { get; set; }

    public ICollection<City> Cities { get; set; } = [];
}
