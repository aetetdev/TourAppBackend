namespace Yolla.Domain.Enums;

// Kart destesindeki kaydırma yönü
public enum SwipeDirection
{
    // Sola: ilgilenmiyorum
    Pass = 0,

    // Sağa: plana ekle
    Like = 1,

    // Aşağı: şimdilik atla, tekrar gösterilebilir
    Later = 2
}
