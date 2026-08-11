using System.Text.Json;

namespace Yolla.Harvester.Enrichment;

/// <summary>Wikidata'dan bir yer hakkında çekilen bilgiler.</summary>
public sealed record WikidataEntity
{
    public required string Id { get; init; }

    /// <summary>Commons'taki fotoğraf dosyası adı (P18 özelliği). Örn: "Hagia Sophia.jpg"</summary>
    public string? ImageFileName { get; init; }

    /// <summary>Türkçe Wikipedia makale başlığı.</summary>
    public string? TurkishWikipediaTitle { get; init; }

    /// <summary>İngilizce Wikipedia makale başlığı.</summary>
    public string? EnglishWikipediaTitle { get; init; }
}

/// <summary>
/// Wikidata <c>wbgetentities</c> yanıtını çözer.
/// </summary>
public static class WikidataResponseParser
{
    public static IReadOnlyDictionary<string, WikidataEntity> Parse(string? json)
    {
        var result = new Dictionary<string, WikidataEntity>(StringComparer.OrdinalIgnoreCase);

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
            if (!document.RootElement.TryGetProperty("entities", out var entities)
                || entities.ValueKind != JsonValueKind.Object)
            {
                return result;
            }

            foreach (var entity in entities.EnumerateObject())
            {
                // Silinmiş ya da bulunamayan kayıtlar "missing" işaretiyle döner
                if (entity.Value.TryGetProperty("missing", out _))
                {
                    continue;
                }

                result[entity.Name] = new WikidataEntity
                {
                    Id = entity.Name,
                    ImageFileName = ReadImageFileName(entity.Value),
                    TurkishWikipediaTitle = ReadSitelink(entity.Value, "trwiki"),
                    EnglishWikipediaTitle = ReadSitelink(entity.Value, "enwiki")
                };
            }
        }

        return result;
    }

    // P18 = "image" özelliği. Birden fazla görsel olabilir; ilki kullanılır.
    private static string? ReadImageFileName(JsonElement entity)
    {
        if (!entity.TryGetProperty("claims", out var claims)
            || !claims.TryGetProperty("P18", out var images)
            || images.ValueKind != JsonValueKind.Array
            || images.GetArrayLength() == 0)
        {
            return null;
        }

        foreach (var image in images.EnumerateArray())
        {
            var value = image
                .GetPropertyOrNull("mainsnak")?
                .GetPropertyOrNull("datavalue")?
                .GetPropertyOrNull("value");

            if (value is { ValueKind: JsonValueKind.String })
            {
                var fileName = value.Value.GetString();

                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    return fileName;
                }
            }
        }

        return null;
    }

    private static string? ReadSitelink(JsonElement entity, string wiki)
    {
        var title = entity
            .GetPropertyOrNull("sitelinks")?
            .GetPropertyOrNull(wiki)?
            .GetPropertyOrNull("title");

        return title is { ValueKind: JsonValueKind.String } ? title.Value.GetString() : null;
    }
}

internal static class JsonElementExtensions
{
    public static JsonElement? GetPropertyOrNull(this JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var value)
            ? value
            : null;

    public static JsonElement? GetPropertyOrNull(this JsonElement? element, string propertyName) =>
        element?.GetPropertyOrNull(propertyName);
}
