using Yolla.Application.Common;

namespace Yolla.Application.Discovery;

/// <summary>
/// Kart destesindeki tek bir yer.
/// </summary>
/// <remarks>
/// Kartta gösterilecek her şeyi taşır; istemci ek istek yapmadan kartı çizebilmelidir.
/// Fotoğrafı veya atıf bilgisi olmayan yerler kart olarak dönmez.
/// </remarks>
public sealed record PlaceCardDto
{
    /// <summary>Yerin kimliği.</summary>
    /// <example>12345</example>
    public required int Id { get; init; }

    /// <summary>Yerin adı.</summary>
    /// <example>Uçhisar Kalesi</example>
    public required string Name { get; init; }

    /// <summary>Web adreslerinde kullanılan kısa ad.</summary>
    /// <example>uchisar-kalesi</example>
    public required string Slug { get; init; }

    /// <summary>Kategori anahtarı.</summary>
    /// <example>castle</example>
    public required string CategoryKey { get; init; }

    /// <summary>Kategorinin görünen adı.</summary>
    /// <example>Kale</example>
    public required string CategoryName { get; init; }

    /// <summary>Kategori ikonu.</summary>
    /// <example>castle</example>
    public string? CategoryIcon { get; init; }

    /// <summary>Kart görselinin orijinali. Büyüktür (ortalama ~1 MB).</summary>
    /// <remarks>
    /// Listede göstermek için <see cref="PhotoThumbUrl"/>, tam ekran kart için
    /// <see cref="PhotoLargeUrl"/> tercih edilmeli.
    /// </remarks>
    public required string PhotoUrl { get; init; }

    /// <summary>Liste ve önizleme için küçültülmüş görsel (500 piksel genişlik).</summary>
    /// <remarks>
    /// Adres Wikimedia deseniyle uyuşmuyorsa orijinalin aynısı döner; alan hiçbir
    /// durumda boş kalmaz.
    /// </remarks>
    public string PhotoThumbUrl => CommonsThumbnail.Thumb(PhotoUrl)!;

    /// <summary>Tam ekran kart için küçültülmüş görsel (960 piksel genişlik).</summary>
    public string PhotoLargeUrl => CommonsThumbnail.Large(PhotoUrl)!;

    /// <summary>
    /// Görselin altında gösterilmesi zorunlu atıf satırı.
    /// </summary>
    /// <example>Fotoğraf: SerifeOzoglu (CC BY-SA 4.0)</example>
    public required string PhotoAttribution { get; init; }

    /// <summary>Görselin kaynak sayfası; atıf bağlantısı olarak kullanılır.</summary>
    public string? PhotoSource { get; init; }

    /// <summary>Kartta gösterilen kısa açıklama.</summary>
    public string? Description { get; init; }

    /// <summary>Bulunduğu şehrin kimliği.</summary>
    /// <remarks>
    /// Beğenilen yerlerden doğrudan şehir içi plan kurulabilmesi için gerekli;
    /// istemci şehir bilgisini ekranlar arasında taşımak zorunda kalmasın.
    /// </remarks>
    /// <example>106</example>
    public required int CityId { get; init; }

    /// <summary>Bulunduğu şehir.</summary>
    /// <example>Nevşehir</example>
    public required string CityName { get; init; }

    /// <summary>Bulunduğu ilçe.</summary>
    /// <example>Uçhisar</example>
    public string? DistrictName { get; init; }

    public required double Latitude { get; init; }

    public required double Longitude { get; init; }

    /// <summary>Ortalama gezme süresi (dakika). Gün planı bölmede kullanılır.</summary>
    public short? AverageVisitMinutes { get; init; }

    /// <summary>0-100 arası içerik kalitesi. Sıralama şeffaflığı için döndürülür.</summary>
    public required short QualityScore { get; init; }

    /// <summary>
    /// Rota modunda, başlangıçtan varışa doğru yol üzerindeki konum (0-1 arası).
    /// Şehir içi modda null.
    /// </summary>
    public double? RouteProgress { get; init; }

    /// <summary>
    /// Rota modunda, bu yere uğramak için ana yoldan sapma mesafesi (metre).
    /// Şehir içi modda null.
    /// </summary>
    public double? DetourMeters { get; init; }
}
