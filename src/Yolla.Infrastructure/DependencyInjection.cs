using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using Yolla.Application.Auth;
using Yolla.Application.Common;
using Yolla.Application.Content;
using Yolla.Application.Discovery;
using Yolla.Application.Geo;
using Yolla.Application.Places;
using Yolla.Application.Rewards;
using Yolla.Application.Routing;
using Yolla.Application.Trips;
using Yolla.Infrastructure.Auth;
using Yolla.Infrastructure.Caching;
using Yolla.Infrastructure.Routing;
using Yolla.Infrastructure.Identity;
using Yolla.Infrastructure.Persistence;
using Yolla.Infrastructure.Services;

namespace Yolla.Infrastructure;

public static class DependencyInjection
{
    /// <summary>docker-compose'daki varsayılan geliştirme şifresi.</summary>
    private const string LocalDevelopmentPassword = "yolla_dev";

    /// <summary>
    /// Önbelleği kurar. Redis tanımlı değilse veya bağlanılamazsa uygulama çalışmaya
    /// devam eder; yalnızca her istek veri kaynağına gider.
    /// </summary>
    private static void AddCaching(IServiceCollection services, IConfiguration configuration)
    {
        var redisConnection = configuration.GetConnectionString("Redis");

        if (string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddSingleton<ICacheService, NoOpCacheService>();

            return;
        }

        try
        {
            var options = ConfigurationOptions.Parse(redisConnection);

            // Redis kapalıysa uygulamanın açılışta takılmaması için: bağlantı arka planda
            // kurulmaya çalışılır, ilk istekler önbelleksiz devam eder
            options.AbortOnConnectFail = false;
            options.ConnectTimeout = 3000;
            options.ConnectRetry = 3;

            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(options));
            services.AddSingleton<ICacheService, RedisCacheService>();

            // Sunucular arası ortak hız sınırı sayacı
            services.AddSingleton<RedisRateLimiter>();
        }
        catch (Exception)
        {
            // Bağlantı dizesi bozuksa önbelleksiz devam edilir; ürünün açılmaması
            // bir hız iyileştirmesinden daha kötü olurdu
            services.AddSingleton<ICacheService, NoOpCacheService>();
        }
    }

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
                // Karmaşıklık kuralları yerine uzunluk: NIST SP 800-63B'nin önerdiği yaklaşım.
                // "Büyük harf + rakam + özel karakter" zorunluluğu kullanıcıyı tahmin edilebilir
                // kalıplara itiyor (Sifre1! gibi) ve gerçek güvenliğe katkısı tartışmalı.
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireDigit = false;

                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = false;

                // Şifre deneme saldırılarına karşı hesap kilidi
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.MaxFailedAccessAttempts = 10;
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
        services.AddScoped<IAccountService, AccountService>();

        // Fotoğraf katkısı, coin ve premium.
        // Depolama tekil: yalnızca kök klasörü ve adresi tutuyor, durumu yok.
        services.AddSingleton<IPhotoStorage, LocalPhotoStorage>();
        services.AddScoped<IRewardService, RewardService>();

        // Kullanıcıların önerdiği yerler; aynı moderasyon kuyruğunun ikinci ayağı.
        services.AddScoped<IPlaceSuggestionService, PlaceSuggestionService>();

        services.Configure<OsrmOptions>(configuration.GetSection(OsrmOptions.SectionName));

        // Rota motoru istemcisi havuzdan yönetilir; her istekte yeni bağlantı açmak
        // soket tükenmesine yol açar
        services.AddHttpClient<IRoutingClient, OsrmClient>((provider, client) =>
        {
            var osrm = configuration.GetSection(OsrmOptions.SectionName).Get<OsrmOptions>() ?? new OsrmOptions();

            client.Timeout = TimeSpan.FromSeconds(osrm.TimeoutSeconds);
        });

        services.AddJwtAuthentication(configuration, environmentName);

        AddCaching(services, configuration);

        return services;
    }
}
