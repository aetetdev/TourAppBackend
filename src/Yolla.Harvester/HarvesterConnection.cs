using Npgsql;

namespace Yolla.Harvester;

/// <summary>
/// Veritabanı bağlantısını çözer ve veri kaynağını kurar.
/// </summary>
/// <remarks>
/// Öncelik sırası: komut satırı argümanı, ardından <c>YOLLA_CONNECTION</c> ortam değişkeni,
/// son olarak yerel geliştirme varsayılanı (docker-compose ile aynı değerler).
/// Üretimde ortam değişkeni kullanılır; bağlantı dizesi koda gömülmez.
/// </remarks>
public static class HarvesterConnection
{
    public const string EnvironmentVariableName = "YOLLA_CONNECTION";

    private const string LocalDevelopmentDefault =
        "Host=localhost;Port=5432;Database=yolla;Username=yolla;Password=yolla_dev";

    public static string Resolve(string? explicitConnectionString)
    {
        if (!string.IsNullOrWhiteSpace(explicitConnectionString))
        {
            return explicitConnectionString;
        }

        var fromEnvironment = Environment.GetEnvironmentVariable(EnvironmentVariableName);

        return string.IsNullOrWhiteSpace(fromEnvironment) ? LocalDevelopmentDefault : fromEnvironment;
    }

    public static NpgsqlDataSource CreateDataSource(string? explicitConnectionString) =>
        new NpgsqlDataSourceBuilder(Resolve(explicitConnectionString)).Build();
}
