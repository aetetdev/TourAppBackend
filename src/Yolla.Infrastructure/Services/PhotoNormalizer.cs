using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using Yolla.Application.Common;
using Yolla.Application.Rewards;

namespace Yolla.Infrastructure.Services;

/// <summary>
/// Gelen fotoğrafı yayına uygun hale getirir.
/// </summary>
/// <remarks>
/// İki yerden çağrılıyor — kullanıcı gönderisi ve içerik ekibinin elle
/// eklediği fotoğraf — ve ikisinde de aynı davranmak zorunda. Özellikle
/// **EXIF temizliği**: kopyalanan bir uygulama bir gün onu atlarsa,
/// kullanıcının fotoğrafı çektiği konum dosyayla birlikte yayına çıkar.
/// Tek bir yerde durması bunu unutulamaz kılıyor.
/// </remarks>
public static class PhotoNormalizer
{
    /// <summary>Saklanan görselin en uzun kenarı (piksel).</summary>
    public const int MaxStoredEdge = 1920;

    /// <summary>JPEG kalitesi; 82 gözle farkı görülmeden dosyayı küçültüyor.</summary>
    private const int JpegQuality = 82;

    /// <summary>
    /// Görseli okur, boyutunu sınırlar, yönünü düzeltir ve EXIF'i siler.
    /// </summary>
    /// <remarks>Dönen görselin sahibi çağıran; işi bitince atmalı.</remarks>
    public static async Task<Image> LoadAsync(
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        Image image;
        try
        {
            image = await Image.LoadAsync(content, cancellationToken);
        }
        catch (UnknownImageFormatException)
        {
            throw RequestValidationException.Single(
                "photo", "Dosya okunamadı; JPEG veya PNG bir fotoğraf gönder.");
        }

        if (image.Width < RewardRules.MinPhotoEdge && image.Height < RewardRules.MinPhotoEdge)
        {
            image.Dispose();
            throw RequestValidationException.Single(
                "photo",
                $"Fotoğraf çok küçük. Kısa kenarı en az {RewardRules.MinPhotoEdge} piksel olmalı.");
        }

        if (image.Width > MaxStoredEdge || image.Height > MaxStoredEdge)
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(MaxStoredEdge, MaxStoredEdge)
            }));
        }

        // EXIF yönü uygulanıp temizleniyor: telefon fotoğrafları yan
        // görünmesin, konum bilgisi de dosyayla birlikte yayına çıkmasın.
        image.Mutate(x => x.AutoOrient());
        image.Metadata.ExifProfile = null;

        return image;
    }

    /// <summary>Görseli JPEG olarak belleğe yazar.</summary>
    public static async Task<MemoryStream> ToJpegAsync(
        Image image,
        CancellationToken cancellationToken = default)
    {
        var buffer = new MemoryStream();
        await image.SaveAsync(buffer, new JpegEncoder { Quality = JpegQuality }, cancellationToken);
        buffer.Position = 0;

        return buffer;
    }
}
