using NetTopologySuite.Geometries;
using Yolla.Domain.Common;
using Yolla.Domain.Enums;

namespace Yolla.Domain.Entities;

/// <summary>
/// Kullanıcının önerdiği, henüz kataloğa girmemiş yer.
/// </summary>
/// <remarks>
/// Veri OpenStreetMap'ten geliyor ve orada olmayan yer bizde de yok: köy
/// çeşmesi, yeni açılmış müze, yalnızca yerlinin bildiği manzara noktası.
/// Bu boşluğu kullanıcı dolduruyor.
///
/// Öneri doğrudan kataloğa girmiyor. Onaylanana kadar burada bekliyor; onay
/// verilince gerçek bir <see cref="Place"/> yaratılıyor ve <see cref="PlaceId"/>
/// ile buraya bağlanıyor. Reddedilen öneri de silinmiyor — aynı yerin tekrar
/// tekrar önerilmesini görebilmek ve kullanıcıya sebebi gösterebilmek için
/// duruyor.
/// </remarks>
public class PlaceSuggestion : BaseEntity
{
    /// <summary>Öneren kullanıcı. Hesap zorunlu olduğu için her zaman dolu.</summary>
    public int UserId { get; set; }

    /// <summary>Hangi cihazdan geldi — kötüye kullanım incelemesi için.</summary>
    public int? DeviceId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Önerilen kategori; kataloğun kategori anahtarlarından biri.</summary>
    public int CategoryId { get; set; }

    public Category Category { get; set; } = null!;

    public Point Location { get; set; } = null!;

    /// <summary>
    /// Konumun düştüğü şehir; gönderi sırasında sınırlardan bulunuyor.
    /// </summary>
    /// <remarks>
    /// Gönderide hesaplanıyor, onayda değil: koordinat Türkiye sınırları
    /// dışındaysa kullanıcı bunu göndermeden öğrenmeli, moderatörün önüne
    /// düşmemeli.
    /// </remarks>
    public int CityId { get; set; }

    public City City { get; set; } = null!;

    public int? DistrictId { get; set; }

    public District? District { get; set; }

    /// <summary>Kullanıcının yazdığı tanıtım; isteğe bağlı.</summary>
    public string? Description { get; set; }

    /// <summary>Adres ya da tarif; isteğe bağlı.</summary>
    public string? Address { get; set; }

    public PlaceSuggestionStatus Status { get; set; } = PlaceSuggestionStatus.Pending;

    public DateTimeOffset? ReviewedAt { get; set; }

    /// <summary>İnceleyen moderatörün kimliği.</summary>
    public int? ReviewedByUserId { get; set; }

    /// <summary>Reddedildiyse sebebi. Kullanıcıya gösteriliyor.</summary>
    public string? RejectionReason { get; set; }

    /// <summary>Onaylandıysa yaratılan yer.</summary>
    public int? PlaceId { get; set; }

    public Place? Place { get; set; }

    /// <summary>
    /// Onay karşılığı verilen coin.
    /// </summary>
    /// <remarks>
    /// Coin defterinde de kaydı var; burada tutulması öneriye bakınca ne
    /// kazandırdığını görmek için.
    /// </remarks>
    public int? CoinsAwarded { get; set; }
}
