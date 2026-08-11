using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yolla.Domain.Entities;

namespace Yolla.Infrastructure.Persistence.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.Property(x => x.Key).HasMaxLength(60).IsRequired();
        builder.Property(x => x.NameTr).HasMaxLength(80).IsRequired();
        builder.Property(x => x.NameEn).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Icon).HasMaxLength(60);

        builder.HasIndex(x => x.Key).IsUnique();

        builder.HasOne(x => x.Parent)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PlaceConfiguration : IEntityTypeConfiguration<Place>
{
    public void Configure(EntityTypeBuilder<Place> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(250).IsRequired();
        builder.Property(x => x.NameEn).HasMaxLength(250);
        builder.Property(x => x.Slug).HasMaxLength(280).IsRequired();
        builder.Property(x => x.Address).HasMaxLength(400);
        builder.Property(x => x.Website).HasMaxLength(500);
        builder.Property(x => x.OpeningHours).HasMaxLength(250);

        builder.Property(x => x.PhotoUrl).HasMaxLength(1000);
        builder.Property(x => x.PhotoAuthor).HasMaxLength(250);
        builder.Property(x => x.PhotoLicense).HasMaxLength(120);
        builder.Property(x => x.PhotoSource).HasMaxLength(1000);

        builder.Property(x => x.WikidataId).HasMaxLength(32);
        builder.Property(x => x.WikipediaTitle).HasMaxLength(250);
        builder.Property(x => x.CommonsRef).HasMaxLength(300);

        builder.Property(x => x.Location)
            .HasColumnType("geography (Point, 4326)")
            .IsRequired();

        // Koridor ve yakınlık sorgularının tamamı bu indekse dayanıyor
        builder.HasIndex(x => x.Location).HasMethod("gist");

        // Harvester tekrar çalıştığında kayıtlar bu ikili ile eşleşip güncellenir (upsert)
        builder.HasIndex(x => new { x.OsmType, x.OsmId }).IsUnique();

        // Şehir içi kart destesinin ana sorgusu
        builder.HasIndex(x => new { x.CityId, x.QualityScore })
            .IsDescending(false, true);

        builder.HasIndex(x => x.Slug);

        builder.HasOne(x => x.Country)
            .WithMany()
            .HasForeignKey(x => x.CountryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.City)
            .WithMany(x => x.Places)
            .HasForeignKey(x => x.CityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.District)
            .WithMany(x => x.Places)
            .HasForeignKey(x => x.DistrictId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Category)
            .WithMany(x => x.Places)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PlaceTranslationConfiguration : IEntityTypeConfiguration<PlaceTranslation>
{
    public void Configure(EntityTypeBuilder<PlaceTranslation> builder)
    {
        builder.Property(x => x.Language).HasMaxLength(5).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(250);

        builder.HasIndex(x => new { x.PlaceId, x.Language }).IsUnique();

        builder.HasOne(x => x.Place)
            .WithMany(x => x.Translations)
            .HasForeignKey(x => x.PlaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
