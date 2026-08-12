namespace Yolla.Application.Common;

/// <summary>
/// Sonuç önbelleği.
/// </summary>
/// <remarks>
/// Önbellek <b>isteğe bağlıdır</b>: Redis yapılandırılmamışsa veya erişilemiyorsa
/// uygulama çalışmaya devam eder, yalnızca her istek veritabanına gider. Önbellek
/// hatası hiçbir zaman isteği düşürmez - bir hız iyileştirmesi uğruna ürünü
/// durdurmak mantıksız olurdu.
/// </remarks>
public interface ICacheService
{
    /// <summary>
    /// Önbellekte varsa değeri döndürür; yoksa üretir, saklar ve döndürür.
    /// </summary>
    /// <param name="key">Önbellek anahtarı.</param>
    /// <param name="duration">Saklama süresi.</param>
    /// <param name="factory">Değer yoksa çalıştırılacak üretici.</param>
    Task<T> GetOrCreateAsync<T>(
        string key,
        TimeSpan duration,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken = default);

    /// <summary>Bir anahtarı siler.</summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Verilen önekle başlayan tüm anahtarları siler.</summary>
    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);
}

/// <summary>Önbellek anahtarlarının tek üretim yeri.</summary>
/// <remarks>
/// Anahtarlar dağınık yerlerde elle kurulursa, geçersiz kılma zamanı geldiğinde hangi
/// anahtarların silineceği bilinemez hale gelir.
/// </remarks>
public static class CacheKeys
{
    public const string CountriesKey = "geo:countries";

    public static string Cities(string countryIso2, string? search, bool onlyWithContent) =>
        $"geo:cities:{countryIso2}:{search ?? "-"}:{onlyWithContent}";

    public static string City(int cityId) => $"geo:city:{cityId}";

    public static string CityBySlug(string slug) => $"geo:city-slug:{slug}";

    public static string PlaceDetail(int placeId, string language) => $"place:{placeId}:{language}";

    public static string PlaceBySlug(string citySlug, string placeSlug, string language) =>
        $"place-slug:{citySlug}:{placeSlug}:{language}";

    /// <summary>Bir yerin içeriği değiştiğinde silinmesi gereken anahtarların öneki.</summary>
    public const string PlacePrefix = "place";

    /// <summary>Coğrafi verilerin öneki; yeni yer eklendiğinde sayılar değişir.</summary>
    public const string GeoPrefix = "geo";
}
