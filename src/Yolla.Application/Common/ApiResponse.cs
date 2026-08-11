namespace Yolla.Application.Common;

/// <summary>
/// Tüm başarılı API yanıtlarının ortak zarfı.
/// </summary>
/// <remarks>
/// İstemcilerin tek bir biçim beklemesi için kullanılır. Hatalar bu zarfla değil,
/// RFC 7807 <c>ProblemDetails</c> biçiminde döner.
/// </remarks>
/// <typeparam name="T">Taşınan veri tipi.</typeparam>
public sealed record ApiResponse<T>
{
    /// <summary>Yanıtın taşıdığı veri.</summary>
    public required T Data { get; init; }

    /// <summary>
    /// Veri kaynaklarının zorunlu atıf metinleri. Harita ve fotoğraf gösteriminde
    /// ekranda gösterilmesi hukuki yükümlülüktür.
    /// </summary>
    public IReadOnlyList<string>? Attributions { get; init; }

    public static ApiResponse<T> Create(T data, IReadOnlyList<string>? attributions = null) =>
        new() { Data = data, Attributions = attributions };
}

/// <summary>
/// İmleç (cursor) tabanlı sayfalama sonucu.
/// </summary>
/// <remarks>
/// Sayfa numarası yerine imleç kullanılıyor: kart destesinde kullanıcı kaydırdıkça
/// yeni kayıtlar geliyor ve araya giren değişiklikler sayfa kaymasına yol açmamalı.
/// </remarks>
public sealed record CursorPage<T>
{
    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>Sonraki sayfayı istemek için gönderilecek imleç. Null ise liste bitmiştir.</summary>
    public string? NextCursor { get; init; }

    public bool HasMore => NextCursor is not null;

    public static CursorPage<T> Create(IReadOnlyList<T> items, string? nextCursor) =>
        new() { Items = items, NextCursor = nextCursor };

    public static CursorPage<T> Empty() =>
        new() { Items = [], NextCursor = null };
}
