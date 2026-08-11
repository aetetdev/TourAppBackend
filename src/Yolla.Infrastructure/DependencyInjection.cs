using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Yolla.Application.Discovery;
using Yolla.Application.Geo;
using Yolla.Infrastructure.Identity;
using Yolla.Infrastructure.Persistence;
using Yolla.Infrastructure.Services;

namespace Yolla.Infrastructure;

public static class DependencyInjection
{
    /// <summary>docker-compose'daki varsayılan geliştirme şifresi.</summary>
    private const string LocalDevelopmentPassword = "yolla_dev";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres tanımlı değil.");

        // Ortam belirsizse en kısıtlayıcı varsayım yapılır
        var environmentName = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production";

        // Yerel geliştirme şifresi appsettings.json'da açık duruyor (docker-compose ile aynı).
        // Üretime kadar taşınırsa gerçek bir güvenlik açığı olur; bu yüzden Development
        // dışındaki ortamlarda kullanılmasına izin verilmiyor.
        if (connectionString.Contains(LocalDevelopmentPassword, StringComparison.Ordinal)
            && !string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"'{environmentName}' ortamında yerel geliştirme şifresi kullanılamaz. "
                + "ConnectionStrings__Postgres ortam değişkenini tanımlayın.");
        }

        services.AddDbContext<YollaDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                {
                    // PostGIS tiplerinin (Point, Geometry) EF tarafından tanınması için
                    npgsql.UseNetTopologySuite();
                    npgsql.MigrationsAssembly(typeof(YollaDbContext).Assembly.FullName);
                })
                // Tablo/kolon adları PostgreSQL konvansiyonunda: places, quality_score
                .UseSnakeCaseNamingConvention());

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 8;
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = false;
            })
            .AddRoles<IdentityRole<int>>()
            .AddEntityFrameworkStores<YollaDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IGeoService, GeoService>();
        services.AddScoped<IDiscoveryService, DiscoveryService>();

        var redisConnection = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(redisConnection));
        }

        return services;
    }
}
