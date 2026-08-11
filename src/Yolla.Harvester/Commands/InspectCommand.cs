using Yolla.Application.Osm;
using Yolla.Application.Places;
using Yolla.Harvester.Osm;

namespace Yolla.Harvester.Commands;

/// <summary>
/// Hazırlanmış GeoJSONSeq dosyasını veritabanına yazmadan analiz eder.
/// </summary>
/// <remarks>
/// Amaç, içe aktarmadan önce veriyi görmek: kaç kayıt turistik yer sayılıyor, hangi
/// kategorilere dağılıyor, kaçı adsız olduğu için elenecek. Kategori eşlemesinde bir
/// hata varsa burada fark edilir, 100 bin satır veritabanına yazıldıktan sonra değil.
/// </remarks>
public static class InspectCommand
{
    public static Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Kullanım: inspect <dosya.geojsonl>");
            return Task.FromResult(1);
        }

        var filePath = args[0];

        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"Dosya bulunamadı: {filePath}");
            return Task.FromResult(1);
        }

        var reader = new GeoJsonSeqReader();
        var stats = new InspectionStats();

        foreach (var feature in reader.Read(filePath))
        {
            stats.Total++;

            var category = OsmCategoryMapper.Map(feature.Tags);

            if (category is null)
            {
                stats.Unmapped++;
                TrackUnmappedTags(stats, feature);
                continue;
            }

            if (IsHiddenCategory(category))
            {
                stats.Hidden++;
                stats.CountCategory(category);
                continue;
            }

            var name = feature.GetTag("name");

            if (string.IsNullOrWhiteSpace(name))
            {
                stats.Unnamed++;
                continue;
            }

            var score = PlaceQualityScorer.Score(new PlaceQualityInput
            {
                Name = name,
                HasWikidata = feature.Tags.ContainsKey("wikidata"),
                HasWikipedia = feature.Tags.ContainsKey("wikipedia"),
                HasNameEn = feature.Tags.ContainsKey("name:en"),
                HasDescription = feature.Tags.ContainsKey("description"),
                HasWebsite = feature.Tags.ContainsKey("website") || feature.Tags.ContainsKey("contact:website"),
                HasOpeningHours = feature.Tags.ContainsKey("opening_hours"),
                // Bu aşamada fotoğraf henüz çekilmedi; skor zenginleştirme sonrası yükselecek
                HasPhoto = false,
                CategoryWeight = 0
            });

            stats.Usable++;
            stats.CountCategory(category);

            if (score >= PlaceQualityScorer.FeedThreshold)
            {
                stats.AboveThreshold++;
            }
        }

        stats.SkippedLines = reader.SkippedLineCount;

        Print(filePath, stats);

        return Task.FromResult(0);
    }

    private static bool IsHiddenCategory(string category) =>
        category is OsmCategoryMapper.Accommodation
            or OsmCategoryMapper.TouristInformation
            or OsmCategoryMapper.CampSite;

    private static void TrackUnmappedTags(InspectionStats stats, OsmFeature feature)
    {
        // Eşlenmeyen kayıtlarda hangi etiketlerin sık geçtiğini biriktir:
        // kategori haritasında eksik kalan bir tür varsa burada görünür
        foreach (var key in new[] { "tourism", "historic", "natural", "leisure", "amenity", "man_made" })
        {
            var value = feature.GetTag(key);

            if (!string.IsNullOrWhiteSpace(value))
            {
                var tag = $"{key}={value}";
                stats.UnmappedTags[tag] = stats.UnmappedTags.GetValueOrDefault(tag) + 1;
            }
        }
    }

    private static void Print(string filePath, InspectionStats stats)
    {
        Console.WriteLine();
        Console.WriteLine($"Dosya: {Path.GetFileName(filePath)}");
        Console.WriteLine(new string('-', 56));
        Console.WriteLine($"  Okunan kayıt            {stats.Total,10:N0}");
        Console.WriteLine($"  Bozuk satır (atlandı)   {stats.SkippedLines,10:N0}");
        Console.WriteLine();
        Console.WriteLine($"  Kullanılabilir yer      {stats.Usable,10:N0}");
        Console.WriteLine($"    feed eşiğini geçen    {stats.AboveThreshold,10:N0}   (fotoğraf öncesi)");
        Console.WriteLine($"  Gizli kategori          {stats.Hidden,10:N0}   (otel, turizm bürosu, kamp)");
        Console.WriteLine($"  Adsız (elendi)          {stats.Unnamed,10:N0}");
        Console.WriteLine($"  Eşlenmeyen              {stats.Unmapped,10:N0}");

        if (stats.Categories.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("  Kategori dağılımı (ilk 20):");

            foreach (var (category, count) in stats.Categories.OrderByDescending(x => x.Value).Take(20))
            {
                Console.WriteLine($"    {category,-24} {count,8:N0}");
            }
        }

        if (stats.UnmappedTags.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("  Eşlenmeyen en sık etiketler (haritaya eklenmeli mi?):");

            foreach (var (tag, count) in stats.UnmappedTags.OrderByDescending(x => x.Value).Take(15))
            {
                Console.WriteLine($"    {tag,-36} {count,8:N0}");
            }
        }

        Console.WriteLine();
    }

    private sealed class InspectionStats
    {
        public int Total { get; set; }
        public int Usable { get; set; }
        public int AboveThreshold { get; set; }
        public int Hidden { get; set; }
        public int Unnamed { get; set; }
        public int Unmapped { get; set; }
        public int SkippedLines { get; set; }

        public Dictionary<string, int> Categories { get; } = [];
        public Dictionary<string, int> UnmappedTags { get; } = [];

        public void CountCategory(string category) =>
            Categories[category] = Categories.GetValueOrDefault(category) + 1;
    }
}
