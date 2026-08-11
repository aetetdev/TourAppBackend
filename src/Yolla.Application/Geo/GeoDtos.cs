namespace Yolla.Application.Geo;

/// <summary>Şehir listesi ve arama sonucu.</summary>
public sealed record CityDto
{
    /// <example>34</example>
    public required int Id { get; init; }

    /// <example>İstanbul</example>
    public required string Name { get; init; }

    /// <example>istanbul</example>
    public required string Slug { get; init; }

    public required double Latitude { get; init; }

    public required double Longitude { get; init; }

    /// <summary>
    /// Kart olarak gösterilebilecek yer sayısı. Sıfır ise uygulama o şehir için
    /// henüz içerik sunamaz; istemci bunu kullanıcıya bildirebilir.
    /// </summary>
    public required int ReadyPlaceCount { get; init; }
}

/// <summary>Ülke bilgisi.</summary>
public sealed record CountryDto
{
    public required int Id { get; init; }

    /// <example>TR</example>
    public required string Iso2 { get; init; }

    /// <example>Türkiye</example>
    public required string Name { get; init; }

    /// <summary>Bu ülkede kart olarak gösterilebilecek yer sayısı.</summary>
    public required int ReadyPlaceCount { get; init; }
}
