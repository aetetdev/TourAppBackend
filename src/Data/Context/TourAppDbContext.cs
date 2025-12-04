using Microsoft.EntityFrameworkCore;
using TourAppBackend.src.Domain.Entities;

namespace TourAppBackend.src.Data.Context
{
    // EF Core DbContext - veritabanı ile iletişimi sağlar
    public class TourAppDbContext : DbContext
    {
        public TourAppDbContext(DbContextOptions<TourAppDbContext> options) : base(options)
        {
        }

        // TouristPlaces tablosunu temsil eden DbSet
        public DbSet<TouristPlace> TouristPlaces { get; set; } = null!;

        // Gerekirse model yapılandırmaları burada yapılır
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Örnek: CityId için index ekleyebilirsiniz (migration ile uygulanır)
            modelBuilder.Entity<TouristPlace>()
                .HasIndex(tp => tp.CityId);
        }
    }
}
