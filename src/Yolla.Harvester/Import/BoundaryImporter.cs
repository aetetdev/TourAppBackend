using NetTopologySuite.IO;
using Npgsql;
using NpgsqlTypes;

namespace Yolla.Harvester.Import;

/// <summary>
/// İl ve ilçe sınırlarını veritabanına aktarır.
/// </summary>
/// <remarks>
/// Sınırlar yer verisinden önce yüklenmelidir: turistik yerlerin hangi ile/ilçeye ait
/// olduğu bu poligonlarla belirlenir.
///
/// OSM'den gelen poligonlar bazen geçersizdir (kendisiyle kesişen kenarlar). Bu yüzden
/// her geometri ST_MakeValid'den geçirilir; aksi halde ST_Intersects sorguları hata verir.
/// </remarks>
public sealed class BoundaryImporter(NpgsqlDataSource dataSource)
{
    // Sınır poligonları büyük; 1000 ilçeyi 81 ille kesiştirmek varsayılan 30 saniyeyi aşıyor
    private const int CommandTimeoutSeconds = 900;

    private static readonly WKBWriter WkbWriter = new();

    /// <summary>İl sınırlarını (admin_level=4) cities tablosuna aktarır.</summary>
    public async Task<BoundaryImportResult> ImportCitiesAsync(
        IReadOnlyList<BoundaryImportRow> rows,
        string countryIso2,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);

        if (rows.Count == 0)
        {
            return new BoundaryImportResult();
        }

        const string upsertSql = """
            WITH valid AS MATERIALIZED (
                SELECT
                    s.osm_relation_id,
                    s.name,
                    s.name_en,
                    s.name_normalized,
                    s.slug,
                    ST_MakeValid(ST_GeomFromWKB(s.boundary_wkb, 4326)) AS boundary
                FROM staging_boundaries s
            ),
            upserted AS (
                INSERT INTO cities (
                    country_id, osm_relation_id, name, name_normalized, name_en, slug,
                    center, boundary, is_active, created_at
                )
                SELECT DISTINCT ON (v.osm_relation_id)
                    co.id,
                    v.osm_relation_id,
                    v.name,
                    v.name_normalized,
                    v.name_en,
                    v.slug,
                    -- Merkez nokta: centroid poligon dışına düşebilir, PointOnSurface düşmez
                    ST_PointOnSurface(v.boundary)::geography,
                    v.boundary,
                    true,
                    now()
                FROM valid v
                CROSS JOIN countries co
                WHERE co.iso2 = @iso2
                  AND NOT ST_IsEmpty(v.boundary)
                ORDER BY v.osm_relation_id
                ON CONFLICT (osm_relation_id) WHERE osm_relation_id IS NOT NULL
                DO UPDATE SET
                    name            = EXCLUDED.name,
                    name_normalized = EXCLUDED.name_normalized,
                    name_en         = EXCLUDED.name_en,
                    slug            = EXCLUDED.slug,
                    center          = EXCLUDED.center,
                    boundary        = EXCLUDED.boundary,
                    updated_at      = now()
                RETURNING (xmax = 0) AS was_inserted
            )
            SELECT
                count(*) FILTER (WHERE was_inserted)     AS inserted,
                count(*) FILTER (WHERE NOT was_inserted) AS updated
            FROM upserted;
            """;

        return await RunAsync(rows, upsertSql, countryIso2, cancellationToken);
    }

    /// <summary>İlçe sınırlarını (admin_level=6) districts tablosuna aktarır.</summary>
    public async Task<BoundaryImportResult> ImportDistrictsAsync(
        IReadOnlyList<BoundaryImportRow> rows,
        string countryIso2,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);

        if (rows.Count == 0)
        {
            return new BoundaryImportResult();
        }

        // İlçenin bağlı olduğu il, ilçe yüzeyindeki bir noktanın hangi il poligonuna
        // düştüğüne bakılarak bulunur. İl sınırları önceden yüklenmiş olmalıdır.
        const string upsertSql = """
            WITH valid AS MATERIALIZED (
                SELECT
                    s.osm_relation_id,
                    s.name,
                    s.name_normalized,
                    s.slug,
                    ST_MakeValid(ST_GeomFromWKB(s.boundary_wkb, 4326)) AS boundary
                FROM staging_boundaries s
            ),
            -- Bağlantı noktası bir kez hesaplanır; JOIN içinde bırakılırsa her il
            -- karşılaştırmasında yeniden üretilir ve sorgu dakikalarca sürer
            located AS MATERIALIZED (
                SELECT
                    v.*,
                    ST_PointOnSurface(v.boundary) AS anchor
                FROM valid v
                WHERE NOT ST_IsEmpty(v.boundary)
            ),
            upserted AS (
                INSERT INTO districts (
                    city_id, osm_relation_id, name, name_normalized, slug, boundary, created_at
                )
                SELECT DISTINCT ON (l.osm_relation_id)
                    ci.id,
                    l.osm_relation_id,
                    l.name,
                    l.name_normalized,
                    l.slug,
                    l.boundary,
                    now()
                FROM located l
                JOIN cities ci
                    ON ST_Intersects(ci.boundary, l.anchor)
                JOIN countries co
                    ON co.id = ci.country_id AND co.iso2 = @iso2
                ORDER BY l.osm_relation_id, ci.id
                ON CONFLICT (osm_relation_id) WHERE osm_relation_id IS NOT NULL
                DO UPDATE SET
                    city_id         = EXCLUDED.city_id,
                    name            = EXCLUDED.name,
                    name_normalized = EXCLUDED.name_normalized,
                    slug            = EXCLUDED.slug,
                    boundary        = EXCLUDED.boundary,
                    updated_at      = now()
                RETURNING (xmax = 0) AS was_inserted
            )
            SELECT
                count(*) FILTER (WHERE was_inserted)     AS inserted,
                count(*) FILTER (WHERE NOT was_inserted) AS updated
            FROM upserted;
            """;

        return await RunAsync(rows, upsertSql, countryIso2, cancellationToken);
    }

    private async Task<BoundaryImportResult> RunAsync(
        IReadOnlyList<BoundaryImportRow> rows,
        string upsertSql,
        string countryIso2,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(countryIso2);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await CreateStagingTableAsync(connection, cancellationToken);
        await CopyAsync(connection, rows, cancellationToken);

        await using var command = new NpgsqlCommand(upsertSql, connection)
        {
            CommandTimeout = CommandTimeoutSeconds
        };

        command.Parameters.AddWithValue("iso2", countryIso2.ToUpperInvariant());

        var inserted = 0;
        var updated = 0;

        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                inserted = (int)reader.GetInt64(0);
                updated = (int)reader.GetInt64(1);
            }
        }

        await transaction.CommitAsync(cancellationToken);

        return new BoundaryImportResult
        {
            Staged = rows.Count,
            Inserted = inserted,
            Updated = updated,
            Unmatched = rows.Count - (inserted + updated)
        };
    }

    private static async Task CreateStagingTableAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            CREATE TEMP TABLE staging_boundaries (
                osm_relation_id bigint NOT NULL,
                name            text   NOT NULL,
                name_en         text,
                name_normalized text   NOT NULL,
                slug            text   NOT NULL,
                boundary_wkb    bytea  NOT NULL
            ) ON COMMIT DROP;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task CopyAsync(
        NpgsqlConnection connection,
        IReadOnlyList<BoundaryImportRow> rows,
        CancellationToken cancellationToken)
    {
        const string copySql = """
            COPY staging_boundaries (
                osm_relation_id, name, name_en, name_normalized, slug, boundary_wkb
            ) FROM STDIN (FORMAT BINARY)
            """;

        await using var writer = await connection.BeginBinaryImportAsync(copySql, cancellationToken);

        foreach (var row in rows)
        {
            await writer.StartRowAsync(cancellationToken);

            await writer.WriteAsync(row.OsmRelationId, NpgsqlDbType.Bigint, cancellationToken);
            await writer.WriteAsync(row.Name, NpgsqlDbType.Text, cancellationToken);

            if (row.NameEn is null)
            {
                await writer.WriteNullAsync(cancellationToken);
            }
            else
            {
                await writer.WriteAsync(row.NameEn, NpgsqlDbType.Text, cancellationToken);
            }

            await writer.WriteAsync(row.NameNormalized, NpgsqlDbType.Text, cancellationToken);
            await writer.WriteAsync(row.Slug, NpgsqlDbType.Text, cancellationToken);
            await writer.WriteAsync(WkbWriter.Write(row.Boundary), NpgsqlDbType.Bytea, cancellationToken);
        }

        await writer.CompleteAsync(cancellationToken);
    }
}

public sealed class BoundaryImportResult
{
    public int Staged { get; set; }

    public int Inserted { get; set; }

    public int Updated { get; set; }

    /// <summary>
    /// Aktarılamayan kayıt sayısı. İlçelerde genellikle sebep, ilçenin hiçbir il sınırının
    /// içine düşmemesidir (sınır ülkelerden sızan kayıtlar).
    /// </summary>
    public int Unmatched { get; set; }
}
