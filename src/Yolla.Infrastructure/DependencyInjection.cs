using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using Yolla.Application.Auth;
using Yolla.Application.Discovery;
using Yolla.Application.Content;
using Yolla.Application.Geo;
using Yolla.Application.Places;
using Yolla.Application.Routing;
using Yolla.Application.Trips;
using Yolla.Infrastructure.Auth;
using Yolla.Infrastructure.Routing;
using Yolla.Infrastructure.Identity;
using Yolla.Infrastructure.Persistence;
using Yolla.Infrastructure.Services;

namespace Yolla.Infrastructure;

public static class DependencyInjection
{
    /// <summary>docker-compose'daki varsayılan geliştirme şifresi.</summary>
    private const string LocalDevelopmentPassword = "yolla_dev";

    /// <param name="environment">
    /// Ortam bilgisi doğrudan barındırma katmanından alınır. Yapılandırmadan
    /// (<c>ASPNETCORE_ENVIRONMENT</c> anahtarı) okumak güvenilir değil: test altyapısı
    /// ortamı <c>UseEnvironment</c> ile ayarladığında bu anahtar yapılandırmaya yansımıyor
    /// ve uygulama kendini Production sanıyor.
    /// </param>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres tanımlı değil.");

        var environmentName = environment.EnvironmentName;

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
        services.AddScoped<IDeviceSessionService, DeviceSessionService>();
        services.AddScoped<ISwipeService, SwipeService>();
        services.AddScoped<IRouteService, RouteService>();
        services.AddScoped<IPlaceService, PlaceService>();
        services.AddScoped<IContentService, ContentService>();
        services.AddScoped<ITripService, TripService>();

        services.Configure<OsrmOptions>(configuration.GetSection(OsrmOptions.SectionName));

        // Rota motoru istemcisi havuzdan yönetilir; her istekte yeni bağlantı açmak
        // soket tükenmesine yol açar
        services.AddHttpClient<IRoutingClient, OsrmClient>((provider, client) =>
        {
            var osrm = configuration.GetSection(OsrmOptions.SectionName).Get<OsrmOptions>() ?? new OsrmOptions();

            client.Timeout = TimeSpan.FromSeconds(osrm.TimeoutSeconds);
        });

        services.AddJwtAuthentication(configuration, environmentName);

        var redisConnection = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(redisConnection));
        }

        return services;
    }
}
