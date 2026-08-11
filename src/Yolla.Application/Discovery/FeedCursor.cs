using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace Yolla.Application.Discovery;

/// <summary>
/// Kart destesi sayfalama imleci.
/// </summary>
/// <remarks>
/// Sayfa numarası yerine son görülen kaydın (puan, kimlik) ikilisi taşınır. Böylece araya
/// yeni kayıt girse bile kullanıcı aynı kartı iki kez görmez veya bir kartı atlamaz -
/// OFFSET tabanlı sayfalamanın klasik sorunu.
/// </remarks>
public readonly record struct FeedCursor(short QualityScore, int PlaceId)
{
    public string Encode()
    {
        var raw = string.Create(CultureInfo.InvariantCulture, $"{QualityScore}:{PlaceId}");

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    public static bool TryDecode(string? value, out FeedCursor cursor)
    {
        cursor = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        Span<byte> buffer = stackalloc byte[64];

        if (!Convert.TryFromBase64String(value, buffer, out var written))
        {
            return false;
        }

        var parts = Encoding.UTF8.GetString(buffer[..written]).Split(':');

        if (parts.Length != 2
            || !short.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var score)
            || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var placeId))
        {
            return false;
        }

        cursor = new FeedCursor(score, placeId);

        return true;
    }
}
