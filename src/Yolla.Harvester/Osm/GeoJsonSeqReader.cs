using System.Globalization;
using System.Text.Json;
using NetTopologySuite.Geometries;
using Yolla.Domain.Enums;

namespace Yolla.Harvester.Osm;

/// <summary>
/// osmium'un ürettiği GeoJSONSeq (RFC 8142) dosyalarını satır satır okur.
/// </summary>
/// <remarks>
/// Dosyanın tamamı belleğe alınmaz: Türkiye POI çıktısı yüz binlerce satır olabiliyor.
/// Bozuk satırlar tüm içe aktarımı düşürmez, atlanır ve sayılır.
/// </remarks>
public sealed class GeoJsonSeqReader
{
    // RFC 8142 kayıt ayırıcısı; osmium her satırın başına bunu koyar
    private const char RecordSeparator = '';

    private static readonly GeometryFactory GeometryFactory = new(new PrecisionModel(), 4326);

    public int SkippedLineCount { get; private set; }

    /// <summary>Dosyadaki kayıtları tembel (lazy) olarak döndürür.</summary>
    public IEnumerable<OsmFeature> Read(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"GeoJSONSeq dosyası bulunamadı: {filePath}", filePath);
        }

        foreach (var rawLine in File.ReadLines(filePath))
        {
            var line = rawLine.Trim().Trim(RecordSeparator);

            if (line.Length == 0)
            {
                continue;
            }

            OsmFeature? feature;

            try
            {
                feature = ParseLine(line);
            }
            catch (JsonException)
            {
                // Yarım yazılmış ya da bozuk satır: içe aktarımı durdurmaya değmez
                SkippedLineCount++;
                continue;
            }

            if (feature is null)
            {
                SkippedLineCount++;
                continue;
            }

            yield return feature;
        }
    }

    private static OsmFeature? ParseLine(string line)
    {
        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;

        if (!root.TryGetProperty("id", out var idElement))
        {
            return null;
        }

        if (!TryParseOsmId(idElement.GetString(), out var elementType, out var osmId))
        {
            return null;
        }

        if (!root.TryGetProperty("geometry", out var geometryElement))
        {
            return null;
        }

        var geometry = ParseGeometry(geometryElement);

        if (geometry is null || geometry.IsEmpty)
        {
            return null;
        }

        var location = geometry is Point point
            ? point
            : GeometryFactory.CreatePoint(geometry.Centroid.Coordinate);

        if (location.IsEmpty || !IsFinite(location.X) || !IsFinite(location.Y))
        {
            return null;
        }

        return new OsmFeature
        {
            ElementType = elementType,
            OsmId = osmId,
            Tags = ParseTags(root),
            Location = location,
            Area = geometry is Point ? null : geometry
        };
    }

    /// <summary>
    /// osmium'un "--add-unique-id=type_id" ile ürettiği kimliği çözer: n123, w456, r789, a1000.
    /// </summary>
    /// <remarks>
    /// Alan geometrileri (kale, milli park, müze binası) "a" ön ekiyle gelir ve kimlik
    /// osmium'un alan numaralandırmasıdır: yol kaynaklı alanlarda <c>way_id * 2</c>,
    /// ilişki kaynaklı alanlarda <c>relation_id * 2 + 1</c>. Kaynak nesneye geri çevrilmesi
    /// şart, çünkü kayıtların tekilliği (osm_type, osm_id) ikilisine dayanıyor: aynı kale
    /// hem alan hem ilişki olarak sayılırsa veritabanında iki kayıt oluşurdu.
    /// </remarks>
    internal static bool TryParseOsmId(string? value, out OsmElementType elementType, out long osmId)
    {
        elementType = OsmElementType.Node;
        osmId = 0;

        if (string.IsNullOrWhiteSpace(value) || value.Length < 2)
        {
            return false;
        }

        var prefix = char.ToLowerInvariant(value[0]);

        if (prefix is not ('n' or 'w' or 'r' or 'a'))
        {
            return false;
        }

        if (!long.TryParse(value.AsSpan(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var rawId)
            || rawId <= 0)
        {
            return false;
        }

        if (prefix is 'a')
        {
            // Tek numaralı alanlar ilişkiden, çift numaralılar yoldan üretilir
            var fromRelation = (rawId & 1) == 1;

            elementType = fromRelation ? OsmElementType.Relation : OsmElementType.Way;
            osmId = fromRelation ? (rawId - 1) / 2 : rawId / 2;

            return osmId > 0;
        }

        elementType = prefix switch
        {
            'n' => OsmElementType.Node,
            'w' => OsmElementType.Way,
            _ => OsmElementType.Relation
        };

        osmId = rawId;

        return true;
    }

    private static Dictionary<string, string> ParseTags(JsonElement root)
    {
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!root.TryGetProperty("properties", out var properties)
            || properties.ValueKind != JsonValueKind.Object)
        {
            return tags;
        }

        foreach (var property in properties.EnumerateObject())
        {
            // osmium etiket değerlerini metin olarak yazar; sayısal gelenler de metne çevrilir
            var value = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Number => property.Value.GetRawText(),
                JsonValueKind.True => "yes",
                JsonValueKind.False => "no",
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(value))
            {
                tags[property.Name] = value;
            }
        }

        return tags;
    }

    private static Geometry? ParseGeometry(JsonElement geometryElement)
    {
        if (!geometryElement.TryGetProperty("type", out var typeElement)
            || !geometryElement.TryGetProperty("coordinates", out var coordinates))
        {
            return null;
        }

        return typeElement.GetString() switch
        {
            "Point" => ParsePoint(coordinates),
            "Polygon" => ParsePolygon(coordinates),
            "MultiPolygon" => ParseMultiPolygon(coordinates),
            _ => null
        };
    }

    private static Point? ParsePoint(JsonElement coordinates)
    {
        if (coordinates.ValueKind != JsonValueKind.Array || coordinates.GetArrayLength() < 2)
        {
            return null;
        }

        // GeoJSON sırası: [longitude, latitude]
        var longitude = coordinates[0].GetDouble();
        var latitude = coordinates[1].GetDouble();

        if (!IsValidCoordinate(longitude, latitude))
        {
            return null;
        }

        return GeometryFactory.CreatePoint(new Coordinate(longitude, latitude));
    }

    private static Polygon? ParsePolygon(JsonElement coordinates)
    {
        if (coordinates.ValueKind != JsonValueKind.Array || coordinates.GetArrayLength() == 0)
        {
            return null;
        }

        var shell = ParseRing(coordinates[0]);

        if (shell is null)
        {
            return null;
        }

        var holes = new List<LinearRing>();

        for (var i = 1; i < coordinates.GetArrayLength(); i++)
        {
            var hole = ParseRing(coordinates[i]);

            if (hole is not null)
            {
                holes.Add(hole);
            }
        }

        return GeometryFactory.CreatePolygon(shell, holes.ToArray());
    }

    private static Geometry? ParseMultiPolygon(JsonElement coordinates)
    {
        if (coordinates.ValueKind != JsonValueKind.Array || coordinates.GetArrayLength() == 0)
        {
            return null;
        }

        var polygons = new List<Polygon>();

        foreach (var polygonElement in coordinates.EnumerateArray())
        {
            var polygon = ParsePolygon(polygonElement);

            if (polygon is not null)
            {
                polygons.Add(polygon);
            }
        }

        return polygons.Count == 0
            ? null
            : GeometryFactory.CreateMultiPolygon(polygons.ToArray());
    }

    private static LinearRing? ParseRing(JsonElement ringElement)
    {
        if (ringElement.ValueKind != JsonValueKind.Array || ringElement.GetArrayLength() < 4)
        {
            return null;
        }

        var coordinates = new List<Coordinate>(ringElement.GetArrayLength());

        foreach (var pointElement in ringElement.EnumerateArray())
        {
            if (pointElement.ValueKind != JsonValueKind.Array || pointElement.GetArrayLength() < 2)
            {
                return null;
            }

            var longitude = pointElement[0].GetDouble();
            var latitude = pointElement[1].GetDouble();

            if (!IsValidCoordinate(longitude, latitude))
            {
                return null;
            }

            coordinates.Add(new Coordinate(longitude, latitude));
        }

        // Halka kapalı olmalı
        if (!coordinates[0].Equals2D(coordinates[^1]))
        {
            coordinates.Add(coordinates[0].Copy());
        }

        return coordinates.Count < 4
            ? null
            : GeometryFactory.CreateLinearRing(coordinates.ToArray());
    }

    private static bool IsValidCoordinate(double longitude, double latitude) =>
        IsFinite(longitude) && IsFinite(latitude)
        && longitude is >= -180 and <= 180
        && latitude is >= -90 and <= 90;

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
