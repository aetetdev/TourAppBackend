using Yolla.Domain.Common;
using Yolla.Domain.Enums;

namespace Yolla.Domain.Entities;

/// <summary>
/// Kullanıcının bir yer için gönderdiği fotoğraf.
/// </summary>
/// <remarks>
/// Otomatik kaynaklar tükendi: feed'e girmeye layık 4.003 kayıt fotoğrafsız
/// kaldı. Bu boşluğu kullanıcılar dolduruyor.
///
/// Gönderi doğrudan yayına girmiyor. Onaylanana kadar burada bekliyor; onay
/// verilince <see cref="PlaceContribution"/> olarak yayına alınıyor. İki tablo
/// ayrı tutuluyor çünkü sorumlulukları farklı: burası kuyruk ve denetim
/// kaydı, orası yayındaki içerik. Reddedilen gönderi de siliniyor değil —
/// aynı kullanıcı aynı fotoğrafı tekrar tekrar göndermesin diye duruyor.
/// </remarks>
public class PhotoSubmission : BaseEntity
{
    public int PlaceId { get; set; }

    public Place Place { get; set; } = null!;

    /// <summary>Gönderen kullanıcı. Hesap zorunlu olduğu için her zaman dolu.</summary>
    public int UserId { get; set; }

    /// <summary>Hangi cihazdan gönderildi — kötüye kullanım incelemesi için.</summary>
    public int? DeviceId { get; set; }

    /// <summary>Dosyanın depodaki göreli yolu.</summary>
    public string StoragePath { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public PhotoSubmissionStatus Status { get; set; } = PhotoSubmissionStatus.Pending;

    public DateTimeOffset? ReviewedAt { get; set; }

    /// <summary>İnceleyen yöneticinin kimliği.</summary>
    public int? ReviewedByUserId { get; set; }

    /// <summary>Reddedildiyse sebebi. Kullanıcıya gösteriliyor.</summary>
    public string? RejectionReason { get; set; }

    /// <summary>
    /// Onay karşılığı verilen coin.
    /// </summary>
    /// <remarks>
    /// Coin defterinde de kaydı var; burada tutulması gönderiye bakınca ne
    /// kazandırdığını görmek için. Ödeme iki kez yapılmasın diye onaydan önce
    /// bu alanın boş olduğu kontrol ediliyor.
    /// </remarks>
    public int? CoinsAwarded { get; set; }

    /// <summary>Onaylanınca üretilen yayın kaydı.</summary>
    public int? PlaceContributionId { get; set; }
}
