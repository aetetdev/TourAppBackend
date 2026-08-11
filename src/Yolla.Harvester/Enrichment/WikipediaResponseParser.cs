using System.Text.Json;

namespace Yolla.Harvester.Enrichment;

/// <summary>
/// Wikipedia <c>extracts</c> yanıtını çözer: makale başlığı -> giriş paragrafı.
/// </summary>
/// <remarks>
/// Yönlendirmeler (redirect) izlendiği için istenen başlıkla dönen başlık farklı olabilir;
/// eşleştirme yanıttaki "redirects" listesiyle yapılır.
/// </remarks>
public static class WikipediaResponseParser
{
    public static IReadOnlyDictionary<string, string> Parse(string? json, int maxLength = 600)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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
            var query = document.RootElement.GetPropertyOrNull("query");

            if (query is null)
            {
                return result;
            }

            var pages = query.Value.GetPropertyOrNull("pages");

            if (pages is not { ValueKind: JsonValueKind.Object })
            {
                return result;
            }

            foreach (var page in pages.Value.EnumerateObject())
            {
                if (page.Value.TryGetProperty("missing", out _))
                {
                    continue;
                }

                var title = page.Value.GetPropertyOrNull("title")?.GetString();
                var extract = page.Value.GetPropertyOrNull("extract")?.GetString();

                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(extract))
                {
                    continue;
                }

                result[title] = Shorten(extract, maxLength);
            }

            // Yönlendirilen başlıklar da aynı özete işaret etmeli
            ApplyRedirects(query.Value, result);
        }

        return result;
    }

    private static void ApplyRedirects(JsonElement query, Dictionary<string, string> result)
    {
        var redirects = query.GetPropertyOrNull("redirects");

        if (redirects is not { ValueKind: JsonValueKind.Array })
        {
            return;
        }

        foreach (var redirect in redirects.Value.EnumerateArray())
        {
            var from = redirect.GetPropertyOrNull("from")?.GetString();
            var to = redirect.GetPropertyOrNull("to")?.GetString();

            if (!string.IsNullOrWhiteSpace(from)
                && !string.IsNullOrWhiteSpace(to)
                && result.TryGetValue(to, out var extract))
            {
                result[from] = extract;
            }
        }
    }

    /// <summary>
    /// Özeti kart üzerinde okunabilir uzunluğa indirir; cümle ortasında kesmez.
    /// </summary>
    internal static string Shorten(string text, int maxLength)
    {
        var normalized = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        if (normalized.Length <= maxLength)
        {
            return normalized;
        }

        var cut = normalized[..maxLength];
        var lastSentenceEnd = cut.LastIndexOfAny(['.', '!', '?']);

        // Cümle sonu makul bir yerdeyse oradan kes, değilse son kelimeden
        if (lastSentenceEnd > maxLength / 2)
        {
            return cut[..(lastSentenceEnd + 1)].TrimEnd();
        }

        var lastSpace = cut.LastIndexOf(' ');

        return (lastSpace > 0 ? cut[..lastSpace] : cut).TrimEnd() + "…";
    }
}
