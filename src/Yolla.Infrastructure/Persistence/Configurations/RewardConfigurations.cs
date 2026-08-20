using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yolla.Domain.Entities;
using Yolla.Domain.Enums;

namespace Yolla.Infrastructure.Persistence.Configurations;

public class PhotoSubmissionConfiguration : IEntityTypeConfiguration<PhotoSubmission>
{
    public void Configure(EntityTypeBuilder<PhotoSubmission> builder)
    {
        builder.Property(x => x.StoragePath).HasMaxLength(400).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(80).IsRequired();
        builder.Property(x => x.RejectionReason).HasMaxLength(500);

        // Moderasyon kuyruğu her açılışta "bekleyenler, eskiden yeniye" diye
        // okunuyor; sıralamayı da kapsayan bileşik indeks tarama yapmasını
        // engelliyor.
        builder.HasIndex(x => new { x.Status, x.CreatedAt });

        // "Bu kullanıcının gönderileri" ve kota kontrolü.
        builder.HasIndex(x => new { x.UserId, x.Status });

        builder.HasIndex(x => x.PlaceId);

        builder.HasOne(x => x.Place)
            .WithMany()
            .HasForeignKey(x => x.PlaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CoinEntryConfiguration : IEntityTypeConfiguration<CoinEntry>
{
    public void Configure(EntityTypeBuilder<CoinEntry> builder)
    {
        builder.Property(x => x.Note).HasMaxLength(300);

        // Bakiye defterin toplamı olarak hesaplanıyor; kullanıcı bazlı toplama
        // ve geçmiş listesi bu indeksi kullanıyor.
        builder.HasIndex(x => new { x.UserId, x.CreatedAt });

        // Bir gönderi en fazla bir kez ödüllendirilebilir. Eşzamanlı iki onay
        // isteği gelirse ikincisi burada patlıyor — kontrolü yalnızca uygulama
        // katmanına bırakmak yarış durumuna açık kalırdı.
        builder.HasIndex(x => x.PhotoSubmissionId)
            .IsUnique()
            .HasFilter("photo_submission_id IS NOT NULL");

        // Aynısı yer önerileri için: bir öneri en fazla bir kez ödüllendirilir.
        builder.HasIndex(x => x.PlaceSuggestionId)
            .IsUnique()
            .HasFilter("place_suggestion_id IS NOT NULL");
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.Property(x => x.Title).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Body).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Language).HasMaxLength(5).IsRequired();

        // Liste "benim bildirimlerim, yeniden eskiye" diye okunuyor.
        builder.HasIndex(x => new { x.UserId, x.CreatedAt });

        // Okunmamış sayısı her açılışta sorulabiliyor; kısmi indeks tabloyu
        // taramasını engelliyor.
        builder.HasIndex(x => x.UserId)
            .HasFilter("read_at IS NULL");
    }
}

public class PlaceSuggestionConfiguration : IEntityTypeConfiguration<PlaceSuggestion>
{
    public void Configure(EntityTypeBuilder<PlaceSuggestion> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(250).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.Address).HasMaxLength(400);
        builder.Property(x => x.RejectionReason).HasMaxLength(500);

        builder.Property(x => x.Location)
            .HasColumnType("geography (Point, 4326)")
            .IsRequired();

        // Tekrar önerisi kontrolü ve moderatöre gösterilen yakın kayıtlar
        // mesafe sorgusu yapıyor.
        builder.HasIndex(x => x.Location).HasMethod("gist");

        // Moderasyon kuyruğu "bekleyenler, eskiden yeniye" diye okunuyor.
        builder.HasIndex(x => new { x.Status, x.CreatedAt });

        // "Benim önerilerim" ve kota kontrolü.
        builder.HasIndex(x => new { x.UserId, x.Status });

        builder.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.City)
            .WithMany()
            .HasForeignKey(x => x.CityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.District)
            .WithMany()
            .HasForeignKey(x => x.DistrictId)
            .OnDelete(DeleteBehavior.SetNull);

        // Öneri kataloğa girdikten sonra yer silinirse öneri kaydı kalsın;
        // denetim izi olarak duruyor.
        builder.HasOne(x => x.Place)
            .WithMany()
            .HasForeignKey(x => x.PlaceId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class PremiumGrantConfiguration : IEntityTypeConfiguration<PremiumGrant>
{
    public void Configure(EntityTypeBuilder<PremiumGrant> builder)
    {
        builder.Property(x => x.Note).HasMaxLength(300);

        // "Bu kullanıcının şu an geçerli hakkı var mı" sorgusu her istekte
        // çalışabiliyor.
        builder.HasIndex(x => new { x.UserId, x.ExpiresAt });
    }
}
