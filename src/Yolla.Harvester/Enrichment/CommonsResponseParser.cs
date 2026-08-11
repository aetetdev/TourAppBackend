using System.Net;
using System.Text;
using System.Text.Json;

namespace Yolla.Harvester.Enrichment;

/// <summary>Commons'tan çekilen fotoğraf ve zorunlu atıf bilgileri.</summary>
public sealed record CommonsPhoto
{
    /// <summary>Dosya adı, "File:" ön eki dahil.</summary>
    public required string FileTitle { get; init; }

    public required string Url { get; init; }

    /// <summary>Fotoğrafçı adı. CC BY-SA lisansları bunun gösterilmesini şart koşar.</summary>
    public string? Author { get; init; }

    /// <summary>Lisans adı, örn. "CC BY-SA 4.0".</summary>
    public string? License { get; init; }

    /// <summary>Commons'taki dosya sayfası - atıf linki olarak gösterilir.</summary>
    public string? DescriptionUrl { get; init; }
}

/// <summary>
/// Commons <c>imageinfo</c> yanıtını çözer.
/// </summary>
/// <remarks>
/// Atıf bilgisi olmadan görsel kullanılamaz: Commons görsellerinin çoğu CC BY-SA lisanslı
/// ve fotoğrafçı adının gösterilmesi hukuki zorunluluk. Yazar alanı HTML içerdiği için
/// (bağlantılar, biçimlendirme) düz metne çevrilir.
/// </remarks>
public static class CommonsResponseParser
{
    private const int MaxAuthorLength = 250;

    public static IReadOnlyDictionary<string, CommonsPhoto> Parse(string? json)
    {
        var result = new Dictionary<string, CommonsPhoto>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(json))
        {
            return result;
        }

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return result;
        }

        using (document)
        {
            var pages = document.RootElement
                .GetPropertyOrNull("query")?
                .GetPropertyOrNull("pages");

            if (pages is not { ValueKind: JsonValueKind.Object })
            {
                return result;
            }

            foreach (var page in pages.Value.EnumerateObject())
            {
                var photo = ParsePage(page.Value);

                if (photo is not null)
                {
                    result[photo.FileTitle] = photo;
                }
            }
        }

        return result;
    }

    private static CommonsPhoto? ParsePage(JsonElement page)
    {
        if (page.TryGetProperty("missing", out _))
        {
            return null;
        }

        var title = page.GetPropertyOrNull("title")?.GetString();

        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var imageInfo = page.GetPropertyOrNull("imageinfo");

        if (imageInfo is not { ValueKind: JsonValueKind.Array } || imageInfo.Value.GetArrayLength() == 0)
        {
            return null;
        }

        var info = imageInfo.Value[0];
        var url = info.GetPropertyOrNull("url")?.GetString();

        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var metadata = info.GetPropertyOrNull("extmetadata");
        var license = ReadMetadataValue(metadata, "LicenseShortName");

        // Commons'ta olmaması gereken ama nadiren görülen kısıtlı lisanslar
        if (license is not null && license.Contains("fair use", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new CommonsPhoto
        {
            FileTitle = title,
            Url = url,
            Author = Truncate(StripHtml(ReadMetadataValue(metadata, "Artist")), MaxAuthorLength),
            License = license,
            DescriptionUrl = info.GetPropertyOrNull("descriptionurl")?.GetString()
        };
    }

    private static string? ReadMetadataValue(JsonElement? metadata, string key)
    {
        var value = metadata?.GetPropertyOrNull(key)?.GetPropertyOrNull("value");

        return value is { ValueKind: JsonValueKind.String } ? value.Value.GetString() : null;
    }

    /// <summary>
    /// Yazar alanındaki HTML'i düz metne çevirir: <c>&lt;a href="..."&gt;Ad&lt;/a&gt;</c> -> <c>Ad</c>
    /// </summary>
    internal static string? StripHtml(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var builder = new StringBuilder(value.Length);
        var insideTag = false;

        foreach (var ch in value)
        {
            switch (ch)
            {
                case '<':
                    insideTag = true;
                    break;
                case '>':
                    insideTag = false;
                    // Etiket sınırı kelimeleri birbirine yapıştırmasın
                    builder.Append(' ');
                    break;
                default:
                    if (!insideTag)
                    {
                        builder.Append(ch);
                    }

                    break;
            }
        }

        var text = WebUtility.HtmlDecode(builder.ToString());
        var collapsed = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        return collapsed.Length == 0 ? null : collapsed;
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (value is null)
        {
            return null;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
