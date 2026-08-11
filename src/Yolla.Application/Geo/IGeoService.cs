namespace Yolla.Application.Geo;

/// <summary>Ülke ve şehir bilgilerini sunar.</summary>
public interface IGeoService
{
    /// <summary>Veri toplanmış ülkeleri döndürür.</summary>
    Task<IReadOnlyList<CountryDto>> GetCountriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Bir ülkenin şehirlerini döndürür; arama metni verilirse ada göre süzer.
    /// </summary>
    /// <param name="countryIso2">Ülke kodu, örn. TR.</param>
    /// <param name="search">Arama metni. Türkçe karakter farkı gözetilmez.</param>
    /// <param name="onlyWithContent">Yalnızca gösterilebilir içeriği olan şehirler.</param>
    Task<IReadOnlyList<CityDto>> GetCitiesAsync(
        string countryIso2,
        string? search = null,
        bool onlyWithContent = false,
        CancellationToken cancellationToken = default);

    /// <summary>Şehri kimliğine göre getirir.</summary>
    Task<CityDto> GetCityAsync(int cityId, CancellationToken cancellationToken = default);

    /// <summary>Şehri web adresindeki kısa adına göre getirir.</summary>
    Task<CityDto> GetCityBySlugAsync(string slug, CancellationToken cancellationToken = default);
}
