using Shouldly;
using Yolla.Application.Notifications;
using Yolla.Domain.Enums;

namespace Yolla.Application.Tests.Notifications;

/// <summary>
/// Bildirim metinleri.
/// </summary>
/// <remarks>
/// Metin sunucuda kuruluyor çünkü bildirim telefon kapalıyken de gelmek
/// zorunda ve o anda uygulamanın metin dosyaları devrede değil. Bu da metnin
/// doğru dilde ve anlamlı olmasını sunucunun sorumluluğu yapıyor: burada
/// bozulan bir şey doğrudan kullanıcının kilit ekranına düşüyor.
/// </remarks>
public class NotificationTextsTests
{
    [Fact]
    public void Onay_metni_yerin_adini_ve_coini_soyluyor()
    {
        var (title, body) = NotificationTexts.Build(
            NotificationKind.PhotoApproved,
            new NotificationContext { PlaceName = "Ayasofya", Coins = 10 },
            "tr");

        title.ShouldNotBeNullOrWhiteSpace();
        body.ShouldContain("Ayasofya");
        body.ShouldContain("10");
    }

    [Fact]
    public void Coin_verilmediyse_metinde_coin_gecmiyor()
    {
        // Ekip kendi eklediği içerikte coin yazmıyor; "0 coin kazandın"
        // demek kullanıcıyı yanıltır.
        var (_, body) = NotificationTexts.Build(
            NotificationKind.SuggestionApproved,
            new NotificationContext { PlaceName = "Köy Çeşmesi" },
            "tr");

        body.ShouldContain("Köy Çeşmesi");
        body.ShouldNotContain("coin");
    }

    [Fact]
    public void Red_metni_sebebi_tasiyor()
    {
        // Sebepsiz "reddedildi" kullanıcıyı aynı hatayı tekrar yapmaya
        // bırakıyor.
        var (_, body) = NotificationTexts.Build(
            NotificationKind.SuggestionRejected,
            new NotificationContext
            {
                PlaceName = "Deneme Yeri",
                Reason = "Bu konumda böyle bir yer bulunamadı."
            },
            "tr");

        body.ShouldContain("Deneme Yeri");
        body.ShouldContain("bulunamadı");
    }

    [Fact]
    public void Ingilizce_cihazda_metin_ingilizce()
    {
        var (title, body) = NotificationTexts.Build(
            NotificationKind.PhotoApproved,
            new NotificationContext { PlaceName = "Hagia Sophia", Coins = 10 },
            "en");

        title.ShouldBe("Your photo is live");
        body.ShouldContain("Hagia Sophia");
    }

    [Theory]
    [InlineData(NotificationKind.PhotoApproved)]
    [InlineData(NotificationKind.PhotoRejected)]
    [InlineData(NotificationKind.SuggestionApproved)]
    [InlineData(NotificationKind.SuggestionRejected)]
    public void Her_tur_iki_dilde_de_dolu_metin_uretiyor(NotificationKind kind)
    {
        foreach (var dil in new[] { "tr", "en" })
        {
            var (title, body) = NotificationTexts.Build(
                kind,
                new NotificationContext { PlaceName = "Test Yeri" },
                dil);

            title.ShouldNotBeNullOrWhiteSpace($"{kind} / {dil} başlığı boş.");
            body.ShouldContain(
                "Test Yeri",
                customMessage: $"{kind} / {dil} gövdesi yeri anmıyor.");
        }
    }
}
