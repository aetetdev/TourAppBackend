using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Yolla.Infrastructure.Identity;
using Yolla.Infrastructure.Persistence;

namespace Yolla.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres tanımlı değil.");

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

        var redisConnection = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(redisConnection));
        }

        return services;
    }
}
