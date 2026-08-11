using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Yolla.Infrastructure.Persistence;

namespace Yolla.Api.IntegrationTests;

/// <summary>
/// Testler için gerçek bir PostGIS örneği başlatır ve şemayı uygular.
/// </summary>
/// <remarks>
/// Sahte veritabanı (in-memory) kullanılamaz: içe aktarma mantığının tamamı PostGIS
/// fonksiyonlarına ve ON CONFLICT davranışına dayanıyor. Bunlar ancak gerçek sunucuda
/// doğrulanabilir.
/// </remarks>
public sealed class PostgisFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgis/postgis:17-3.5")
        .WithDatabase("yolla_test")
        .WithUsername("yolla")
        .WithPassword("yolla_test")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public NpgsqlDataSource DataSource { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        DataSource = new NpgsqlDataSourceBuilder(ConnectionString).Build();

        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();
    }

    public YollaDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<YollaDbContext>()
            .UseNpgsql(ConnectionString, npgsql =>
            {
                npgsql.UseNetTopologySuite();
                npgsql.MigrationsAssembly(typeof(YollaDbContext).Assembly.FullName);
            })
            .UseSnakeCaseNamingConvention()
            .Options;

        return new YollaDbContext(options);
    }

    public async Task DisposeAsync()
    {
        await DataSource.DisposeAsync();
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class PostgisCollection : ICollectionFixture<PostgisFixture>
{
    public const string Name = "postgis";
}
