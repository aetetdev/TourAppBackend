using Npgsql;
using NpgsqlTypes;
using Yolla.Application.Places;

namespace Yolla.Harvester.Enrichment;

/// <summary>
/// Wikidata kimliği olan yerleri fotoğraf, atıf bilgisi ve açıklama ile zenginleştirir.
/// </summary>
/// <remarks>
/// Fotoğraf, kart destesinin olmazsa olmazı: fotoğrafsız yer kullanıcıya gösterilemez.
/// Fotoğrafla birlikte fotoğrafçı adı ve lisans da kaydedilir - Commons görsellerinin
/// çoğu CC BY-SA ve atıf göstermek hukuki zorunluluk.
///
/// Zenginleştirme sonrası kalite puanı yeniden hesaplanır; fotoğraf tek başına 25 puan
/// getirdiği için birçok kayıt bu adımda feed eşiğini geçer.
/// </remarks>
public sealed class PlaceEnricher(
    NpgsqlDataSource dataSource,
    WikidataClient wikidataClient,
    CommonsClient commonsClient,
    WikipediaClient wikipediaClient)
{
    private const int CommandTimeoutSeconds = 300;

    public async Task<EnrichmentResult> EnrichAsync(
        int? limit,
        bool refreshExisting,
        IProgress<EnrichmentProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var candidates = await LoadCandidatesAsync(limit, refreshExisting, cancellationToken);
        var result = new EnrichmentResult { Total = candidates.Count };

        foreach (var batch in candidates.Chunk(WikidataClient.BatchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();

            await EnrichBatchAsync(batch, result, cancellationToken);

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
        EnrichmentResult result,
        CancellationToken cancellationToken)
    {
        var entities = await wikidataClient.GetEntitiesAsync(
            batch.Select(x => x.WikidataId).ToList(), cancellationToken);

        // --- Fotoğraflar ---

        var fileNames = entities.Values
            .Select(x => x.ImageFileName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var photos = new Dictionary<string, CommonsPhoto>(StringComparer.OrdinalIgnoreCase);

        foreach (var chunk in fileNames.Chunk(CommonsClient.BatchSize))
        {
            foreach (var (key, value) in await commonsClient.GetPhotosAsync(chunk, cancellationToken))
            {
                photos[key] = value;
            }
        }

        // --- Wikipedia özetleri ---

        var turkishTitles = CollectTitles(batch, entities, turkish: true);
        var englishTitles = CollectTitles(batch, entities, turkish: false);

        var turkishExtracts = await LoadExtractsAsync(turkishTitles, "tr", cancellationToken);
        var englishExtracts = await LoadExtractsAsync(englishTitles, "en", cancellationToken);

        // --- Güncelleme ---

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        foreach (var candidate in batch)
        {
            result.Processed++;

            entities.TryGetValue(candidate.WikidataId, out var entity);

            var photo = ResolvePhoto(entity, photos);
            var turkishTitle = entity?.TurkishWikipediaTitle ?? candidate.WikipediaTitle;
            var englishTitle = entity?.EnglishWikipediaTitle;

            var descriptionTr = Lookup(turkishExtracts, turkishTitle) ?? candidate.DescriptionTr;
            var descriptionEn = Lookup(englishExtracts, englishTitle) ?? candidate.DescriptionEn;

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
                HasWikidata = true,
                HasPhoto = photo is not null,
                HasWikipedia = turkishTitle is not null || englishTitle is not null,
                HasDescription = descriptionTr is not null,
                HasNameEn = candidate.NameEn is not null,
                HasWebsite = candidate.HasWebsite,
                HasOpeningHours = candidate.HasOpeningHours,
                CategoryWeight = candidate.CategoryWeight
            });

            await UpdatePlaceAsync(
                connection,
                candidate.Id,
                photo,
                turkishTitle,
                descriptionTr,
                descriptionEn,
                score,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static CommonsPhoto? ResolvePhoto(
        WikidataEntity? entity,
        IReadOnlyDictionary<string, CommonsPhoto> photos)
    {
        if (entity?.ImageFileName is null)
        {
            return null;
        }

        var title = CommonsClient.NormalizeFileTitle(entity.ImageFileName);

        return photos.GetValueOrDefault(title);
    }

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
            entities.TryGetValue(candidate.WikidataId, out var entity);

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
            foreach (var (key, value) in await wikipediaClient.GetExtractsAsync(chunk, language, cancellationToken))
            {
                extracts[key] = value;
            }
        }

        return extracts;
    }

    private async Task<List<EnrichmentCandidate>> LoadCandidatesAsync(
        int? limit,
        bool refreshExisting,
        CancellationToken cancellationToken)
    {
        var sql = """
            SELECT
                p.id, p.wikidata_id, p.name, p.name_en, p.wikipedia_title,
                p.description_tr, p.description_en,
                p.website IS NOT NULL       AS has_website,
                p.opening_hours IS NOT NULL AS has_opening_hours,
                c.weight
            FROM places p
            JOIN categories c ON c.id = p.category_id
            WHERE p.wikidata_id IS NOT NULL
            """;

        // Varsayılan davranış: daha önce fotoğrafı çekilmiş kayıtlara dokunma
        if (!refreshExisting)
        {
            sql += " AND p.photo_url IS NULL";
        }

        sql += " ORDER BY p.quality_score DESC, p.id";

        if (limit is > 0)
        {
            sql += $" LIMIT {limit.Value}";
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
                WikidataId = reader.GetString(1),
                Name = reader.GetString(2),
                NameEn = reader.IsDBNull(3) ? null : reader.GetString(3),
                WikipediaTitle = reader.IsDBNull(4) ? null : reader.GetString(4),
                DescriptionTr = reader.IsDBNull(5) ? null : reader.GetString(5),
                DescriptionEn = reader.IsDBNull(6) ? null : reader.GetString(6),
                HasWebsite = reader.GetBoolean(7),
                HasOpeningHours = reader.GetBoolean(8),
                CategoryWeight = reader.GetInt16(9)
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
        command.Parameters.Add(new NpgsqlParameter("photo_url", NpgsqlDbType.Text)
        {
            Value = (object?)photo?.Url ?? DBNull.Value
        });
        command.Parameters.Add(new NpgsqlParameter("photo_author", NpgsqlDbType.Text)
        {
            Value = (object?)photo?.Author ?? DBNull.Value
        });
        command.Parameters.Add(new NpgsqlParameter("photo_license", NpgsqlDbType.Text)
        {
            Value = (object?)photo?.License ?? DBNull.Value
        });
        command.Parameters.Add(new NpgsqlParameter("photo_source", NpgsqlDbType.Text)
        {
            Value = (object?)photo?.DescriptionUrl ?? DBNull.Value
        });
        command.Parameters.Add(new NpgsqlParameter("wikipedia_title", NpgsqlDbType.Text)
        {
            Value = (object?)wikipediaTitle ?? DBNull.Value
        });
        command.Parameters.Add(new NpgsqlParameter("description_tr", NpgsqlDbType.Text)
        {
            Value = (object?)descriptionTr ?? DBNull.Value
        });
        command.Parameters.Add(new NpgsqlParameter("description_en", NpgsqlDbType.Text)
        {
            Value = (object?)descriptionEn ?? DBNull.Value
        });
        command.Parameters.AddWithValue("quality_score", qualityScore);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

public sealed record EnrichmentCandidate
{
    public required int Id { get; init; }
    public required string WikidataId { get; init; }
    public required string Name { get; init; }
    public string? NameEn { get; init; }
    public string? WikipediaTitle { get; init; }
    public string? DescriptionTr { get; init; }
    public string? DescriptionEn { get; init; }
    public bool HasWebsite { get; init; }
    public bool HasOpeningHours { get; init; }
    public short CategoryWeight { get; init; }
}

public sealed class EnrichmentResult
{
    public int Total { get; set; }
    public int Processed { get; set; }
    public int PhotosFound { get; set; }
    public int DescriptionsFound { get; set; }
}

public sealed record EnrichmentProgress
{
    public required int Processed { get; init; }
    public required int Total { get; init; }
    public required int PhotosFound { get; init; }
}
