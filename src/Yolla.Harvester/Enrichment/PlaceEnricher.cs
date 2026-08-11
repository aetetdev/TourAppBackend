using Npgsql;
using NpgsqlTypes;
using Yolla.Application.Places;

namespace Yolla.Harvester.Enrichment;

/// <summary>
/// Yerleri Wikimedia kaynaklarından gelen fotoğraf, atıf bilgisi ve açıklama ile zenginleştirir.
/// </summary>
/// <remarks>
/// Fotoğraf, kart destesinin olmazsa olmazı. Tek kaynak yetmediği için sırayla denenir:
///   1. Wikidata P18 - en güvenilir, doğrudan "bu yerin fotoğrafı" demek
///   2. OSM wikimedia_commons etiketi - dosya ya da kategori referansı
///   3. Wikipedia makalesinin öne çıkan görseli
///   4. Commons coğrafi araması - koordinat çevresindeki fotoğraflar (son çare)
///
/// Son adım isabetli olmayabilir (150 metre öteki fotoğraf başka bir yeri gösterebilir),
/// bu yüzden varsayılan olarak kapalıdır ve ayrı bir bayrakla açılır.
///
/// Fotoğrafla birlikte fotoğrafçı adı ve lisans da kaydedilir; Commons görsellerinin
/// çoğu CC BY-SA ve atıf göstermek hukuki zorunluluk.
/// </remarks>
public sealed class PlaceEnricher(
    NpgsqlDataSource dataSource,
    WikidataClient wikidataClient,
    CommonsClient commonsClient,
    WikipediaClient wikipediaClient)
{
    private const int CommandTimeoutSeconds = 300;
    private const int BatchSize = 50;

    public async Task<EnrichmentResult> EnrichAsync(
        EnrichmentOptions options,
        IProgress<EnrichmentProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var candidates = await LoadCandidatesAsync(options, cancellationToken);
        var result = new EnrichmentResult { Total = candidates.Count };

        foreach (var batch in candidates.Chunk(BatchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();

            await EnrichBatchAsync(batch, options, result, cancellationToken);

            progress?.Report(new EnrichmentProgress
            {
                Processed = result.Processed,
                Total = result.Total,
                PhotosFound = result.PhotosFound
            });
        }

        return result;
    }

    private async Task EnrichBatchAsync(
        EnrichmentCandidate[] batch,
        EnrichmentOptions options,
        EnrichmentResult result,
        CancellationToken cancellationToken)
    {
        // --- 1. Wikidata: fotoğraf adı, makale başlıkları, kısa tanım ---

        var wikidataIds = batch
            .Where(x => x.WikidataId is not null)
            .Select(x => x.WikidataId!)
            .ToList();

        var entities = wikidataIds.Count > 0
            ? await wikidataClient.GetEntitiesAsync(wikidataIds, cancellationToken)
            : new Dictionary<string, WikidataEntity>();

        // --- 2. Her kayıt için fotoğraf dosyası adayını belirle ---

        var fileNameByPlace = new Dictionary<int, string>();

        foreach (var candidate in batch)
        {
            var entity = GetEntity(entities, candidate);

            // Öncelik: Wikidata P18
            if (entity?.ImageFileName is not null)
            {
                fileNameByPlace[candidate.Id] = CommonsClient.NormalizeFileTitle(entity.ImageFileName);
                continue;
            }

            // Sonra: OSM'deki doğrudan dosya referansı
            if (candidate.CommonsRef is not null
                && candidate.CommonsRef.StartsWith("File:", StringComparison.OrdinalIgnoreCase))
            {
                fileNameByPlace[candidate.Id] = candidate.CommonsRef;
            }
        }

        // --- 3. Commons kategorilerinden görsel seç ---

        foreach (var candidate in batch.Where(x => !fileNameByPlace.ContainsKey(x.Id)))
        {
            if (candidate.CommonsRef is null
                || !candidate.CommonsRef.StartsWith("Category:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var files = await commonsClient.GetCategoryFilesAsync(
                candidate.CommonsRef, limit: 3, cancellationToken);

            if (files.Count > 0)
            {
                fileNameByPlace[candidate.Id] = files[0];
                result.FromCommonsCategory++;
            }
        }

        // --- 4. Wikipedia makalesinin öne çıkan görseli ---

        await ApplyPageImagesAsync(batch, entities, fileNameByPlace, result, cancellationToken);

        // --- 5. Dosya bilgilerini (adres, yazar, lisans) toplu çek ---

        var photos = await LoadPhotosAsync(fileNameByPlace.Values, cancellationToken);

        // --- 6. Wikipedia özetleri ---

        var turkishExtracts = await LoadExtractsAsync(
            CollectTitles(batch, entities, turkish: true), "tr", cancellationToken);

        var englishExtracts = await LoadExtractsAsync(
            CollectTitles(batch, entities, turkish: false), "en", cancellationToken);

        // --- 7. Kayıtları güncelle ---

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        foreach (var candidate in batch)
        {
            result.Processed++;

            var entity = GetEntity(entities, candidate);

            var photo = fileNameByPlace.TryGetValue(candidate.Id, out var fileName)
                ? photos.GetValueOrDefault(fileName)
                : null;

            // Son çare: koordinat çevresindeki fotoğraflar
            if (photo is null && options.UseGeoSearch)
            {
                photo = await FindNearbyPhotoAsync(candidate, options, cancellationToken);

                if (photo is not null)
                {
                    result.FromGeoSearch++;
                }
            }

            var turkishTitle = entity?.TurkishWikipediaTitle ?? candidate.WikipediaTitle;
            var englishTitle = entity?.EnglishWikipediaTitle;

            // Açıklama önceliği: Wikipedia özeti > mevcut > Wikidata kısa tanımı
            var descriptionTr = Lookup(turkishExtracts, turkishTitle)
                                ?? candidate.DescriptionTr
                                ?? entity?.TurkishDescription;

            var descriptionEn = Lookup(englishExtracts, englishTitle)
                                ?? candidate.DescriptionEn
                                ?? entity?.EnglishDescription;

            if (photo is not null)
            {
                result.PhotosFound++;
            }

            if (descriptionTr is not null && candidate.DescriptionTr != descriptionTr)
            {
                result.DescriptionsFound++;
            }

            var score = PlaceQualityScorer.Score(new PlaceQualityInput
            {
                Name = candidate.Name,
                HasWikidata = candidate.WikidataId is not null,
                HasPhoto = photo is not null,
                HasWikipedia = turkishTitle is not null || englishTitle is not null,
                HasDescription = descriptionTr is not null,
                HasNameEn = candidate.NameEn is not null,
                HasWebsite = candidate.HasWebsite,
                HasOpeningHours = candidate.HasOpeningHours,
                CategoryWeight = candidate.CategoryWeight
            });

            await UpdatePlaceAsync(
                connection, candidate.Id, photo, turkishTitle,
                descriptionTr, descriptionEn, score, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task ApplyPageImagesAsync(
        EnrichmentCandidate[] batch,
        IReadOnlyDictionary<string, WikidataEntity> entities,
        Dictionary<int, string> fileNameByPlace,
        EnrichmentResult result,
        CancellationToken cancellationToken)
    {
        var missing = batch.Where(x => !fileNameByPlace.ContainsKey(x.Id)).ToList();

        if (missing.Count == 0)
        {
            return;
        }

        foreach (var language in new[] { "tr", "en" })
        {
            var titleByPlace = new Dictionary<int, string>();

            foreach (var candidate in missing.Where(x => !fileNameByPlace.ContainsKey(x.Id)))
            {
                var entity = GetEntity(entities, candidate);

                var title = language == "tr"
                    ? entity?.TurkishWikipediaTitle ?? candidate.WikipediaTitle
                    : entity?.EnglishWikipediaTitle;

                if (!string.IsNullOrWhiteSpace(title))
                {
                    titleByPlace[candidate.Id] = title;
                }
            }

            if (titleByPlace.Count == 0)
            {
                continue;
            }

            var pageImages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var chunk in titleByPlace.Values.Distinct().Chunk(WikipediaClient.BatchSize))
            {
                foreach (var (key, value) in
                         await wikipediaClient.GetPageImagesAsync(chunk, language, cancellationToken))
                {
                    pageImages[key] = value;
                }
            }

            foreach (var (placeId, title) in titleByPlace)
            {
                if (fileNameByPlace.ContainsKey(placeId) || !pageImages.TryGetValue(title, out var image))
                {
                    continue;
                }

                fileNameByPlace[placeId] = CommonsClient.NormalizeFileTitle(image);
                result.FromPageImage++;
            }
        }
    }

    private async Task<CommonsPhoto?> FindNearbyPhotoAsync(
        EnrichmentCandidate candidate,
        EnrichmentOptions options,
        CancellationToken cancellationToken)
    {
        var nearby = await commonsClient.SearchNearbyAsync(
            candidate.Longitude,
            candidate.Latitude,
            options.GeoSearchRadiusMeters,
            limit: 5,
            cancellationToken);

        // Yakınlık tek başına yetmez: dosya adı yer adıyla örtüşmeyen fotoğraflar
        // büyük çoğunlukla başka bir yeri gösteriyor
        return nearby.Values.FirstOrDefault(photo =>
            PhotoRelevanceFilter.IsLikelyRelevant(candidate.Name, photo.FileTitle));
    }

    private async Task<Dictionary<string, CommonsPhoto>> LoadPhotosAsync(
        IEnumerable<string> fileNames,
        CancellationToken cancellationToken)
    {
        var photos = new Dictionary<string, CommonsPhoto>(StringComparer.OrdinalIgnoreCase);

        var distinct = fileNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        foreach (var chunk in distinct.Chunk(CommonsClient.BatchSize))
        {
            foreach (var (key, value) in await commonsClient.GetPhotosAsync(chunk, cancellationToken))
            {
                photos[key] = value;
            }
        }

        return photos;
    }

    private static WikidataEntity? GetEntity(
        IReadOnlyDictionary<string, WikidataEntity> entities,
        EnrichmentCandidate candidate) =>
        candidate.WikidataId is not null && entities.TryGetValue(candidate.WikidataId, out var entity)
            ? entity
            : null;

    private static string? Lookup(IReadOnlyDictionary<string, string> extracts, string? title) =>
        title is not null && extracts.TryGetValue(title, out var extract) ? extract : null;

    private static List<string> CollectTitles(
        EnrichmentCandidate[] batch,
        IReadOnlyDictionary<string, WikidataEntity> entities,
        bool turkish)
    {
        var titles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in batch)
        {
            var entity = GetEntity(entities, candidate);

            var title = turkish
                ? entity?.TurkishWikipediaTitle ?? candidate.WikipediaTitle
                : entity?.EnglishWikipediaTitle;

            if (!string.IsNullOrWhiteSpace(title))
            {
                titles.Add(title);
            }
        }

        return titles.ToList();
    }

    private async Task<Dictionary<string, string>> LoadExtractsAsync(
        List<string> titles,
        string language,
        CancellationToken cancellationToken)
    {
        var extracts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var chunk in titles.Chunk(WikipediaClient.BatchSize))
        {
            foreach (var (key, value) in
                     await wikipediaClient.GetExtractsAsync(chunk, language, cancellationToken))
            {
                extracts[key] = value;
            }
        }

        return extracts;
    }

    private async Task<List<EnrichmentCandidate>> LoadCandidatesAsync(
        EnrichmentOptions options,
        CancellationToken cancellationToken)
    {
        var sql = """
            SELECT
                p.id, p.wikidata_id, p.name, p.name_en, p.wikipedia_title, p.commons_ref,
                p.description_tr, p.description_en,
                p.website IS NOT NULL       AS has_website,
                p.opening_hours IS NOT NULL AS has_opening_hours,
                c.weight,
                ST_X(p.location::geometry)  AS longitude,
                ST_Y(p.location::geometry)  AS latitude
            FROM places p
            JOIN categories c ON c.id = p.category_id
            WHERE c.is_visible
            """;

        // Coğrafi arama açıksa her kayıt adaydır; değilse bir kaynağa bağlı olanlar
        if (!options.UseGeoSearch)
        {
            sql += """
                 AND (p.wikidata_id IS NOT NULL OR p.commons_ref IS NOT NULL
                      OR p.wikipedia_title IS NOT NULL)
                """;
        }

        if (!options.RefreshExisting)
        {
            sql += " AND p.photo_url IS NULL";
        }

        if (options.MinQualityScore > 0)
        {
            sql += $" AND p.quality_score >= {options.MinQualityScore}";
        }

        sql += " ORDER BY p.quality_score DESC, p.id";

        if (options.Limit is > 0)
        {
            sql += $" LIMIT {options.Limit.Value}";
        }

        await using var command = dataSource.CreateCommand(sql);
        command.CommandTimeout = CommandTimeoutSeconds;

        var candidates = new List<EnrichmentCandidate>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            candidates.Add(new EnrichmentCandidate
            {
                Id = reader.GetInt32(0),
                WikidataId = reader.IsDBNull(1) ? null : reader.GetString(1),
                Name = reader.GetString(2),
                NameEn = reader.IsDBNull(3) ? null : reader.GetString(3),
                WikipediaTitle = reader.IsDBNull(4) ? null : reader.GetString(4),
                CommonsRef = reader.IsDBNull(5) ? null : reader.GetString(5),
                DescriptionTr = reader.IsDBNull(6) ? null : reader.GetString(6),
                DescriptionEn = reader.IsDBNull(7) ? null : reader.GetString(7),
                HasWebsite = reader.GetBoolean(8),
                HasOpeningHours = reader.GetBoolean(9),
                CategoryWeight = reader.GetInt16(10),
                Longitude = reader.GetDouble(11),
                Latitude = reader.GetDouble(12)
            });
        }

        return candidates;
    }

    private static async Task UpdatePlaceAsync(
        NpgsqlConnection connection,
        int placeId,
        CommonsPhoto? photo,
        string? wikipediaTitle,
        string? descriptionTr,
        string? descriptionEn,
        short qualityScore,
        CancellationToken cancellationToken)
    {
        // COALESCE: bu turda bulunamayan bilgi, önceden kayıtlı olanı silmemeli
        const string sql = """
            UPDATE places SET
                photo_url        = COALESCE(@photo_url, photo_url),
                photo_author     = COALESCE(@photo_author, photo_author),
                photo_license    = COALESCE(@photo_license, photo_license),
                photo_source     = COALESCE(@photo_source, photo_source),
                wikipedia_title  = COALESCE(@wikipedia_title, wikipedia_title),
                description_tr   = COALESCE(@description_tr, description_tr),
                description_en   = COALESCE(@description_en, description_en),
                quality_score    = @quality_score,
                updated_at       = now()
            WHERE id = @id;
            """;

        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("id", placeId);
        AddText(command, "photo_url", photo?.Url);
        AddText(command, "photo_author", photo?.Author);
        AddText(command, "photo_license", photo?.License);
        AddText(command, "photo_source", photo?.DescriptionUrl);
        AddText(command, "wikipedia_title", wikipediaTitle);
        AddText(command, "description_tr", descriptionTr);
        AddText(command, "description_en", descriptionEn);
        command.Parameters.AddWithValue("quality_score", qualityScore);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddText(NpgsqlCommand command, string name, string? value) =>
        command.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Text)
        {
            Value = (object?)value ?? DBNull.Value
        });
}

public sealed record EnrichmentOptions
{
    public int? Limit { get; init; }

    /// <summary>Fotoğrafı olan kayıtları da yeniden işler.</summary>
    public bool RefreshExisting { get; init; }

    /// <summary>
    /// Hiçbir kaynağa bağlı olmayan kayıtlar için koordinat çevresinde fotoğraf arar.
    /// İsabet garantisi yoktur, bu yüzden varsayılan olarak kapalıdır.
    /// </summary>
    public bool UseGeoSearch { get; init; }

    public int GeoSearchRadiusMeters { get; init; } = 150;

    /// <summary>Bu puanın altındaki kayıtlar işlenmez; geniş taramaları sınırlamak için.</summary>
    public int MinQualityScore { get; init; }
}

public sealed record EnrichmentCandidate
{
    public required int Id { get; init; }
    public string? WikidataId { get; init; }
    public required string Name { get; init; }
    public string? NameEn { get; init; }
    public string? WikipediaTitle { get; init; }
    public string? CommonsRef { get; init; }
    public string? DescriptionTr { get; init; }
    public string? DescriptionEn { get; init; }
    public bool HasWebsite { get; init; }
    public bool HasOpeningHours { get; init; }
    public short CategoryWeight { get; init; }
    public double Longitude { get; init; }
    public double Latitude { get; init; }
}

public sealed class EnrichmentResult
{
    public int Total { get; set; }
    public int Processed { get; set; }
    public int PhotosFound { get; set; }
    public int DescriptionsFound { get; set; }

    /// <summary>Commons kategorisinden seçilen görsel sayısı.</summary>
    public int FromCommonsCategory { get; set; }

    /// <summary>Wikipedia makalesinin öne çıkan görselinden gelen sayı.</summary>
    public int FromPageImage { get; set; }

    /// <summary>Koordinat aramasıyla bulunan sayı.</summary>
    public int FromGeoSearch { get; set; }
}

public sealed record EnrichmentProgress
{
    public required int Processed { get; init; }
    public required int Total { get; init; }
    public required int PhotosFound { get; init; }
}
