using Yolla.Application.Places;
using Yolla.Harvester.Import;
using Yolla.Harvester.Osm;

namespace Yolla.Harvester.Commands;

/// <summary>
/// Hazırlanmış GeoJSONSeq dosyasını veritabanına yazmadan analiz eder.
/// </summary>
/// <remarks>
/// Amaç, içe aktarmadan önce veriyi görmek: kaç kayıt turistik yer sayılıyor, hangi
/// kategorilere dağılıyor, kaçı adsız olduğu için elenecek. Kategori eşlemesinde bir
/// hata varsa burada fark edilir, 100 bin satır veritabanına yazıldıktan sonra değil.
///
/// İçe aktarmayla aynı dönüştürücüyü kullanır; rapor ile gerçek sonuç ayrışmaz.
/// </remarks>
public static class InspectCommand
{
    public static Task<int> RunAsync(string[] args)
    {
        var options = new CommandLineArgs(args);
        var filePath = options.Positional.FirstOrDefault()
                       ?? options.GetValue("file", Path.Combine("data", "poi.geojsonl"))!;

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

            // Gizli kategoriler de dönüştürülüyor ki raporda ayrı satır olarak görünsünler
            var result = PlaceFeatureConverter.Convert(feature, includeHiddenCategories: true);

            if (result.Row is null)
            {
                stats.Record(result.SkipReason);

                if (result.SkipReason is PlaceSkipReason.NoCategory)
                {
                    TrackUnmappedTags(stats, feature);
                }

                continue;
            }

            var row = result.Row;

            if (PlaceFeatureConverter.IsHidden(row.CategoryKey))
            {
                stats.Hidden++;
            }
            else
            {
                stats.Usable++;

                if (row.QualityScore >= PlaceQualityScorer.FeedThreshold)
                {
                    stats.AboveThreshold++;
                }
            }

            stats.CountCategory(row.CategoryKey);
        }

        stats.SkippedLines = reader.SkippedLineCount;

        Print(filePath, stats);

        return Task.FromResult(0);
    }

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
        public int Unnamed { get; private set; }
        public int Unmapped { get; private set; }
        public int SkippedLines { get; set; }

        public Dictionary<string, int> Categories { get; } = [];
        public Dictionary<string, int> UnmappedTags { get; } = [];

        public void Record(PlaceSkipReason reason)
        {
            if (reason is PlaceSkipReason.NoCategory)
            {
                Unmapped++;
                return;
            }

            Unnamed++;
        }

        public void CountCategory(string category) =>
            Categories[category] = Categories.GetValueOrDefault(category) + 1;
    }
}
