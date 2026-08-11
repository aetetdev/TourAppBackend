using System.Net;

namespace Yolla.Harvester.Enrichment;

/// <summary>
/// Wikimedia API'lerine nazik davranan HTTP istemcisi.
/// </summary>
/// <remarks>
/// Wikimedia'nın kullanım politikası, kendini tanıtan bir User-Agent ve makul bir istek
/// hızı bekler; aksi halde IP engellenir. Bu yüzden istekler seri gönderilir, aralarında
/// kısa bekleme vardır ve 429/503 yanıtlarında artan gecikmeyle yeniden denenir.
/// </remarks>
public sealed class WikimediaHttpClient : IDisposable
{
    private const string UserAgent = "Yolla/1.0 (https://github.com/aetetdev/TourAppBackend)";

    private readonly HttpClient _httpClient;
    private readonly TimeSpan _delayBetweenRequests;
    private readonly int _maxRetries;

    private DateTimeOffset _lastRequestAt = DateTimeOffset.MinValue;

    public WikimediaHttpClient(
        HttpMessageHandler? handler = null,
        TimeSpan? delayBetweenRequests = null,
        int maxRetries = 4)
    {
        // Accept-Encoding başlığını elle eklemek yetmez: otomatik açma kapalıysa sıkıştırılmış
        // gövde ham haliyle okunur ve JSON ayrıştırması sessizce başarısız olur.
        _httpClient = handler is null
            ? new HttpClient(new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            })
            : new HttpClient(handler, disposeHandler: false);

        _httpClient.Timeout = TimeSpan.FromSeconds(60);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);

        _delayBetweenRequests = delayBetweenRequests ?? TimeSpan.FromMilliseconds(150);
        _maxRetries = maxRetries;
    }

    public async Task<string?> GetStringAsync(string url, CancellationToken cancellationToken = default)
    {
        var delay = TimeSpan.FromSeconds(2);

        for (var attempt = 1; attempt <= _maxRetries; attempt++)
        {
            await ThrottleAsync(cancellationToken);

            try
            {
                using var response = await _httpClient.GetAsync(url, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync(cancellationToken);
                }

                // Kalıcı hatalarda yeniden denemenin anlamı yok
                if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
                {
                    return null;
                }

                if (attempt == _maxRetries)
                {
                    return null;
                }
            }
            catch (HttpRequestException) when (attempt < _maxRetries)
            {
                // Ağ hatası: yeniden dene
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < _maxRetries)
            {
                // Zaman aşımı: yeniden dene
            }

            await Task.Delay(delay, cancellationToken);
            delay *= 2;
        }

        return null;
    }

    private async Task ThrottleAsync(CancellationToken cancellationToken)
    {
        var elapsed = DateTimeOffset.UtcNow - _lastRequestAt;

        if (elapsed < _delayBetweenRequests)
        {
            await Task.Delay(_delayBetweenRequests - elapsed, cancellationToken);
        }

        _lastRequestAt = DateTimeOffset.UtcNow;
    }

    public void Dispose() => _httpClient.Dispose();
}
