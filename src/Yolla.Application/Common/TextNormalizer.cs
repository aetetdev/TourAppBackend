using System.Globalization;
using System.Text;

namespace Yolla.Application.Common;

// Türkçe metinleri arama ve URL için sadeleştirir.
// Kültüre duyarlı ToLower() burada kullanılamaz: "İstanbul".ToLower() invariant kültürde
// "i̇stanbul" (birleşik nokta) üretir ve arama eşleşmez. Bu yüzden harf eşlemesi elle yapılıyor.
public static class TextNormalizer
{
    private static readonly Dictionary<char, char> TurkishMap = new()
    {
        ['ı'] = 'i', ['I'] = 'i', ['İ'] = 'i', ['i'] = 'i',
        ['ş'] = 's', ['Ş'] = 's',
        ['ğ'] = 'g', ['Ğ'] = 'g',
        ['ü'] = 'u', ['Ü'] = 'u',
        ['ö'] = 'o', ['Ö'] = 'o',
        ['ç'] = 'c', ['Ç'] = 'c',
        ['â'] = 'a', ['Â'] = 'a',
        ['î'] = 'i', ['Î'] = 'i',
        ['û'] = 'u', ['Û'] = 'u'
    };

    // Arama için: "Çanakkale Şehitleri" -> "canakkale sehitleri"
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);

        foreach (var ch in value.Trim())
        {
            if (TurkishMap.TryGetValue(ch, out var mapped))
            {
                builder.Append(mapped);
                continue;
            }

            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
                continue;
            }

            // Kesme işareti Türkçe'de ek ayırıcıdır, kelimeyi bölmemeli: "Ölüdeniz'de" -> "oludenizde"
            if (ch is '\'' or '’' or 'ʼ')
            {
                continue;
            }

            // Tire, eğik çizgi, nokta gibi ayırıcılar kelime sınırı sayılır
            builder.Append(' ');
        }

        return CollapseWhitespace(builder.ToString());
    }

    // URL için: "Ayasofya-i Kebir Camii" -> "ayasofya-i-kebir-camii"
    public static string Slugify(string? value)
    {
        var normalized = Normalize(RemoveDiacritics(value));

        if (normalized.Length == 0)
        {
            return string.Empty;
        }

        return normalized.Replace(' ', '-');
    }

    // Aynı ada sahip yerleri ayırmak için slug'a ayırt edici son ek ekler:
    // "kale" + "canakkale" -> "kale-canakkale"
    public static string SlugifyWithSuffix(string? value, string? suffix)
    {
        var slug = Slugify(value);
        var slugSuffix = Slugify(suffix);

        if (slug.Length == 0)
        {
            return slugSuffix;
        }

        return slugSuffix.Length == 0 ? slug : $"{slug}-{slugSuffix}";
    }

    private static string RemoveDiacritics(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        // Türkçe harfler TurkishMap'te ele alınıyor; bu adım yabancı dil kayıtları için
        // (örneğin "Café", "Ürgüp'te bulunan Château") aksanları temizler.
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var ch in decomposed)
        {
            if (TurkishMap.ContainsKey(ch))
            {
                builder.Append(ch);
                continue;
            }

            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string CollapseWhitespace(string value)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parts);
    }
}
