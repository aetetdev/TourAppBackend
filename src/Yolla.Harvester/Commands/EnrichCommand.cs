using System.Diagnostics;
using Yolla.Harvester.Enrichment;

namespace Yolla.Harvester.Commands;

/// <summary>
/// Yerleri Wikimedia kaynaklarından gelen fotoğraf ve açıklamalarla zenginleştirir.
/// </summary>
public static class EnrichCommand
{
    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var options = new CommandLineArgs(args);

        var limit = int.TryParse(options.GetValue("limit"), out var parsedLimit) ? parsedLimit : (int?)null;
        var refreshExisting = options.HasFlag("refresh");
        var delayMs = int.TryParse(options.GetValue("delay"), out var parsedDelay) ? parsedDelay : 150;

        await using var dataSource = HarvesterConnection.CreateDataSource(options.GetValue("connection"));

        using var httpClient = new WikimediaHttpClient(delayBetweenRequests: TimeSpan.FromMilliseconds(delayMs));

        var enricher = new PlaceEnricher(
            dataSource,
            new WikidataClient(httpClient),
            new CommonsClient(httpClient),
            new WikipediaClient(httpClient));

        Console.WriteLine("Zenginleştirme başlıyor (Wikidata -> Commons fotoğraf + lisans, Wikipedia özet)...");

        if (refreshExisting)
        {
            Console.WriteLine("  --refresh: fotoğrafı olan kayıtlar da yeniden çekilecek.");
        }

        var stopwatch = Stopwatch.StartNew();
        var lastReport = 0;

        var progress = new Progress<EnrichmentProgress>(p =>
        {
            // Her 500 kayıtta bir rapor: konsolu boğmadan ilerleme göstermek için
            if (p.Processed - lastReport < 500 && p.Processed != p.Total)
            {
                return;
            }

            lastReport = p.Processed;

            var percentage = p.Total == 0 ? 100 : p.Processed * 100.0 / p.Total;

            Console.WriteLine($"  ... {p.Processed:N0}/{p.Total:N0} (%{percentage:N0}) "
                              + $"- {p.PhotosFound:N0} fotoğraf bulundu");
        });

        var result = await enricher.EnrichAsync(limit, refreshExisting, progress, cancellationToken);

        stopwatch.Stop();

        Console.WriteLine();
        Console.WriteLine($"  Aday kayıt            {result.Total,10:N0}");
        Console.WriteLine($"  İşlenen               {result.Processed,10:N0}");
        Console.WriteLine($"  Fotoğraf bulundu      {result.PhotosFound,10:N0}");
        Console.WriteLine($"  Açıklama eklendi      {result.DescriptionsFound,10:N0}");
        Console.WriteLine();
        Console.WriteLine($"  Süre: {stopwatch.Elapsed.TotalMinutes:N1} dk");

        return 0;
    }
}
