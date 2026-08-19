namespace Yolla.Application.Rewards;

/// <summary>Fotoğraf katkısı, coin ve premium.</summary>
public interface IRewardService
{
    /// <summary>Bir yer için fotoğraf gönderir. Gönderi moderasyona düşer.</summary>
    Task<PhotoSubmissionDto> SubmitPhotoAsync(
        int userId,
        int? deviceId,
        int placeId,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>Kullanıcının kendi gönderileri, yeniden eskiye.</summary>
    Task<IReadOnlyList<PhotoSubmissionDto>> GetMySubmissionsAsync(
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>Coin bakiyesi, premium durumu ve aylık plan kotası.</summary>
    Task<RewardStatusDto> GetStatusAsync(
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>Coin defteri, yeniden eskiye.</summary>
    Task<IReadOnlyList<CoinEntryDto>> GetCoinHistoryAsync(
        int userId,
        int take = 50,
        CancellationToken cancellationToken = default);

    /// <summary>Coini premium hakkına çevirir.</summary>
    Task<RewardStatusDto> RedeemAsync(
        int userId,
        PremiumPackage package,
        CancellationToken cancellationToken = default);

    /// <summary>Kullanıcının şu an geçerli premium hakkı var mı?</summary>
    Task<bool> IsPremiumAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kullanıcı yeni bir plan kaydedebilir mi?
    /// </summary>
    /// <remarks>
    /// Ücretsiz hesap bir takvim ayında <see cref="RewardRules.FreeMonthlyTripLimit"/>
    /// plan kaydedebiliyor; premiumda sınır yok.
    /// </remarks>
    Task<bool> CanCreateTripAsync(int userId, CancellationToken cancellationToken = default);

    // --- Moderasyon ---

    /// <summary>İncelenmeyi bekleyen gönderiler, eskiden yeniye.</summary>
    Task<IReadOnlyList<ModerationItemDto>> GetPendingAsync(
        int take = 50,
        CancellationToken cancellationToken = default);

    /// <summary>Gönderiyi yayına alır ve kullanıcıya coin verir.</summary>
    Task ApproveAsync(
        int submissionId,
        int reviewerUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Gönderiyi reddeder. Coin verilmez.</summary>
    Task RejectAsync(
        int submissionId,
        int reviewerUserId,
        string reason,
        CancellationToken cancellationToken = default);
}

/// <summary>Gönderilen fotoğrafların saklandığı yer.</summary>
/// <remarks>
/// Şimdilik yerel dosya sistemi. Arayüz olarak ayrılması, sunucuya çıkarken
/// nesne deposuna (S3 uyumlu) geçmeyi tek sınıf değiştirmeye indiriyor —
/// birden fazla sunucu çalıştığında yerel disk zaten yetmez.
/// </remarks>
public interface IPhotoStorage
{
    /// <summary>Dosyayı kaydeder, göreli yolunu döndürür.</summary>
    Task<string> SaveAsync(
        string relativePath,
        Stream content,
        CancellationToken cancellationToken = default);

    /// <summary>Göreli yolu, istemcinin açabileceği adrese çevirir.</summary>
    string UrlFor(string relativePath);

    Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default);
}
