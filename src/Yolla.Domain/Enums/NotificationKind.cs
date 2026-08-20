namespace Yolla.Domain.Enums;

/// <summary>Bildirimin sebebi.</summary>
public enum NotificationKind
{
    /// <summary>Gönderilen fotoğraf yayına alındı.</summary>
    PhotoApproved = 0,

    /// <summary>Gönderilen fotoğraf reddedildi.</summary>
    PhotoRejected = 1,

    /// <summary>Önerilen yer kataloğa alındı.</summary>
    SuggestionApproved = 2,

    /// <summary>Önerilen yer reddedildi.</summary>
    SuggestionRejected = 3
}
