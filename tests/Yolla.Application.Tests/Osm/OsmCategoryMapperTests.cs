using Shouldly;
using Yolla.Application.Osm;

namespace Yolla.Application.Tests.Osm;

public class OsmCategoryMapperTests
{
    // --- Konaklama elemesi: eski veri toplayıcının en büyük sorunu ---

    [Theory]
    [InlineData("hotel")]
    [InlineData("hostel")]
    [InlineData("guest_house")]
    [InlineData("motel")]
    [InlineData("apartment")]
    [InlineData("chalet")]
    [InlineData("alpine_hut")]
    public void Konaklama_tesisleri_gizli_kategoriye_dusrur(string tourismValue)
    {
        var result = OsmCategoryMapper.Map(Tags(("tourism", tourismValue)));

        result.ShouldBe(OsmCategoryMapper.Accommodation);
    }

    [Fact]
    public void Otel_ayni_anda_tarihi_bina_olsa_bile_konaklama_sayilir()
    {
        // Tarihi konakta pansiyon işletiliyorsa kullanıcıya gezilecek yer olarak gösterilmemeli
        var result = OsmCategoryMapper.Map(Tags(
            ("tourism", "guest_house"),
            ("historic", "building"),
            ("building", "yes")));

        result.ShouldBe(OsmCategoryMapper.Accommodation);
    }

    [Fact]
    public void Turizm_burosu_gizli_kategoriye_duser()
    {
        OsmCategoryMapper.Map(Tags(("tourism", "information")))
            .ShouldBe(OsmCategoryMapper.TouristInformation);
    }

    [Theory]
    [InlineData("camp_site")]
    [InlineData("caravan_site")]
    public void Kamp_alanlari_gizli_kategoriye_duser(string value)
    {
        OsmCategoryMapper.Map(Tags(("tourism", value))).ShouldBe(OsmCategoryMapper.CampSite);
    }

    // --- Tarihi yerler ---

    [Theory]
    [InlineData("castle", "castle")]
    [InlineData("fort", "castle")]
    [InlineData("city_walls", "castle")]
    [InlineData("ruins", "ruins")]
    [InlineData("archaeological_site", "archaeological_site")]
    [InlineData("monument", "monument")]
    [InlineData("memorial", "monument")]
    [InlineData("bridge", "historic_bridge")]
    [InlineData("tomb", "tomb")]
    [InlineData("caravanserai", "caravanserai")]
    [InlineData("tower", "tower")]
    [InlineData("aqueduct", "historic_building")]
    public void Tarihi_etiketler_dogru_kategoriye_eslenir(string historicValue, string expected)
    {
        OsmCategoryMapper.Map(Tags(("historic", historicValue))).ShouldBe(expected);
    }

    [Fact]
    public void Tarihi_etiket_turizm_etiketinden_oncelikli()
    {
        // Rumeli Hisarı hem "historic=castle" hem "tourism=attraction" taşır; kale kazanmalı
        var result = OsmCategoryMapper.Map(Tags(
            ("historic", "castle"),
            ("tourism", "attraction")));

        result.ShouldBe("castle");
    }

    [Fact]
    public void Historic_yes_tek_basina_anlamsizdir_yapi_tipine_bakilir()
    {
        OsmCategoryMapper.Map(Tags(("historic", "yes"), ("building", "mosque")))
            .ShouldBe("mosque");

        OsmCategoryMapper.Map(Tags(("historic", "yes"), ("building", "yes")))
            .ShouldBe("historic_building");
    }

    [Fact]
    public void Historic_yes_yapi_bilgisi_yoksa_elenir()
    {
        OsmCategoryMapper.Map(Tags(("historic", "yes"))).ShouldBeNull();
    }

    [Fact]
    public void Tarihi_bina_yapi_tipine_gore_daha_iyi_kategoriye_tasinir()
    {
        // "historic=building + building=mosque" -> tarihi bina değil, cami
        OsmCategoryMapper.Map(Tags(("historic", "building"), ("building", "mosque")))
            .ShouldBe("mosque");

        OsmCategoryMapper.Map(Tags(("historic", "building"), ("building", "castle")))
            .ShouldBe("castle");
    }

    // --- İbadet yerleri ---

    [Theory]
    [InlineData("muslim", "mosque")]
    [InlineData("christian", "church")]
    [InlineData("jewish", "synagogue")]
    public void Ibadet_yerleri_din_bilgisine_gore_ayrilir(string religion, string expected)
    {
        var result = OsmCategoryMapper.Map(Tags(
            ("amenity", "place_of_worship"),
            ("religion", religion)));

        result.ShouldBe(expected);
    }

    [Fact]
    public void Hristiyan_ibadet_yeri_manastir_ise_manastir_kategorisine_gider()
    {
        var result = OsmCategoryMapper.Map(Tags(
            ("amenity", "place_of_worship"),
            ("religion", "christian"),
            ("building", "monastery")));

        result.ShouldBe("monastery");
    }

    [Fact]
    public void Din_bilgisi_olmayan_ibadet_yeri_elenir()
    {
        OsmCategoryMapper.Map(Tags(("amenity", "place_of_worship"))).ShouldBeNull();
    }

    // --- Doğal oluşumlar ---

    [Fact]
    public void Selale_waterway_etiketinden_bulunur()
    {
        OsmCategoryMapper.Map(Tags(("waterway", "waterfall"))).ShouldBe("waterfall");
    }

    [Theory]
    [InlineData("beach", "beach")]
    [InlineData("cave_entrance", "cave")]
    [InlineData("hot_spring", "hot_spring")]
    [InlineData("valley", "valley")]
    [InlineData("peak", "viewpoint")]
    public void Dogal_olusumlar_eslenir(string naturalValue, string expected)
    {
        OsmCategoryMapper.Map(Tags(("natural", naturalValue))).ShouldBe(expected);
    }

    [Fact]
    public void Su_alani_sadece_gol_ise_kaydedilir()
    {
        OsmCategoryMapper.Map(Tags(("natural", "water"), ("water", "lake"))).ShouldBe("lake");

        // Dere, kanal, havuz turistik yer değil
        OsmCategoryMapper.Map(Tags(("natural", "water"), ("water", "river"))).ShouldBeNull();
        OsmCategoryMapper.Map(Tags(("natural", "water"))).ShouldBeNull();
    }

    [Fact]
    public void Kaynak_sadece_termal_ise_kaplica_sayilir()
    {
        OsmCategoryMapper.Map(Tags(("natural", "spring"))).ShouldBeNull();

        OsmCategoryMapper.Map(Tags(("natural", "spring"), ("bath:type", "thermal")))
            .ShouldBe("hot_spring");
    }

    [Fact]
    public void Tarihi_hamam_termal_degilse_tarihi_yapi_sayilir()
    {
        OsmCategoryMapper.Map(Tags(("amenity", "public_bath"))).ShouldBe("historic_building");

        OsmCategoryMapper.Map(Tags(("amenity", "public_bath"), ("bath:type", "thermal")))
            .ShouldBe("hot_spring");
    }

    // --- Diğer ---

    [Theory]
    [InlineData("museum", "museum")]
    [InlineData("gallery", "gallery")]
    [InlineData("viewpoint", "viewpoint")]
    [InlineData("zoo", "zoo")]
    [InlineData("aquarium", "aquarium")]
    [InlineData("theme_park", "theme_park")]
    [InlineData("attraction", "other_poi")]
    public void Turizm_etiketleri_eslenir(string tourismValue, string expected)
    {
        OsmCategoryMapper.Map(Tags(("tourism", tourismValue))).ShouldBe(expected);
    }

    [Fact]
    public void Milli_park_sinir_etiketinden_bulunur()
    {
        OsmCategoryMapper.Map(Tags(("boundary", "national_park"))).ShouldBe("national_park");
        OsmCategoryMapper.Map(Tags(("leisure", "nature_reserve"))).ShouldBe("national_park");
    }

    [Fact]
    public void Carsi_ve_pazar_bazaar_kategorisine_gider()
    {
        OsmCategoryMapper.Map(Tags(("amenity", "marketplace"))).ShouldBe("bazaar");
    }

    [Fact]
    public void Ada_place_etiketinden_bulunur()
    {
        OsmCategoryMapper.Map(Tags(("place", "island"))).ShouldBe("island");
    }

    // --- Gerçek veri taramasından sonra eklenen eşlemeler ---

    [Fact]
    public void Savas_alani_kendi_kategorisine_gider()
    {
        // Çanakkale ve Dumlupınar gibi alanlar Türkiye için önemli
        OsmCategoryMapper.Map(Tags(("historic", "battlefield"))).ShouldBe("battlefield");
    }

    [Theory]
    [InlineData("locomotive")]
    [InlineData("aircraft")]
    [InlineData("tank")]
    [InlineData("ship")]
    public void Acik_havada_sergilenen_tarihi_araclar_anit_sayilir(string historicValue)
    {
        // Müze bahçesindeki lokomotif, park içindeki uçak
        OsmCategoryMapper.Map(Tags(("historic", historicValue))).ShouldBe("monument");
    }

    [Fact]
    public void Antik_sutun_anit_sayilir()
    {
        OsmCategoryMapper.Map(Tags(("man_made", "column"))).ShouldBe("monument");
    }

    [Fact]
    public void Sulak_alan_kendi_kategorisine_gider()
    {
        // Kuş cennetleri ve deltalar
        OsmCategoryMapper.Map(Tags(("natural", "wetland"))).ShouldBe("wetland");
    }

    [Fact]
    public void Kamp_alani_parcasi_da_gizli_kategoriye_duser()
    {
        OsmCategoryMapper.Map(Tags(("tourism", "camp_pitch"))).ShouldBe(OsmCategoryMapper.CampSite);
    }

    // --- Elenmesi gerekenler ---

    [Theory]
    [InlineData("amenity", "restaurant")]
    [InlineData("amenity", "fuel")]
    [InlineData("amenity", "pharmacy")]
    [InlineData("shop", "supermarket")]
    [InlineData("highway", "bus_stop")]
    [InlineData("natural", "tree")]
    public void Turistik_olmayan_yerler_elenir(string key, string value)
    {
        OsmCategoryMapper.Map(Tags((key, value))).ShouldBeNull();
    }

    [Fact]
    public void Bos_etiket_sozlugu_null_dondurur()
    {
        OsmCategoryMapper.Map(new Dictionary<string, string>()).ShouldBeNull();
    }

    [Fact]
    public void Null_etiket_sozlugu_hata_firlatir()
    {
        Should.Throw<ArgumentNullException>(() => OsmCategoryMapper.Map(null!));
    }

    // --- Biçim toleransı ---

    [Fact]
    public void Etiket_degerleri_buyuk_harf_ve_bosluklu_gelse_de_eslenir()
    {
        OsmCategoryMapper.Map(Tags(("historic", " Castle "))).ShouldBe("castle");
        OsmCategoryMapper.Map(Tags(("tourism", "MUSEUM"))).ShouldBe("museum");
    }

    [Fact]
    public void Bos_deger_tasiyan_etiket_yok_sayilir()
    {
        OsmCategoryMapper.Map(Tags(("historic", "   "), ("tourism", "museum")))
            .ShouldBe("museum");
    }

    private static Dictionary<string, string> Tags(params (string Key, string Value)[] pairs) =>
        pairs.ToDictionary(p => p.Key, p => p.Value);
}
