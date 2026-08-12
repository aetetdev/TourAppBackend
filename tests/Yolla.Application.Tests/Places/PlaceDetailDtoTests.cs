using System.Text.Json;
using System.Text.Json.Serialization;
using Shouldly;
using Yolla.Application.Places;

namespace Yolla.Application.Tests.Places;

/// <remarks>
/// Yer detayı Redis'te JSON olarak saklanıyor. Küçültülmüş görsel adresleri hesaplanan
/// alanlar olduğu için önbellekten dönen kayıtta da doğru olmalı — aksi halde önbelleğe
/// düşen istekler eksik alanla dönerdi.
/// </remarks>
public class PlaceDetailDtoTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public void Turetilmis_gorsel_alanlari_yanitta_yer_alir()
    {
        var json = JsonSerializer.Serialize(NewDetail(), Options);

        json.ShouldContain("photoThumbUrl");
        json.ShouldContain("500px-Uchisar_Castle.jpg");
        json.ShouldContain("960px-Uchisar_Castle.jpg");
    }

    [Fact]
    public void Onbellek_gidis_donusunde_alanlar_yeniden_hesaplanir()
    {
        var json = JsonSerializer.Serialize(NewDetail(), Options);

        // Salt okunur alanlar geri okunmaz, orijinal adresten yeniden üretilir:
        // genişlik listesi değişirse önbellekteki eski kayıtlar da yeni adresi verir
        var restored = JsonSerializer.Deserialize<PlaceDetailDto>(json, Options);

        restored.ShouldNotBeNull();
        restored.PhotoThumbUrl!.ShouldContain("/500px-");
        restored.PhotoLargeUrl!.ShouldContain("/960px-");
        restored.Nearby[0].PhotoThumbUrl!.ShouldContain("/500px-");
    }

    [Fact]
    public void Fotografsiz_yerde_alanlar_null_kalir()
    {
        var detail = NewDetail() with { PhotoUrl = null };

        detail.PhotoThumbUrl.ShouldBeNull();
        detail.PhotoLargeUrl.ShouldBeNull();
    }

    private static PlaceDetailDto NewDetail() => new()
    {
        Id = 93,
        Name = "Uçhisar Kalesi",
        Slug = "uchisar-kalesi",
        CategoryKey = "castle",
        CategoryName = "Kale",
        PhotoUrl = "https://upload.wikimedia.org/wikipedia/commons/f/f0/Uchisar_Castle.jpg",
        PhotoAttribution = "Fotoğraf: Test (CC BY-SA 4.0)",
        Latitude = 38.63,
        Longitude = 34.80,
        CityName = "Nevşehir",
        CitySlug = "nevsehir",
        QualityScore = 92,
        DirectionsUrl = "https://www.google.com/maps/dir/?api=1&destination=38.63,34.80",
        Nearby =
        [
            new NearbyPlaceDto
            {
                Id = 94,
                Name = "Güvercinlik Vadisi",
                Slug = "guvercinlik-vadisi",
                CategoryName = "Vadi",
                PhotoUrl = "https://upload.wikimedia.org/wikipedia/commons/a/ab/Pigeon_Valley.jpg",
                DistanceMeters = 800
            }
        ]
    };
}
