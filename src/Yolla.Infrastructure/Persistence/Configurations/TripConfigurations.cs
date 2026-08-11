using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yolla.Domain.Entities;

namespace Yolla.Infrastructure.Persistence.Configurations;

public class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.Property(x => x.Platform).HasMaxLength(20).IsRequired();
        builder.Property(x => x.AppVersion).HasMaxLength(30);
        builder.Property(x => x.Language).HasMaxLength(5).IsRequired();

        builder.HasIndex(x => x.DeviceUuid).IsUnique();
        builder.HasIndex(x => x.UserId);
    }
}

public class TripConfiguration : IEntityTypeConfiguration<Trip>
{
    public void Configure(EntityTypeBuilder<Trip> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.RouteGeometry).HasColumnType("text");

        builder.Property(x => x.StartPoint).HasColumnType("geography (Point, 4326)");
        builder.Property(x => x.EndPoint).HasColumnType("geography (Point, 4326)");

        builder.HasIndex(x => x.DeviceId);
        builder.HasIndex(x => x.UserId);

        builder.HasOne(x => x.Device)
            .WithMany(x => x.Trips)
            .HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.City)
            .WithMany()
            .HasForeignKey(x => x.CityId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class TripPlaceConfiguration : IEntityTypeConfiguration<TripPlace>
{
    public void Configure(EntityTypeBuilder<TripPlace> builder)
    {
        builder.Property(x => x.Note).HasMaxLength(500);

        builder.HasIndex(x => new { x.TripId, x.PlaceId }).IsUnique();

        builder.HasOne(x => x.Trip)
            .WithMany(x => x.Places)
            .HasForeignKey(x => x.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Place)
            .WithMany()
            .HasForeignKey(x => x.PlaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SwipeConfiguration : IEntityTypeConfiguration<Swipe>
{
    public void Configure(EntityTypeBuilder<Swipe> builder)
    {
        // Aynı cihaz aynı yeri iki kez görmesin; yön değişirse kayıt güncellenir
        builder.HasIndex(x => new { x.DeviceId, x.PlaceId }).IsUnique();
        builder.HasIndex(x => x.PlaceId);

        builder.HasOne(x => x.Device)
            .WithMany(x => x.Swipes)
            .HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Place)
            .WithMany()
            .HasForeignKey(x => x.PlaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
