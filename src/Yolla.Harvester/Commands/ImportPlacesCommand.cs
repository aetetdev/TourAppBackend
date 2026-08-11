using System.Diagnostics;
using Yolla.Harvester.Import;
using Yolla.Harvester.Osm;

namespace Yolla.Harvester.Commands;

/// <summary>
/// Turistik yerleri veritabanına aktarır.
/// </summary>
/// <remarks>
/// Kayıtlar dosyadan akış halinde okunur; tamamı belleğe alınmaz. Aynı komut tekrar
/// çalıştırıldığında kayıtlar (osm_type, osm_id) ikilisiyle eşleşip güncellenir, çoğalmaz.
/// </remarks>
public static class ImportPlacesCommand
{
    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var options = new CommandLineArgs(args);

        var filePath = options.GetValue("file", Path.Combine("data", "poi.geojsonl"))!;
        var country = options.GetValue("country", "TR")!;
        var includeHidden = options.HasFlag("include-hidden");

        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"Yer verisi bulunamadı: {filePath}");
            Console.Error.WriteLine("Önce ./scripts/prepare-osm-data.ps1 çalıştırılmalı.");
            return 1;
        }

        await using var dataSource = HarvesterConnection.CreateDataSource(options.GetValue("connection"));

        if (!await HasBoundariesAsync(dataSource, country, cancellationToken))
        {
            Console.Error.WriteLine(
                "Veritabanında hiç il sınırı yok. Önce 'import-boundaries' çalıştırılmalı, "
                + "aksi halde yerler hiçbir şehre bağlanamaz.");
            return 1;
        }

        var importer = new PlaceImporter(dataSource);
        var reader = new GeoJsonSeqReader();
        var stats = new ConversionStats();

        Console.WriteLine($"Yerler okunuyor: {filePath}");

        var stopwatch = Stopwatch.StartNew();

        var rows = ConvertFeatures(reader, filePath, includeHidden, stats);
        var result = await importer.ImportAsync(rows, country, cancellationToken);

        stopwatch.Stop();

        Console.WriteLine();
        Console.WriteLine($"  Okunan kayıt          {stats.Read,10:N0}");
        Console.WriteLine($"  Elenen (kategorisiz)  {stats.Unmapped,10:N0}");
        Console.WriteLine($"  Elenen (adsız/gizli)  {stats.Filtered,10:N0}");
        Console.WriteLine($"  Bozuk satır           {reader.SkippedLineCount,10:N0}");
        Console.WriteLine();
        Console.WriteLine($"  Eklendi               {result.Inserted,10:N0}");
        Console.WriteLine($"  Güncellendi           {result.Updated,10:N0}");
        Console.WriteLine($"  Şehre bağlanamadı     {result.WithoutCity,10:N0}");
        Console.WriteLine();
        Console.WriteLine($"  Süre: {stopwatch.Elapsed.TotalSeconds:N1} sn");

        return 0;
    }

    private static IEnumerable<PlaceImportRow> ConvertFeatures(
        GeoJsonSeqReader reader,
        string filePath,
        bool includeHidden,
        ConversionStats stats)
    {
        foreach (var feature in reader.Read(filePath))
        {
            stats.Read++;

            if (stats.Read % 50_000 == 0)
            {
                Console.WriteLine($"  ... {stats.Read:N0} kayıt işlendi");
            }

            var result = PlaceFeatureConverter.Convert(feature, includeHiddenCategories: includeHidden);

            if (result.Row is null)
            {
                stats.Record(result.SkipReason);
                continue;
            }

            yield return result.Row;
        }
    }

    private static async Task<bool> HasBoundariesAsync(
        Npgsql.NpgsqlDataSource dataSource,
        string countryIso2,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM cities ci
                JOIN countries co ON co.id = ci.country_id
                WHERE co.iso2 = @iso2 AND ci.boundary IS NOT NULL
            );
            """;

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("iso2", countryIso2.ToUpperInvariant());

        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private sealed class ConversionStats
    {
        public int Read { get; set; }

        /// <summary>Kategori haritasında karşılığı olmayanlar - haritada eksik olabilir.</summary>
        public int Unmapped { get; private set; }

        /// <summary>Adsız, slug'sız veya gizli kategoriye düşenler - beklenen eleme.</summary>
        public int Filtered { get; private set; }

        public void Record(PlaceSkipReason reason)
        {
            if (reason is PlaceSkipReason.NoCategory)
            {
                Unmapped++;
                return;
            }

            Filtered++;
        }
    }
}
