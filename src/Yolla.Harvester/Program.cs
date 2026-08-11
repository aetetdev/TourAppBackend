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

        using var cancellation = new CancellationTokenSource();

        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
            Console.WriteLine("İptal ediliyor...");
        };

        try
        {
            return command switch
            {
                "inspect" => await InspectCommand.RunAsync(commandArgs),
                "import-boundaries" => await ImportBoundariesCommand.RunAsync(commandArgs, cancellation.Token),
                "import-places" => await ImportPlacesCommand.RunAsync(commandArgs, cancellation.Token),
                "enrich" => await EnrichCommand.RunAsync(commandArgs, cancellation.Token),
                _ => UnknownCommand(command)
            };
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("İşlem iptal edildi.");
            return 130;
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
              inspect [dosya]            Veri dosyasını analiz eder: kaç kayıt var, hangi
                                         kategorilere düşüyor, kaçı eleniyor. Veritabanına
                                         yazmadan önce veri kalitesini görmek için.
                                         Varsayılan: data/poi.geojsonl

              import-boundaries          İl ve ilçe sınırlarını aktarır. Yerlerden ÖNCE
                                         çalıştırılmalı: il/ilçe ataması bu poligonlarla yapılır.
                --provinces <dosya>      Varsayılan: data/provinces.geojsonl
                --districts <dosya>      Varsayılan: data/districts.geojsonl

              import-places              Turistik yerleri aktarır. Tekrar çalıştırılabilir;
                                         kayıtlar (osm_type, osm_id) ile eşleşip güncellenir.
                --file <dosya>           Varsayılan: data/poi.geojsonl
                --include-hidden         Otel, turizm bürosu ve kamp alanlarını da aktarır.

              enrich                     Wikidata kimliği olan yerlere Commons'tan fotoğraf
                                         (fotoğrafçı ve lisans bilgisiyle) ve Wikipedia'dan
                                         özet ekler, kalite puanını yeniden hesaplar.
                --limit <sayı>           Yalnızca ilk N kaydı işler (deneme için).
                --refresh                Fotoğrafı olan kayıtları da yeniden çeker.
                --delay <ms>             İstekler arası bekleme. Varsayılan: 150

            ORTAK SEÇENEKLER
              --country <ISO2>           Varsayılan: TR
              --connection <dize>        Veritabanı bağlantısı. Verilmezse YOLLA_CONNECTION
                                         ortam değişkeni, o da yoksa yerel geliştirme ayarı.

            ÖNCE
              Ham OSM verisi hazırlanmalı:
                ./scripts/prepare-osm-data.ps1
            """);
    }
}
