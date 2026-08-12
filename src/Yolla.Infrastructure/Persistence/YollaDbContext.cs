using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Yolla.Domain.Common;
using Yolla.Domain.Entities;
using Yolla.Infrastructure.Identity;
using Yolla.Infrastructure.Persistence.Seed;

namespace Yolla.Infrastructure.Persistence;

public class YollaDbContext : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>
{
    public YollaDbContext(DbContextOptions<YollaDbContext> options) : base(options)
    {
    }

    public DbSet<Country> Countries => Set<Country>();
    public DbSet<City> Cities => Set<City>();
    public DbSet<District> Districts => Set<District>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Place> Places => Set<Place>();
    public DbSet<PlaceTranslation> PlaceTranslations => Set<PlaceTranslation>();
    public DbSet<PlaceContribution> PlaceContributions => Set<PlaceContribution>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<TripPlace> TripPlaces => Set<TripPlace>();
    public DbSet<Swipe> Swipes => Set<Swipe>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Coğrafi sorguların tamamı PostGIS üzerinde çalışıyor
        builder.HasPostgresExtension("postgis");

        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(YollaDbContext).Assembly);

        CountrySeed.Apply(builder);
        CategorySeed.Apply(builder);

        // Identity tablolarını da snake_case'e çevirmek yerine kendi önekimizle ayırıyoruz
        builder.Entity<ApplicationUser>().ToTable("users");
        builder.Entity<IdentityRole<int>>().ToTable("roles");
        builder.Entity<IdentityUserRole<int>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<int>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<int>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<int>>().ToTable("user_tokens");
        builder.Entity<IdentityRoleClaim<int>>().ToTable("role_claims");
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // UpdatedAt'i elle set etmeyi unutmamak için tek yerden yönetiyoruz
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
