using Yolla.Harvester.Commands;

namespace Yolla.Harvester;

// Yolla veri toplama aracı.
//
// Akış: scripts/prepare-osm-data.ps1 ham OSM verisini GeoJSONSeq'e çevirir,
// bu araç da onu okuyup veritabanına yazar ve zenginleştirir.
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        var command = args[0].ToLowerInvariant();
        var commandArgs = args.Skip(1).ToArray();

        try
        {
            return command switch
            {
                "inspect" => await InspectCommand.RunAsync(commandArgs),
                _ => UnknownCommand(command)
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"HATA: {ex.Message}");
            return 1;
        }
    }

    private static bool IsHelp(string arg) =>
        arg is "-h" or "--help" or "help" or "-?" or "/?";

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Bilinmeyen komut: {command}");
        Console.Error.WriteLine();
        PrintUsage();
        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            Yolla Harvester - OpenStreetMap veri toplama aracı

            KULLANIM
              dotnet run --project src/Yolla.Harvester -- <komut> [seçenekler]

            KOMUTLAR
              inspect <dosya.geojsonl>   Veri dosyasını analiz eder: kaç kayıt var, hangi
                                         kategorilere düşüyor, kaçı eleniyor. Veritabanına
                                         yazmadan önce veri kalitesini görmek için.

            ÖNCE
              Ham OSM verisi hazırlanmalı:
                ./scripts/prepare-osm-data.ps1
            """);
    }
}
