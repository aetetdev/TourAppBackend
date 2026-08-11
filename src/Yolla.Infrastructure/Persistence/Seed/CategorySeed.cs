using Microsoft.EntityFrameworkCore;
using Yolla.Domain.Entities;

namespace Yolla.Infrastructure.Persistence.Seed;

// Yolla kategori ağacı. Ham OSM etiketleri (tourism=*, historic=*, natural=* ...)
// harvester tarafından bu anahtarlara normalize edilir.
// Weight: kalite skorunda kategori katkısı. IsVisible=false olanlar veride tutulur ama
// kart destesinde ve listelerde gösterilmez (otel, turizm bürosu gibi).
public static class CategorySeed
{
    private static readonly DateTimeOffset SeedDate = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static void Apply(ModelBuilder builder)
    {
        builder.Entity<Category>().HasData(
            // --- Üst kategoriler ---
            Parent(1, "historic", "Tarihi", "History", "landmark"),
            Parent(2, "culture", "Kültür & Sanat", "Culture & Arts", "palette"),
            Parent(3, "nature", "Doğa", "Nature", "tree"),
            Parent(4, "religious", "İnanç", "Religious", "mosque"),
            Parent(5, "entertainment", "Eğlence", "Entertainment", "ferris-wheel"),
            Parent(6, "other", "Diğer", "Other", "map-pin"),

            // --- Tarihi ---
            Child(10, 1, "castle", "Kale", "Castle", "castle", 10),
            Child(11, 1, "ruins", "Ören Yeri", "Ruins", "columns", 9),
            Child(12, 1, "archaeological_site", "Antik Kent", "Archaeological Site", "columns", 10),
            Child(13, 1, "monument", "Anıt", "Monument", "monument", 7),
            Child(14, 1, "historic_building", "Tarihi Yapı", "Historic Building", "building", 8),
            Child(15, 1, "historic_bridge", "Tarihi Köprü", "Historic Bridge", "bridge", 7),
            Child(16, 1, "tomb", "Türbe", "Tomb", "landmark", 6),
            Child(17, 1, "caravanserai", "Kervansaray", "Caravanserai", "warehouse", 9),
            Child(18, 1, "tower", "Kule", "Tower", "tower", 7),

            // --- Kültür & Sanat ---
            Child(20, 2, "museum", "Müze", "Museum", "museum", 10),
            Child(21, 2, "gallery", "Sanat Galerisi", "Art Gallery", "image", 7),
            Child(22, 2, "theatre", "Tiyatro", "Theatre", "drama", 6),
            Child(23, 2, "artwork", "Sanat Eseri", "Artwork", "brush", 4),

            // --- Doğa ---
            Child(30, 3, "beach", "Plaj", "Beach", "umbrella", 9),
            Child(31, 3, "waterfall", "Şelale", "Waterfall", "waves", 10),
            Child(32, 3, "cave", "Mağara", "Cave", "mountain", 9),
            Child(33, 3, "national_park", "Milli Park", "National Park", "trees", 10),
            Child(34, 3, "viewpoint", "Manzara Noktası", "Viewpoint", "binoculars", 8),
            Child(35, 3, "lake", "Göl", "Lake", "droplet", 8),
            Child(36, 3, "hot_spring", "Kaplıca", "Hot Spring", "hot-tub", 8),
            Child(37, 3, "valley", "Vadi & Kanyon", "Valley & Canyon", "mountain-snow", 8),
            Child(38, 3, "island", "Ada", "Island", "palmtree", 7),
            Child(39, 3, "park", "Park", "Park", "tree-deciduous", 4),

            // --- İnanç ---
            Child(40, 4, "mosque", "Cami", "Mosque", "mosque", 6),
            Child(41, 4, "church", "Kilise", "Church", "church", 8),
            Child(42, 4, "synagogue", "Sinagog", "Synagogue", "star-of-david", 8),
            Child(43, 4, "monastery", "Manastır", "Monastery", "church", 9),

            // --- Eğlence ---
            Child(50, 5, "theme_park", "Tema Parkı", "Theme Park", "ferris-wheel", 7),
            Child(51, 5, "zoo", "Hayvanat Bahçesi", "Zoo", "paw-print", 7),
            Child(52, 5, "aquarium", "Akvaryum", "Aquarium", "fish", 7),
            Child(53, 5, "water_park", "Su Parkı", "Water Park", "waves", 6),

            // --- Diğer ---
            Child(60, 6, "bazaar", "Çarşı & Pazar", "Bazaar", "shopping-bag", 7),
            Child(61, 6, "picnic_site", "Piknik Alanı", "Picnic Site", "utensils", 3),
            Child(62, 6, "other_poi", "Diğer", "Other", "map-pin", 2),

            // Gizli kategoriler: veri bütünlüğü için tutulur, kullanıcıya gösterilmez
            Hidden(90, 6, "accommodation", "Konaklama", "Accommodation"),
            Hidden(91, 6, "tourist_information", "Turizm Bürosu", "Tourist Information"),
            Hidden(92, 6, "camp_site", "Kamp Alanı", "Camp Site")
        );
    }

    private static Category Parent(int id, string key, string nameTr, string nameEn, string icon) => new()
    {
        Id = id,
        Key = key,
        NameTr = nameTr,
        NameEn = nameEn,
        Icon = icon,
        Weight = 0,
        IsVisible = true,
        CreatedAt = SeedDate
    };

    private static Category Child(int id, int parentId, string key, string nameTr, string nameEn, string icon, short weight) => new()
    {
        Id = id,
        ParentId = parentId,
        Key = key,
        NameTr = nameTr,
        NameEn = nameEn,
        Icon = icon,
        Weight = weight,
        IsVisible = true,
        CreatedAt = SeedDate
    };

    private static Category Hidden(int id, int parentId, string key, string nameTr, string nameEn) => new()
    {
        Id = id,
        ParentId = parentId,
        Key = key,
        NameTr = nameTr,
        NameEn = nameEn,
        Icon = null,
        Weight = 0,
        IsVisible = false,
        CreatedAt = SeedDate
    };
}
