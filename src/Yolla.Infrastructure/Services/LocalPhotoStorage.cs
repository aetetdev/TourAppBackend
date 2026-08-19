using Microsoft.Extensions.Configuration;
using Yolla.Application.Rewards;

namespace Yolla.Infrastructure.Services;

/// <summary>
/// Gönderilen fotoğrafları yerel diske yazar.
/// </summary>
/// <remarks>
/// Tek sunucu varsayımıyla çalışıyor. Birden fazla sunucu çalıştırıldığında
/// bir sunucuya yüklenen fotoğrafı diğeri göremez — o noktada
/// <see cref="IPhotoStorage"/>'ın S3 uyumlu bir uygulaması yazılmalı.
/// Arayüz tam bunun için ayrıldı.
///
/// Kök klasör <c>Storage:PhotoRoot</c>, dışarıdan görünen adres
/// <c>Storage:PublicBaseUrl</c> ile yapılandırılıyor.
/// </remarks>
public sealed class LocalPhotoStorage : IPhotoStorage
{
    private readonly string _root;
    private readonly string _publicBaseUrl;

    public LocalPhotoStorage(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _root = configuration["Storage:PhotoRoot"]
                ?? Path.Combine(AppContext.BaseDirectory, "uploads");

        _publicBaseUrl = (configuration["Storage:PublicBaseUrl"] ?? "/uploads")
            .TrimEnd('/');

        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(
        string relativePath,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(content);

        var target = ResolveInsideRoot(relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);

        await using var file = File.Create(target);
        await content.CopyToAsync(file, cancellationToken);

        return relativePath;
    }

    public string UrlFor(string relativePath) =>
        $"{_publicBaseUrl}/{relativePath.Replace('\\', '/').TrimStart('/')}";

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var target = ResolveInsideRoot(relativePath);

        if (File.Exists(target)) File.Delete(target);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Göreli yolu kök klasörün içinde çözer.
    /// </summary>
    /// <remarks>
    /// Yol kökün dışına çıkıyorsa reddediliyor: göreli yol bir gün dışarıdan
    /// gelen bir değerle kurulursa <c>../../</c> ile sunucudaki başka
    /// dosyalara yazılmasını engelliyor.
    /// </remarks>
    private string ResolveInsideRoot(string relativePath)
    {
        var full = Path.GetFullPath(Path.Combine(_root, relativePath));
        var rootFull = Path.GetFullPath(_root);

        if (!full.StartsWith(rootFull, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Depolama kökünün dışına çıkan yol reddedildi: {relativePath}");
        }

        return full;
    }
}
