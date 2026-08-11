namespace Yolla.Harvester.Enrichment;

/// <summary>Wikidata varlıklarını toplu olarak çeker.</summary>
public sealed class WikidataClient(WikimediaHttpClient httpClient)
{
    // wbgetentities tek çağrıda en fazla 50 kimlik kabul eder
    public const int BatchSize = 50;

    public async Task<IReadOnlyDictionary<string, WikidataEntity>> GetEntitiesAsync(
        IReadOnlyList<string> wikidataIds,
        CancellationToken cancellationToken = default)
    {
        if (wikidataIds.Count == 0)
        {
            return new Dictionary<string, WikidataEntity>();
        }

        var ids = string.Join('|', wikidataIds.Take(BatchSize));

        var url = "https://www.wikidata.org/w/api.php"
                  + "?action=wbgetentities&format=json&props=claims%7Csitelinks%7Cdescriptions"
                  + "&languages=tr%7Cen"
                  + $"&ids={Uri.EscapeDataString(ids)}";

        var json = await httpClient.GetStringAsync(url, cancellationToken);

        return WikidataResponseParser.Parse(json);
    }
}

/// <summary>Commons dosyalarının adresini ve atıf bilgisini toplu olarak çeker.</summary>
public sealed class CommonsClient(WikimediaHttpClient httpClient)
{
    public const int BatchSize = 50;

    public async Task<IReadOnlyDictionary<string, CommonsPhoto>> GetPhotosAsync(
        IReadOnlyList<string> fileNames,
        CancellationToken cancellationToken = default)
    {
        if (fileNames.Count == 0)
        {
            return new Dictionary<string, CommonsPhoto>();
        }

        var titles = string.Join('|', fileNames.Take(BatchSize).Select(NormalizeFileTitle));

        var url = "https://commons.wikimedia.org/w/api.php"
                  + "?action=query&format=json&prop=imageinfo&iiprop=url%7Cextmetadata"
                  + $"&titles={Uri.EscapeDataString(titles)}";

        var json = await httpClient.GetStringAsync(url, cancellationToken);

        return CommonsResponseParser.Parse(json);
    }

    /// <summary>Wikidata dosya adını Commons başlığına çevirir: "Ayasofya.jpg" -> "File:Ayasofya.jpg"</summary>
    public static string NormalizeFileTitle(string fileName)
    {
        var trimmed = fileName.Trim().Replace('_', ' ');

        return trimmed.StartsWith("File:", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"File:{trimmed}";
    }

    /// <summary>
    /// Bir Commons kategorisindeki ilk fotoğrafları döndürür.
    /// </summary>
    /// <remarks>
    /// OSM'deki wikimedia_commons etiketi çoğunlukla dosyayı değil kategoriyi gösterir
    /// ("Category:Underground City of Kaymaklı"). Kategori içinden bir görsel seçilir.
    /// </remarks>
    public async Task<IReadOnlyList<string>> GetCategoryFilesAsync(
        string categoryTitle,
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        var url = "https://commons.wikimedia.org/w/api.php"
                  + "?action=query&format=json&list=categorymembers"
                  + "&cmtype=file&cmsort=sortkey"
                  + $"&cmlimit={limit}"
                  + $"&cmtitle={Uri.EscapeDataString(categoryTitle)}";

        var json = await httpClient.GetStringAsync(url, cancellationToken);

        return CommonsResponseParser.ParseCategoryMembers(json);
    }

    /// <summary>
    /// Verilen koordinatın çevresindeki fotoğrafları en yakından başlayarak döndürür.
    /// </summary>
    /// <remarks>
    /// Son çare kaynağı: Wikidata kaydı ve Commons etiketi olmayan yerler için. Yarıçap
    /// dar tutulur, çünkü 500 metre öteki bir fotoğraf başka bir yeri gösteriyor olabilir.
    /// </remarks>
    public async Task<IReadOnlyDictionary<string, CommonsPhoto>> SearchNearbyAsync(
        double longitude,
        double latitude,
        int radiusMeters = 150,
        int limit = 3,
        CancellationToken cancellationToken = default)
    {
        var coordinate = FormattableString.Invariant($"{latitude}|{longitude}");

        var url = "https://commons.wikimedia.org/w/api.php"
                  + "?action=query&format=json&generator=geosearch"
                  + "&ggsnamespace=6"
                  + $"&ggsradius={radiusMeters}&ggslimit={limit}"
                  + $"&ggscoord={Uri.EscapeDataString(coordinate)}"
                  + "&prop=imageinfo&iiprop=url%7Cextmetadata";

        var json = await httpClient.GetStringAsync(url, cancellationToken);

        return CommonsResponseParser.Parse(json);
    }
}

/// <summary>Wikipedia makalelerinin giriş paragrafını toplu olarak çeker.</summary>
public sealed class WikipediaClient(WikimediaHttpClient httpClient)
{
    // extracts eklentisi tek çağrıda en fazla 20 makale döndürür
    public const int BatchSize = 20;

    public async Task<IReadOnlyDictionary<string, string>> GetExtractsAsync(
        IReadOnlyList<string> titles,
        string language,
        CancellationToken cancellationToken = default)
    {
        if (titles.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        var joined = string.Join('|', titles.Take(BatchSize));

        var url = $"https://{language}.wikipedia.org/w/api.php"
                  + "?action=query&format=json&prop=extracts"
                  + "&exintro=1&explaintext=1&exlimit=20&redirects=1"
                  + $"&titles={Uri.EscapeDataString(joined)}";

        var json = await httpClient.GetStringAsync(url, cancellationToken);

        return WikipediaResponseParser.Parse(json);
    }

    /// <summary>
    /// Makalelerin öne çıkan görselinin Commons dosya adını döndürür.
    /// </summary>
    /// <remarks>
    /// Wikidata'da P18 özelliği olmayan ama Wikipedia makalesi bulunan yerler için:
    /// makalenin baş görseli genellikle o yerin fotoğrafıdır.
    /// </remarks>
    public async Task<IReadOnlyDictionary<string, string>> GetPageImagesAsync(
        IReadOnlyList<string> titles,
        string language,
        CancellationToken cancellationToken = default)
    {
        if (titles.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        var joined = string.Join('|', titles.Take(BatchSize));

        var url = $"https://{language}.wikipedia.org/w/api.php"
                  + "?action=query&format=json&prop=pageimages"
                  + "&piprop=name&redirects=1"
                  + $"&titles={Uri.EscapeDataString(joined)}";

        var json = await httpClient.GetStringAsync(url, cancellationToken);

        return WikipediaResponseParser.ParsePageImages(json);
    }
}
