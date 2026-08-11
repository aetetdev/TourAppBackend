using NetTopologySuite.Geometries;
using Yolla.Domain.Common;

namespace Yolla.Domain.Entities;

// Şehir (Türkiye için il)
public class City : BaseEntity
{
    public int CountryId { get; set; }

    public Country Country { get; set; } = null!;

    // OSM'deki idari sınır ilişkisi (admin_level=4).
    // Veri toplayıcı tekrar çalıştığında kayıtlar bu kimlikle eşleşir; şehir adı
    // değişse bile yeni kayıt oluşmaz.
    public long? OsmRelationId { get; set; }

    public string Name { get; set; } = string.Empty;

    // Türkçe karakterler sadeleştirilmiş arama alanı: "İstanbul" -> "istanbul"
    public string NameNormalized { get; set; } = string.Empty;

    public string? NameEn { get; set; }

    // URL'de kullanılan kısa ad: "istanbul" - web SEO sayfaları buna bağlı
    public string Slug { get; set; } = string.Empty;

    // Şehir merkezi - harita ilk açılışında buraya odaklanır
    public Point Center { get; set; } = null!;

    // Şehir sınırı - şehir içi mod bu poligonun içindeki yerleri sorgular
    public Geometry? Boundary { get; set; }

    public int? Population { get; set; }

    public bool IsActive { get; set; }

    public ICollection<District> Districts { get; set; } = [];

    public ICollection<Place> Places { get; set; } = [];
}
