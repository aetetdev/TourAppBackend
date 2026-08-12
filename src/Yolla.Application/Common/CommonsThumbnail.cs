namespace Yolla.Application.Common;

/// <summary>
/// Wikimedia Commons görsellerinin küçültülmüş sürümlerinin adresini üretir.
/// </summary>
/// <remarks>
/// Veritabanında görselin <b>orijinali</b> duruyor; ortalama 1 MB civarı. Kart destesi
/// birkaç kartı önden yüklediği için bu haliyle mobil veride kullanılamaz.
///
/// Commons, adrese <c>thumb/</c> ekleyip sonuna <c>&lt;genişlik&gt;px-&lt;ad&gt;</c>
/// getirerek sunucu tarafında küçültülmüş sürüm veriyor:
///
/// <code>
/// .../commons/f/f0/Ad.jpg
/// .../commons/thumb/f/f0/Ad.jpg/500px-Ad.jpg
/// </code>
///
/// Dönüşüm belirlenimci olduğu için istemcide de yapılabilirdi, ancak izin verilen
/// genişlikler listesi Wikimedia'nın kararı ve zamanla değişebiliyor. Tek yerde tutmak
/// için sunucuda üretiliyor: mobil ve web istemcileri aynı mantığı ayrı ayrı taşımaz.
/// </remarks>
public static class CommonsThumbnail
{
    /// <summary>Liste ve kart görselleri için varsayılan genişlik.</summary>
    /// <remarks>Ölçüm: 652 KB orijinal → 131 KB.</remarks>
    public const int ThumbWidth = 500;

    /// <summary>Tam ekran kart ve detay sayfası başlık görseli için genişlik.</summary>
    /// <remarks>Ölçüm: 652 KB orijinal → 385 KB. Yüksek yoğunluklu ekranlar için.</remarks>
    public const int LargeWidth = 960;

    private const string CommonsHost = "upload.wikimedia.org";

    /// <summary>
    /// Wikimedia'nın kabul ettiği <b>standart</b> genişlikler.
    /// </summary>
    /// <remarks>
    /// Liste keyfi değil: Wikimedia doğrudan bağlantıda standart dışı genişlikleri
    /// <c>400 Bad Request</c> ile reddediyor ("Use thumbnail sizes listed on...").
    /// Kaynak: <c>https://www.mediawiki.org/wiki/Common_thumbnail_sizes</c>
    ///
    /// Yuvarlamanın ikinci faydası önbellek: farklı ekran genişliğindeki cihazlar
    /// aynı dosyayı paylaşır.
    /// </remarks>
    private static readonly int[] Buckets = [20, 40, 60, 120, 250, 330, 500, 960, 1280, 1920, 3840];

    /// <summary>Liste/kart genişliğinde küçük görsel adresi.</summary>
    public static string? Thumb(string? originalUrl) => For(originalUrl, ThumbWidth);

    /// <summary>Tam ekran genişliğinde görsel adresi.</summary>
    public static string? Large(string? originalUrl) => For(originalUrl, LargeWidth);

    /// <summary>
    /// <paramref name="originalUrl"/> için verilen genişlikte küçük görsel adresi üretir.
    /// </summary>
    /// <remarks>
    /// Adres Commons deseniyle uyuşmuyorsa (başka bir kaynak, zaten küçültülmüş bir adres
    /// ya da SVG) girdi olduğu gibi döner — hiçbir durumda görsel kaybolmaz.
    /// </remarks>
    public static string? For(string? originalUrl, int width)
    {
        if (string.IsNullOrWhiteSpace(originalUrl))
        {
            return originalUrl;
        }

        var schemeEnd = originalUrl.IndexOf("://", StringComparison.Ordinal);

        if (schemeEnd < 0)
        {
            return originalUrl;
        }

        var pathStart = originalUrl.IndexOf('/', schemeEnd + 3);

        if (pathStart < 0)
        {
            return originalUrl;
        }

        var host = originalUrl[(schemeEnd + 3)..pathStart];

        if (!host.Equals(CommonsHost, StringComparison.OrdinalIgnoreCase))
        {
            return originalUrl;
        }

        var path = originalUrl[pathStart..];

        // Sorgu ya da çapa varsa dosya adı belirsizleşir; dokunmamak güvenli
        if (path.Contains('?') || path.Contains('#'))
        {
            return originalUrl;
        }

        // Yol ham haliyle işlenir: yüzde kodlaması çözülüp yeniden kurulursa adres
        // sessizce değişebilir (örneğin virgül tekrar kodlanmaz). Dosya adına hiç
        // dokunmamak tek güvenli yol.
        var segments = path.Split('/');

        // Beklenen: ['', 'wikipedia', '<proje>', '<a>', '<ab>', '<dosya>']
        // Zaten küçültülmüş adresler ('.../thumb/a/ab/Ad.jpg/500px-Ad.jpg') sekiz
        // parçalıdır ve bu kontrolde elenir.
        if (segments.Length != 6 || segments[1] != "wikipedia")
        {
            return originalUrl;
        }

        var fileName = segments[5];

        if (fileName.Length == 0)
        {
            return originalUrl;
        }

        // SVG'nin küçültülmüşü PNG olarak döner, uzantı değişir. Kapsam dışı bırakmak
        // yanlış adres üretmekten iyi.
        if (fileName.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
        {
            return originalUrl;
        }

        var scheme = originalUrl[..schemeEnd];
        var target = BucketFor(width);

        return $"{scheme}://{host}/wikipedia/{segments[2]}/thumb/" +
               $"{segments[3]}/{segments[4]}/{fileName}/{target}px-{fileName}";
    }

    /// <summary>İstenen genişliği izin verilen en yakın üst kovaya yuvarlar.</summary>
    internal static int BucketFor(int width)
    {
        foreach (var bucket in Buckets)
        {
            if (width <= bucket)
            {
                return bucket;
            }
        }

        return Buckets[^1];
    }
}
