using NetTopologySuite.Geometries;
using Yolla.Domain.Common;
using Yolla.Domain.Enums;

namespace Yolla.Domain.Entities;

// Turistik yer - uygulamanın çekirdek varlığı
public class Place : BaseEntity
{
    public int CountryId { get; set; }

    public Country Country { get; set; } = null!;

    public int CityId { get; set; }

    public City City { get; set; } = null!;

    public int? DistrictId { get; set; }

    public District? District { get; set; }

    public int CategoryId { get; set; }

    public Category Category { get; set; } = null!;

    // --- OSM kimliği: harvester tekrar çalıştığında kayıt bu ikiliyle eşleşir ---

    public OsmElementType OsmType { get; set; }

    public long OsmId { get; set; }

    // --- Temel bilgiler ---

    public string Name { get; set; } = string.Empty;

    public string? NameEn { get; set; }

    public string Slug { get; set; } = string.Empty;

    // WGS84 (SRID 4326). Koridor ve mesafe sorgularının tamamı bu kolon üzerinde çalışır.
    public Point Location { get; set; } = null!;

    public string? Address { get; set; }

    public string? Website { get; set; }

    // OSM opening_hours formatında ham metin
    public string? OpeningHours { get; set; }

    // --- Fotoğraf ve lisans ---
    // Wikimedia Commons görselleri çoğunlukla CC BY-SA: yazar ve lisans göstermek zorunlu.
    // Bu yüzden URL tek başına yeterli değil.

    public string? PhotoUrl { get; set; }

    public string? PhotoAuthor { get; set; }

    public string? PhotoLicense { get; set; }

    // Görselin alındığı kaynak sayfası - atıf linki olarak gösterilir
    public string? PhotoSource { get; set; }

    // --- Zenginleştirme ---

    public string? WikidataId { get; set; }

    public string? WikipediaTitle { get; set; }

    // OSM'deki wikimedia_commons etiketi: "File:X.jpg" ya da "Category:Y".
    // Wikidata kaydı olmayan yerlerin fotoğrafı çoğunlukla buradan bulunur.
    public string? CommonsRef { get; set; }

    public string? DescriptionTr { get; set; }

    public string? DescriptionEn { get; set; }

    // --- Sıralama ve gösterim ---

    // 0-100. Kart destesinde hangi yerlerin öne çıkacağını belirler.
    public short QualityScore { get; set; }

    // Ortalama gezme süresi (dakika) - gün planı bölmede kullanılır
    public short? AvgVisitMinutes { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<PlaceTranslation> Translations { get; set; } = [];
}
