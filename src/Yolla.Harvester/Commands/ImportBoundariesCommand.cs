using Yolla.Harvester.Import;
using Yolla.Harvester.Osm;

namespace Yolla.Harvester.Commands;

/// <summary>
/// İl ve ilçe sınırlarını veritabanına aktarır.
/// </summary>
/// <remarks>
/// Yer verisinden önce çalıştırılmalıdır: turistik yerlerin il/ilçe ataması bu poligonlarla yapılır.
/// </remarks>
public static class ImportBoundariesCommand
{
    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var options = new CommandLineArgs(args);

        var provincesPath = options.GetValue("provinces", Path.Combine("data", "provinces.geojsonl"))!;
        var districtsPath = options.GetValue("districts", Path.Combine("data", "districts.geojsonl"))!;
        var country = options.GetValue("country", "TR")!;

        if (!File.Exists(provincesPath))
        {
            Console.Error.WriteLine($"İl sınırları dosyası bulunamadı: {provincesPath}");
            Console.Error.WriteLine("Önce ./scripts/prepare-osm-data.ps1 çalıştırılmalı.");
            return 1;
        }

        await using var dataSource = HarvesterConnection.CreateDataSource(options.GetValue("connection"));
        var importer = new BoundaryImporter(dataSource);

        // --- İller ---

        Console.WriteLine($"İl sınırları okunuyor: {provincesPath}");
        var provinces = ReadBoundaries(provincesPath, adminLevel: 4, country);
        Console.WriteLine($"  {provinces.Count} il sınırı okundu.");

        var cityResult = await importer.ImportCitiesAsync(provinces, country, cancellationToken);
        PrintResult("İller", cityResult);

        // --- İlçeler ---

        if (!File.Exists(districtsPath))
        {
            Console.WriteLine($"İlçe dosyası bulunamadı ({districtsPath}), bu adım atlandı.");
            return 0;
        }

        Console.WriteLine($"İlçe sınırları okunuyor: {districtsPath}");
        var districts = ReadBoundaries(districtsPath, adminLevel: 6, country);
        Console.WriteLine($"  {districts.Count} ilçe sınırı okundu.");

        var districtResult = await importer.ImportDistrictsAsync(districts, country, cancellationToken);
        PrintResult("İlçeler", districtResult);

        return 0;
    }

    private static List<BoundaryImportRow> ReadBoundaries(string path, int adminLevel, string country)
    {
        var reader = new GeoJsonSeqReader();
        var rows = new List<BoundaryImportRow>();
        var prefix = $"{country.ToUpperInvariant()}-";

        foreach (var feature in reader.Read(path))
        {
            var row = BoundaryFeatureConverter.Convert(feature, adminLevel, prefix);

            if (row is not null)
            {
                rows.Add(row);
            }
        }

        return rows;
    }

    private static void PrintResult(string label, BoundaryImportResult result)
    {
        Console.WriteLine($"  {label}: {result.Inserted} eklendi, {result.Updated} güncellendi"
                          + (result.Unmatched > 0 ? $", {result.Unmatched} eşleşmedi" : string.Empty));
    }
}
