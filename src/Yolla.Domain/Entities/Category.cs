using Yolla.Domain.Common;

namespace Yolla.Domain.Entities;

// Yolla kategorisi - ham OSM etiketleri buraya normalize edilir
// (OSM'de "tourism=museum", "historic=castle", "natural=beach" gibi dağınık etiketler var)
public class Category : BaseEntity
{
    // Kod içinde kullanılan sabit anahtar: "museum", "castle", "beach"
    public string Key { get; set; } = string.Empty;

    // Üst kategori: "Tarihi", "Doğa", "Kültür"
    public int? ParentId { get; set; }

    public Category? Parent { get; set; }

    public string NameTr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    // Mobil taraftaki ikon adı
    public string? Icon { get; set; }

    // Kalite skorunu etkileyen kategori ağırlığı (müze bir piknik alanından değerlidir)
    public short Weight { get; set; }

    // Kullanıcıya gösterilmeyen kategoriler (otel, pansiyon gibi) veride tutulur ama filtrelenir
    public bool IsVisible { get; set; } = true;

    public ICollection<Category> Children { get; set; } = [];

    public ICollection<Place> Places { get; set; } = [];
}
