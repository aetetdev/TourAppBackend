using Shouldly;
using Yolla.Application.Discovery;

namespace Yolla.Application.Tests.Discovery;

public class FeedCursorTests
{
    [Fact]
    public void Imlec_kodlanip_cozulur()
    {
        var cursor = new FeedCursor(92, 12345);

        FeedCursor.TryDecode(cursor.Encode(), out var decoded).ShouldBeTrue();

        decoded.ShouldBe(cursor);
    }

    [Fact]
    public void Sifir_ve_negatif_puanlar_korunur()
    {
        var cursor = new FeedCursor(0, 1);

        FeedCursor.TryDecode(cursor.Encode(), out var decoded).ShouldBeTrue();
        decoded.QualityScore.ShouldBe((short)0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bu-base64-degil!")]
    [InlineData("YWJj")]              // base64 ama içerik "abc"
    [InlineData("OTI=")]              // ayraç yok
    [InlineData("YTpi")]              // "a:b" - sayı değil
    public void Gecersiz_imlec_reddedilir(string? value)
    {
        // Bozuk imleç hata fırlatmamalı; istemci ilk sayfadan devam edebilmeli
        FeedCursor.TryDecode(value, out _).ShouldBeFalse();
    }

    [Fact]
    public void Imlec_url_guvenli_karakterler_uretir()
    {
        var encoded = new FeedCursor(85, 999999).Encode();

        encoded.ShouldNotContain(" ");
        encoded.ShouldNotContain("\n");
    }
}
