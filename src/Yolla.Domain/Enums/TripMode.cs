namespace Yolla.Domain.Enums;

// Uygulamanın iki çalışma modu
public enum TripMode
{
    // Şehir içi gezi: seçilen şehirdeki yerler, yürüme rotası
    City = 0,

    // Şehirlerarası yolculuk: rota koridoru üzerindeki yerler
    Route = 1
}
