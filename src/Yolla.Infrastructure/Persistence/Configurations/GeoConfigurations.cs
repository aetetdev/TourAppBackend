using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yolla.Domain.Entities;

namespace Yolla.Infrastructure.Persistence.Configurations;

public class CountryConfiguration : IEntityTypeConfiguration<Country>
{
    public void Configure(EntityTypeBuilder<Country> builder)
    {
        builder.Property(x => x.Iso2).HasMaxLength(2).IsRequired();
        builder.Property(x => x.NameTr).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NameEn).HasMaxLength(100).IsRequired();

        builder.HasIndex(x => x.Iso2).IsUnique();
    }
}

public class CityConfiguration : IEntityTypeConfiguration<City>
{
    public void Configure(EntityTypeBuilder<City> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.NameNormalized).HasMaxLength(120).IsRequired();
        builder.Property(x => x.NameEn).HasMaxLength(120);
        builder.Property(x => x.Slug).HasMaxLength(140).IsRequired();

        // Nokta verisi geography: mesafeler doğrudan metre cinsinden çıkar
        builder.Property(x => x.Center)
            .HasColumnType("geography (Point, 4326)")
            .IsRequired();

        // Sınır poligonu geometry: ST_Contains gibi topoloji fonksiyonları bunu ister
        builder.Property(x => x.Boundary)
            .HasColumnType("geometry (Geometry, 4326)");

        builder.HasIndex(x => x.Slug).IsUnique();
        builder.HasIndex(x => x.NameNormalized);
        builder.HasIndex(x => x.Center).HasMethod("gist");

        // Veri toplayıcının idempotent çalışması bu kimliğe dayanıyor
        builder.HasIndex(x => x.OsmRelationId).IsUnique().HasFilter("osm_relation_id IS NOT NULL");

        // Şehir içi mod, sınır poligonu içindeki yerleri sorgular
        builder.HasIndex(x => x.Boundary).HasMethod("gist");

        builder.HasOne(x => x.Country)
            .WithMany(x => x.Cities)
            .HasForeignKey(x => x.CountryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class DistrictConfiguration : IEntityTypeConfiguration<District>
{
    public void Configure(EntityTypeBuilder<District> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.NameNormalized).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Slug).HasMaxLength(140).IsRequired();

        builder.Property(x => x.Boundary)
            .HasColumnType("geometry (Geometry, 4326)");

        builder.HasIndex(x => new { x.CityId, x.NameNormalized });
        builder.HasIndex(x => x.OsmRelationId).IsUnique().HasFilter("osm_relation_id IS NOT NULL");
        builder.HasIndex(x => x.Boundary).HasMethod("gist");

        builder.HasOne(x => x.City)
            .WithMany(x => x.Districts)
            .HasForeignKey(x => x.CityId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
