using System.Globalization;
using System.Text;

namespace Yolla.Application.Routing;

/// <summary>
/// Koridor sayfalama imleci: yol üzerindeki ilerleme oranı ve yer kimliği.
/// </summary>
/// <remarks>
/// Koridor kartları yol sırasına göre geldiği için imleç de ilerleme oranını taşır.
/// Böylece kullanıcı İstanbul'dan çıkarken önce yolun başındaki yerleri görür ve
/// sonraki sayfa kaldığı yerden devam eder.
/// </remarks>
public readonly record struct CorridorCursor(double Progress, int PlaceId)
{
    public string Encode()
    {
        var raw = FormattableString.Invariant($"{Progress:R}:{PlaceId}");

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    public static bool TryDecode(string? value, out CorridorCursor cursor)
    {
        cursor = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        Span<byte> buffer = stackalloc byte[128];

        if (!Convert.TryFromBase64String(value, buffer, out var written))
        {
            return false;
        }

        var parts = Encoding.UTF8.GetString(buffer[..written]).Split(':');

        if (parts.Length != 2
            || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var progress)
            || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var placeId)
            || progress is < 0 or > 1
            || double.IsNaN(progress))
        {
            return false;
        }

        cursor = new CorridorCursor(progress, placeId);

        return true;
    }
}
