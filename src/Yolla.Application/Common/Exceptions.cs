namespace Yolla.Application.Common;

/// <summary>İstenen kayıt bulunamadı. HTTP 404'e çevrilir.</summary>
public sealed class NotFoundException(string resource, object key)
    : Exception($"{resource} bulunamadı: {key}")
{
    public string Resource { get; } = resource;

    public object Key { get; } = key;
}

/// <summary>İstek geçerli değil. HTTP 400'e çevrilir.</summary>
public sealed class RequestValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("İstek doğrulanamadı.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;

    public static RequestValidationException Single(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}

/// <summary>Dış servis (OSRM, Wikimedia) yanıt vermedi. HTTP 502'ye çevrilir.</summary>
public sealed class UpstreamServiceException(string service, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public string Service { get; } = service;
}
