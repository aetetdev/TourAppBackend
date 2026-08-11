using Shouldly;
using Yolla.Application.Routing;

namespace Yolla.Application.Tests.Routing;

public class CorridorCursorTests
{
    [Fact]
    public void Imlec_kodlanip_cozulur()
    {
        var cursor = new CorridorCursor(0.42731, 12345);

        CorridorCursor.TryDecode(cursor.Encode(), out var decoded).ShouldBeTrue();

        decoded.PlaceId.ShouldBe(12345);
        decoded.Progress.ShouldBe(0.42731, 0.000001);
    }

    [Fact]
    public void Ondalik_hassasiyet_korunur()
    {
        // İlerleme oranı yuvarlanırsa aynı kart iki kez gelebilir veya bir kart atlanabilir
        var cursor = new CorridorCursor(0.123456789012345, 1);

        CorridorCursor.TryDecode(cursor.Encode(), out var decoded).ShouldBeTrue();

        decoded.Progress.ShouldBe(0.123456789012345);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    public void Sinir_degerleri_kabul_edilir(double progress)
    {
        var cursor = new CorridorCursor(progress, 5);

        CorridorCursor.TryDecode(cursor.Encode(), out var decoded).ShouldBeTrue();
        decoded.Progress.ShouldBe(progress);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("base64-degil!")]
    [InlineData("YWJj")]
    public void Gecersiz_imlec_reddedilir(string? value)
    {
        CorridorCursor.TryDecode(value, out _).ShouldBeFalse();
    }

    [Fact]
    public void Aralik_disindaki_ilerleme_reddedilir()
    {
        // İlerleme oranı 0-1 arasında olmalı; dışarıdan gelen bozuk değer sorguyu şaşırtır
        var disaridan = Convert.ToBase64String("1.5:10"u8.ToArray());

        CorridorCursor.TryDecode(disaridan, out _).ShouldBeFalse();
    }

    [Fact]
    public void Sayisal_olmayan_deger_reddedilir()
    {
        var bozuk = Convert.ToBase64String("abc:xyz"u8.ToArray());

        CorridorCursor.TryDecode(bozuk, out _).ShouldBeFalse();
    }
}
