using Npgsql;
using NpgsqlTypes;

namespace Yolla.Harvester.Import;

/// <summary>
/// Hazırlanmış yer kayıtlarını veritabanına toplu olarak aktarır.
/// </summary>
/// <remarks>
/// Akış: kayıtlar önce geçici bir tabloya COPY ile yazılır (satır satır INSERT yüz binlerce
/// kayıtta dakikalar sürerdi), sonra tek bir SQL ifadesiyle asıl tabloya aktarılır.
/// İl/ilçe ataması bu ifadede PostGIS ile yapılır: nokta hangi sınır poligonunun içindeyse
/// o şehre bağlanır. Metin karşılaştırması yapılmaz - eski toplayıcı "Kale" ilçesi ile
/// "Kale" adlı turistik yeri karıştırabiliyordu.
/// </remarks>
public sealed class PlaceImporter(NpgsqlDataSource dataSource)
{
    private const int BatchSize = 20_000;

    // Her kayıt 81 il poligonuyla kesiştiriliyor; varsayılan 30 saniye yetmiyor
    private const int CommandTimeoutSeconds = 900;

    public async Task<PlaceImportResult> ImportAsync(
        IEnumerable<PlaceImportRow> rows,
        string countryIso2,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentException.ThrowIfNullOrWhiteSpace(countryIso2);

        var result = new PlaceImportResult();

        foreach (var batch in Chunk(rows, BatchSize))
        {
            var batchResult = await ImportBatchAsync(batch, countryIso2, cancellationToken);

            result.Staged += batchResult.Staged;
            result.Inserted += batchResult.Inserted;
            result.Updated += batchResult.Updated;
            result.WithoutCity += batchResult.WithoutCity;
        }

        return result;
    }

    private async Task<PlaceImportResult> ImportBatchAsync(
        IReadOnlyList<PlaceImportRow> batch,
        string countryIso2,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await CreateStagingTableAsync(connection, cancellationToken);
        await CopyAsync(connection, batch, cancellationToken);

        var (inserted, updated) = await UpsertAsync(connection, countryIso2, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new PlaceImportResult
        {
            Staged = batch.Count,
            Inserted = inserted,
            Updated = updated,
            // Sınır poligonlarının içine düşmeyen kayıtlar (deniz, sınır dışı, hatalı koordinat)
            WithoutCity = batch.Count - (inserted + updated)
        };
    }

    private static async Task CreateStagingTableAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        // Geometri yerine ham enlem/boylam yazılıyor: COPY tarafı böylece tip eklentisi
        // gerektirmiyor, nokta SQL içinde kuruluyor.
        const string sql = """
            CREATE TEMP TABLE staging_places (
                osm_type         int      NOT NULL,
                osm_id           bigint   NOT NULL,
                name             text     NOT NULL,
                name_en          text,
                slug             text     NOT NULL,
                category_key     text     NOT NULL,
                longitude        double precision NOT NULL,
                latitude         double precision NOT NULL,
                address          text,
                website          text,
                opening_hours    text,
                wikidata_id      text,
                wikipedia_title  text,
                commons_ref      text,
                description_tr   text,
                description_en   text,
                quality_score    smallint NOT NULL
            ) ON COMMIT DROP;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task CopyAsync(
        NpgsqlConnection connection,
        IReadOnlyList<PlaceImportRow> batch,
        CancellationToken cancellationToken)
    {
        const string copySql = """
            COPY staging_places (
                osm_type, osm_id, name, name_en, slug, category_key,
                longitude, latitude, address, website, opening_hours,
                wikidata_id, wikipedia_title, commons_ref,
                description_tr, description_en, quality_score
            ) FROM STDIN (FORMAT BINARY)
            """;

        await using var writer = await connection.BeginBinaryImportAsync(copySql, cancellationToken);

        foreach (var row in batch)
        {
            await writer.StartRowAsync(cancellationToken);

            await writer.WriteAsync((int)row.OsmType, NpgsqlDbType.Integer, cancellationToken);
            await writer.WriteAsync(row.OsmId, NpgsqlDbType.Bigint, cancellationToken);
            await writer.WriteAsync(row.Name, NpgsqlDbType.Text, cancellationToken);
            await WriteNullableAsync(writer, row.NameEn, cancellationToken);
            await writer.WriteAsync(row.Slug, NpgsqlDbType.Text, cancellationToken);
            await writer.WriteAsync(row.CategoryKey, NpgsqlDbType.Text, cancellationToken);
            await writer.WriteAsync(row.Location.X, NpgsqlDbType.Double, cancellationToken);
            await writer.WriteAsync(row.Location.Y, NpgsqlDbType.Double, cancellationToken);
            await WriteNullableAsync(writer, row.Address, cancellationToken);
            await WriteNullableAsync(writer, row.Website, cancellationToken);
            await WriteNullableAsync(writer, row.OpeningHours, cancellationToken);
            await WriteNullableAsync(writer, row.WikidataId, cancellationToken);
            await WriteNullableAsync(writer, row.WikipediaTitle, cancellationToken);
            await WriteNullableAsync(writer, row.CommonsRef, cancellationToken);
            await WriteNullableAsync(writer, row.DescriptionTr, cancellationToken);
            await WriteNullableAsync(writer, row.DescriptionEn, cancellationToken);
            await writer.WriteAsync(row.QualityScore, NpgsqlDbType.Smallint, cancellationToken);
        }

        await writer.CompleteAsync(cancellationToken);
    }

    private static async Task<(int Inserted, int Updated)> UpsertAsync(
        NpgsqlConnection connection,
        string countryIso2,
        CancellationToken cancellationToken)
    {
        // DISTINCT ON: bir nokta iki sınır poligonuna birden düşerse (sınır çakışmaları)
        // tek satır kalır; aksi halde ON CONFLICT aynı satırı iki kez güncellemeye çalışır.
        //
        // ST_Intersects (ST_Contains değil): sınır çizgisi üzerindeki noktalar da şehre bağlansın.
        //
        // Kalite puanına kategori ağırlığı burada ekleniyor, çünkü ağırlık veritabanında tanımlı.
        const string sql = """
            WITH matched AS (
                SELECT DISTINCT ON (s.osm_type, s.osm_id)
                    s.*,
                    ci.id           AS city_id,
                    ci.country_id   AS country_id,
                    d.id            AS district_id,
                    cat.id          AS category_id,
                    cat.weight      AS category_weight
                FROM staging_places s
                JOIN categories cat
                    ON cat.key = s.category_key
                JOIN cities ci
                    ON ST_Intersects(ci.boundary, ST_SetSRID(ST_MakePoint(s.longitude, s.latitude), 4326))
                JOIN countries co
                    ON co.id = ci.country_id AND co.iso2 = @iso2
                LEFT JOIN districts d
                    ON d.city_id = ci.id
                   AND ST_Intersects(d.boundary, ST_SetSRID(ST_MakePoint(s.longitude, s.latitude), 4326))
                ORDER BY s.osm_type, s.osm_id, ci.id, d.id
            ),
            upserted AS (
                INSERT INTO places (
                    country_id, city_id, district_id, category_id,
                    osm_type, osm_id, name, name_en, slug, location,
                    address, website, opening_hours,
                    wikidata_id, wikipedia_title, commons_ref, description_tr, description_en,
                    quality_score, is_active, created_at
                )
                SELECT
                    m.country_id, m.city_id, m.district_id, m.category_id,
                    m.osm_type, m.osm_id, m.name, m.name_en, m.slug,
                    ST_SetSRID(ST_MakePoint(m.longitude, m.latitude), 4326)::geography,
                    m.address, m.website, m.opening_hours,
                    m.wikidata_id, m.wikipedia_title, m.commons_ref, m.description_tr, m.description_en,
                    LEAST(100, m.quality_score + COALESCE(m.category_weight, 0))::smallint,
                    true, now()
                FROM matched m
                ON CONFLICT (osm_type, osm_id) DO UPDATE SET
                    city_id         = EXCLUDED.city_id,
                    district_id     = EXCLUDED.district_id,
                    category_id     = EXCLUDED.category_id,
                    name            = EXCLUDED.name,
                    name_en         = EXCLUDED.name_en,
                    slug            = EXCLUDED.slug,
                    location        = EXCLUDED.location,
                    address         = EXCLUDED.address,
                    website         = EXCLUDED.website,
                    opening_hours   = EXCLUDED.opening_hours,
                    wikidata_id     = COALESCE(EXCLUDED.wikidata_id, places.wikidata_id),
                    wikipedia_title = COALESCE(EXCLUDED.wikipedia_title, places.wikipedia_title),
                    commons_ref     = COALESCE(EXCLUDED.commons_ref, places.commons_ref),
                    -- Zenginleştirmeden gelen açıklama (Wikipedia özeti) OSM'deki kısa
                    -- description etiketinden değerli; yeniden içe aktarma onu ezmemeli
                    description_tr  = COALESCE(places.description_tr, EXCLUDED.description_tr),
                    description_en  = COALESCE(places.description_en, EXCLUDED.description_en),
                    -- Zenginleştirme sonrası fotoğraf puanı eklenmiş olabilir; düşürmüyoruz
                    quality_score   = GREATEST(places.quality_score, EXCLUDED.quality_score),
                    updated_at      = now()
                RETURNING (xmax = 0) AS was_inserted
            )
            SELECT
                count(*) FILTER (WHERE was_inserted)     AS inserted,
                count(*) FILTER (WHERE NOT was_inserted) AS updated
            FROM upserted;
            """;

        await using var command = new NpgsqlCommand(sql, connection)
        {
            CommandTimeout = CommandTimeoutSeconds
        };

        command.Parameters.AddWithValue("iso2", countryIso2.ToUpperInvariant());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return (0, 0);
        }

        return ((int)reader.GetInt64(0), (int)reader.GetInt64(1));
    }

    private static async Task WriteNullableAsync(
        NpgsqlBinaryImporter writer,
        string? value,
        CancellationToken cancellationToken)
    {
        if (value is null)
        {
            await writer.WriteNullAsync(cancellationToken);
            return;
        }

        await writer.WriteAsync(value, NpgsqlDbType.Text, cancellationToken);
    }

    private static IEnumerable<IReadOnlyList<T>> Chunk<T>(IEnumerable<T> source, int size)
    {
        var buffer = new List<T>(size);

        foreach (var item in source)
        {
            buffer.Add(item);

            if (buffer.Count < size)
            {
                continue;
            }

            yield return buffer;
            buffer = new List<T>(size);
        }

        if (buffer.Count > 0)
        {
            yield return buffer;
        }
    }
}

public sealed class PlaceImportResult
{
    /// <summary>Geçici tabloya yazılan kayıt sayısı.</summary>
    public int Staged { get; set; }

    public int Inserted { get; set; }

    public int Updated { get; set; }

    /// <summary>Hiçbir il sınırının içine düşmediği için atlanan kayıt sayısı.</summary>
    public int WithoutCity { get; set; }
}
