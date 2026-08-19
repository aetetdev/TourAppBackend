namespace Yolla.Application.Places;

/// <summary>
/// Haritada gösterilecek yer işareti.
/// </summary>
/// <remarks>
/// Kart gövdesinin (<see cref="PlaceCardDto"/>) çok küçültülmüş hali. Bir ekran
/// dolusu harita yüzlerce işaret taşıyabildiği için burada yalnızca çizmek ve
/// dokunulduğunda tanımak için gerekenler var; açıklama, fotoğraf adresi ve
/// atıf satırı yok. Kullanıcı işarete dokunduğunda yer detayı ayrıca çekiliyor.
/// </remarks>
public sealed class PlacePinDto
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public double Latitude { get; init; }

    public double Longitude { get; init; }

    /// <summary>İşaret simgesini seçmek için kullanılır.</summary>
    public string CategoryKey { get; init; } = string.Empty;

    public string? CategoryIcon { get; init; }

    /// <summary>
    /// Yerin yayınlanabilir fotoğrafı var mı?
    /// </summary>
    /// <remarks>
    /// Fotoğrafsız yerler haritada bilerek gösteriliyor: kullanıcıdan fotoğraf
    /// istemenin doğal yeri burası. İstemci bu bayrağa bakıp işareti farklı
    /// çiziyor ve detayında "fotoğraf ekle" çağrısını öne çıkarıyor.
    /// </remarks>
    public bool HasPhoto { get; init; }

    /// <summary>0-100 kalite puanı. İstemci yakınlaştırma seviyesine göre
    /// eleme yapmak isterse kullanır.</summary>
    public int QualityScore { get; init; }
}
