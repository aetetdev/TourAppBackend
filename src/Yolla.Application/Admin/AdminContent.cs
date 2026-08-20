namespace Yolla.Application.Admin;

/// <summary>
/// İçerik ekibinin doğrudan yaptığı eklemeler.
/// </summary>
/// <remarks>
/// Kullanıcı katkısından farkı moderasyondan geçmemesi: ekip zaten
/// moderasyonun kendisi, kendi eklediğini kendine onaylatması anlamsız.
/// Bu yüzden eklenen fotoğraf ve yer doğrudan yayına giriyor ve coin
/// yazılmıyor.
/// </remarks>
public interface IAdminContentService
{
    /// <summary>
    /// Fotoğrafı olmayan yerler; ekibin iş listesi.
    /// </summary>
    /// <remarks>
    /// Kaliteli olanlar önce: bir fotoğraf eklendiğinde doğrudan kart
    /// destesine giren kayıtlar en çok işe yarayanlar.
    /// </remarks>
    Task<IReadOnlyList<MissingPhotoDto>> GetPlacesMissingPhotoAsync(
        int? cityId = null,
        string? search = null,
        int take = 30,
        CancellationToken cancellationToken = default);

    /// <summary>Bir yere doğrudan fotoğraf ekler; yayına anında girer.</summary>
    Task AddPhotoAsync(
        int placeId,
        AddPhotoRequest request,
        Stream content,
        int editorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Kataloğa doğrudan yeni yer ekler.</summary>
    Task<CreatedPlaceDto> CreatePlaceAsync(
        CreatePlaceRequest request,
        int editorUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>Fotoğraf bekleyen yer.</summary>
public sealed record MissingPhotoDto
{
    public required int PlaceId { get; init; }

    public required string Name { get; init; }

    public required string CityName { get; init; }

    public string? DistrictName { get; init; }

    public required string CategoryName { get; init; }

    public required short QualityScore { get; init; }

    public required bool HasDescription { get; init; }

    public required double Latitude { get; init; }

    public required double Longitude { get; init; }

    /// <summary>Varsa Wikidata kimliği; elle araştırmanın başlangıç noktası.</summary>
    public string? WikidataId { get; init; }
}

/// <summary>Ekibin eklediği fotoğrafın künyesi.</summary>
public sealed record AddPhotoRequest
{
    /// <summary>
    /// Fotoğrafı çeken kişi.
    /// </summary>
    /// <remarks>
    /// Zorunlu: görselin yanında atıf olmadan yayına girmesi hem lisansa
    /// aykırı hem de emeği görünmez kılıyor.
    /// </remarks>
    public required string PhotographerName { get; init; }

    /// <summary>Lisans metni; boş bırakılırsa "Yolla".</summary>
    public string? License { get; init; }

    /// <summary>Görselin kaynağı (adres); varsa.</summary>
    public string? SourceUrl { get; init; }
}

/// <summary>Ekibin doğrudan eklediği yer.</summary>
public sealed record CreatePlaceRequest
{
    public required string Name { get; init; }

    public required string CategoryKey { get; init; }

    public required double Latitude { get; init; }

    public required double Longitude { get; init; }

    public string? Description { get; init; }

    public string? Address { get; init; }
}

public sealed record CreatedPlaceDto
{
    public required int PlaceId { get; init; }

    public required string Name { get; init; }

    public required string CityName { get; init; }

    public required string Slug { get; init; }
}
