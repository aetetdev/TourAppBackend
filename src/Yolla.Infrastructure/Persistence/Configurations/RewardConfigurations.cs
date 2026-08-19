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
