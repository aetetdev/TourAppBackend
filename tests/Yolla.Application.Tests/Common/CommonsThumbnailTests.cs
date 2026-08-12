using Shouldly;
using Yolla.Application.Common;

namespace Yolla.Application.Tests.Common;

public class CommonsThumbnailTests
{
    private const string Original =
        "https://upload.wikimedia.org/wikipedia/commons/f/f0/Uchisar_Castle.jpg";

    [Fact]
    public void Commons_adresi_kucultulmus_surume_cevrilir()
    {
        CommonsThumbnail.For(Original, 500).ShouldBe(
            "https://upload.wikimedia.org/wikipedia/commons/thumb/f/f0/" +
            "Uchisar_Castle.jpg/500px-Uchisar_Castle.jpg");
    }

    [Theory]
    [InlineData(1, 20)]
    [InlineData(20, 20)]
    [InlineData(21, 40)]
    [InlineData(400, 500)]
    [InlineData(800, 960)]
    [InlineData(1080, 1280)]
    [InlineData(9000, 3840)]
    public void Genislik_izin_verilen_en_yakin_ust_kovaya_yuvarlanir(int requested, int expected)
    {
        // Wikimedia standart dışı genişlikleri 400 Bad Request ile reddediyor;
        // yuvarlama olmadan istemci hiçbir görsel gösteremezdi
        CommonsThumbnail.For(Original, requested).ShouldEndWith($"/{expected}px-Uchisar_Castle.jpg");
    }

    [Fact]
    public void Hazir_kisayollar_belgelenen_genislikleri_uretir()
    {
        CommonsThumbnail.Thumb(Original)!.ShouldContain("/500px-");
        CommonsThumbnail.Large(Original)!.ShouldContain("/960px-");
    }

    [Fact]
    public void Commons_disi_proje_dosyalari_da_cevrilir()
    {
        // Wikipedia'nın yerel dosyaları da aynı thumb desenini kullanıyor
        CommonsThumbnail.For("https://upload.wikimedia.org/wikipedia/tr/a/ab/Yerel.jpg", 250)
            .ShouldBe("https://upload.wikimedia.org/wikipedia/tr/thumb/a/ab/Yerel.jpg/250px-Yerel.jpg");
    }

    [Theory]
    // Başka bir kaynak: desen tutmaz
    [InlineData("https://cdn.yolla.travel/foto.jpg")]
    // Zaten küçültülmüş: ikinci kez küçültülemez
    [InlineData("https://upload.wikimedia.org/wikipedia/commons/thumb/f/f0/Ad.jpg/500px-Ad.jpg")]
    // SVG'nin küçültülmüşü PNG olarak döner, uzantı değişir
    [InlineData("https://upload.wikimedia.org/wikipedia/commons/1/12/Harita.svg")]
    // Beklenmeyen yol derinliği
    [InlineData("https://upload.wikimedia.org/test.jpg")]
    // Sorgu varsa dosya adı belirsizleşir
    [InlineData("https://upload.wikimedia.org/wikipedia/commons/f/f0/Ad.jpg?download=1")]
    // Adres bile değil
    [InlineData("dosya.jpg")]
    public void Desene_uymayan_adres_oldugu_gibi_doner(string url)
    {
        // Görselin kaybolması, büyük görsel göstermekten kötü
        CommonsThumbnail.For(url, 500).ShouldBe(url);
    }

    [Fact]
    public void Bos_deger_oldugu_gibi_doner()
    {
        CommonsThumbnail.Thumb(null).ShouldBeNull();
        CommonsThumbnail.Thumb("").ShouldBe("");
    }

    [Fact]
    public void Dosya_adindaki_ozel_karakterler_korunur()
    {
        // Yol ham haliyle işlenmezse yüzde kodlaması çözülüp yeniden kurulur ve
        // adres sessizce değişir; görsel 404 döner
        const string encoded =
            "https://upload.wikimedia.org/wikipedia/commons/3/3a/Sultan_Ahmet%2C_%C4%B0stanbul.jpg";

        CommonsThumbnail.For(encoded, 500).ShouldBe(
            "https://upload.wikimedia.org/wikipedia/commons/thumb/3/3a/" +
            "Sultan_Ahmet%2C_%C4%B0stanbul.jpg/500px-Sultan_Ahmet%2C_%C4%B0stanbul.jpg");
    }
}
