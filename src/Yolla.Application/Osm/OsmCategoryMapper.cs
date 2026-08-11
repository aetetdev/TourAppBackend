namespace Yolla.Application.Osm;

// Ham OSM etiketlerini Yolla kategori anahtarlarına çevirir.
//
// OSM'de turistik yer diye tek bir etiket yok: bir kale "historic=castle", bir şelale
// "waterway=waterfall", bir cami "amenity=place_of_worship + religion=muslim" ile işaretlenir.
// Üstelik "tourism=*" etiketinin altında oteller ve turizm büroları da bulunur - eski veri
// toplayıcının en büyük sorunu buydu, 100 bin kaydın büyük kısmı pansiyondu.
//
// Kural sırası özellikle önemli: bir yer hem "historic=castle" hem "tourism=attraction"
// taşıyabilir; daha spesifik olan (historic) kazanır.
public static class OsmCategoryMapper
{
    // Gizli kategoriler - veritabanında tutulur, kart destesinde gösterilmez
    public const string Accommodation = "accommodation";
    public const string TouristInformation = "tourist_information";
    public const string CampSite = "camp_site";

    private static readonly HashSet<string> AccommodationValues =
    [
        "hotel", "hostel", "guest_house", "motel", "apartment", "chalet",
        "alpine_hut", "wilderness_hut", "apartments", "love_hotel"
    ];

    private static readonly Dictionary<string, string> HistoricMap = new()
    {
        ["castle"] = "castle",
        ["fort"] = "castle",
        ["fortification"] = "castle",
        ["city_gate"] = "castle",
        ["city_walls"] = "castle",
        ["ruins"] = "ruins",
        ["archaeological_site"] = "archaeological_site",
        ["monument"] = "monument",
        ["memorial"] = "monument",
        ["obelisk"] = "monument",
        ["building"] = "historic_building",
        ["manor"] = "historic_building",
        ["palace"] = "historic_building",
        ["farm"] = "historic_building",
        ["house"] = "historic_building",
        ["baths"] = "historic_building",
        ["aqueduct"] = "historic_building",
        ["bridge"] = "historic_bridge",
        ["tomb"] = "tomb",
        ["mausoleum"] = "tomb",
        ["cemetery"] = "tomb",
        ["caravanserai"] = "caravanserai",
        ["tower"] = "tower",
        ["lighthouse"] = "tower",
        ["church"] = "church",
        ["monastery"] = "monastery",
        ["mosque"] = "mosque"
    };

    private static readonly Dictionary<string, string> TourismMap = new()
    {
        ["museum"] = "museum",
        ["gallery"] = "gallery",
        ["artwork"] = "artwork",
        ["viewpoint"] = "viewpoint",
        ["zoo"] = "zoo",
        ["aquarium"] = "aquarium",
        ["theme_park"] = "theme_park",
        ["picnic_site"] = "picnic_site",
        ["attraction"] = "other_poi"
    };

    private static readonly Dictionary<string, string> NaturalMap = new()
    {
        ["beach"] = "beach",
        ["cave_entrance"] = "cave",
        ["hot_spring"] = "hot_spring",
        ["valley"] = "valley",
        ["volcano"] = "valley",
        ["peak"] = "viewpoint",
        ["cliff"] = "viewpoint",
        ["arch"] = "viewpoint"
    };

    private static readonly Dictionary<string, string> LeisureMap = new()
    {
        ["nature_reserve"] = "national_park",
        ["water_park"] = "water_park",
        ["beach_resort"] = "beach",
        ["garden"] = "park",
        ["park"] = "park"
    };

    private static readonly Dictionary<string, string> ManMadeMap = new()
    {
        ["lighthouse"] = "tower",
        ["tower"] = "tower",
        ["obelisk"] = "monument",
        ["watermill"] = "historic_building",
        ["windmill"] = "historic_building"
    };

    /// <summary>
    /// Etiketleri bir Yolla kategori anahtarına eşler.
    /// </summary>
    /// <returns>
    /// Kategori anahtarı; yer Yolla için tamamen ilgisizse (restoran, benzinlik, market) <c>null</c>.
    /// </returns>
    public static string? Map(IReadOnlyDictionary<string, string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        // 1. Konaklama ve hizmet noktaları: kaydedilir ama gizli kategoriye düşer.
        //    Sıra en başta, çünkü bir otel aynı zamanda "tourism=hotel + historic=building" olabilir.
        if (TryGet(tags, "tourism", out var tourism))
        {
            if (AccommodationValues.Contains(tourism))
            {
                return Accommodation;
            }

            if (tourism is "information")
            {
                return TouristInformation;
            }

            if (tourism is "camp_site" or "caravan_site")
            {
                return CampSite;
            }
        }

        // 2. Tarihi etiketler en spesifik bilgiyi taşır
        if (TryGet(tags, "historic", out var historic))
        {
            if (HistoricMap.TryGetValue(historic, out var historicCategory))
            {
                return Refine(historicCategory, tags);
            }

            // historic=yes gibi belirsiz değerlerde yapı tipine bakılır
            if (historic is "yes")
            {
                return MapHistoricYes(tags);
            }
        }

        // 3. İbadet yerleri - din bilgisi kategoriyi belirler
        if (TryGet(tags, "amenity", out var amenity))
        {
            switch (amenity)
            {
                case "place_of_worship":
                    return MapPlaceOfWorship(tags);
                case "theatre":
                    return "theatre";
                case "arts_centre":
                    return "gallery";
                case "marketplace":
                    return "bazaar";
                case "public_bath":
                    return IsThermal(tags) ? "hot_spring" : "historic_building";
                case "monastery":
                    return "monastery";
            }
        }

        // 4. Turizm etiketleri
        if (tourism is not null && TourismMap.TryGetValue(tourism, out var tourismCategory))
        {
            return tourismCategory;
        }

        // 5. Doğal oluşumlar
        if (TryGet(tags, "waterway", out var waterway) && waterway is "waterfall")
        {
            return "waterfall";
        }

        if (TryGet(tags, "natural", out var natural))
        {
            if (NaturalMap.TryGetValue(natural, out var naturalCategory))
            {
                return naturalCategory;
            }

            if (natural is "water")
            {
                return TryGet(tags, "water", out var water) && water is "lake" or "reservoir" or "lagoon"
                    ? "lake"
                    : null;
            }

            if (natural is "spring")
            {
                return IsThermal(tags) ? "hot_spring" : null;
            }
        }

        // 6. Korunan alanlar
        if (TryGet(tags, "boundary", out var boundary) && boundary is "national_park" or "protected_area")
        {
            return "national_park";
        }

        if (TryGet(tags, "place", out var place) && place is "island" or "islet" or "archipelago")
        {
            return "island";
        }

        // 7. Rekreasyon alanları
        if (TryGet(tags, "leisure", out var leisure) && LeisureMap.TryGetValue(leisure, out var leisureCategory))
        {
            return leisureCategory;
        }

        // 8. İnsan yapımı işaret noktaları
        if (TryGet(tags, "man_made", out var manMade) && ManMadeMap.TryGetValue(manMade, out var manMadeCategory))
        {
            return manMadeCategory;
        }

        // 9. Dini yapı tipleri (amenity olmadan sadece building ile işaretlenmiş olabilir)
        if (TryGet(tags, "building", out var building))
        {
            switch (building)
            {
                case "mosque":
                    return "mosque";
                case "church":
                case "cathedral":
                case "chapel":
                    return "church";
                case "synagogue":
                    return "synagogue";
                case "monastery":
                    return "monastery";
                case "castle":
                case "palace":
                    return "castle";
            }
        }

        return null;
    }

    // Kilise/manastır ayrımı ve cami tespiti din etiketlerinden yapılır
    private static string? MapPlaceOfWorship(IReadOnlyDictionary<string, string> tags)
    {
        if (!TryGet(tags, "religion", out var religion))
        {
            return null;
        }

        return religion switch
        {
            "muslim" => "mosque",
            "christian" => IsMonastery(tags) ? "monastery" : "church",
            "jewish" => "synagogue",
            _ => null
        };
    }

    // historic=yes tek başına anlamsız; yanındaki yapı etiketine bakılır
    private static string? MapHistoricYes(IReadOnlyDictionary<string, string> tags)
    {
        if (TryGet(tags, "building", out var building))
        {
            return building switch
            {
                "mosque" => "mosque",
                "church" or "cathedral" or "chapel" => "church",
                "synagogue" => "synagogue",
                "castle" or "palace" => "castle",
                "yes" => "historic_building",
                _ => "historic_building"
            };
        }

        if (TryGet(tags, "amenity", out var amenity) && amenity is "place_of_worship")
        {
            return MapPlaceOfWorship(tags);
        }

        return null;
    }

    // Hristiyan ibadet yerlerinde manastır ayrımı
    private static bool IsMonastery(IReadOnlyDictionary<string, string> tags) =>
        (TryGet(tags, "building", out var building) && building is "monastery")
        || (TryGet(tags, "amenity", out var amenity) && amenity is "monastery")
        || (TryGet(tags, "historic", out var historic) && historic is "monastery");

    // Kaplıca tespiti: bath:type=thermal ya da doğrudan sıcak su etiketi
    private static bool IsThermal(IReadOnlyDictionary<string, string> tags) =>
        (TryGet(tags, "bath:type", out var bathType) && bathType.Contains("thermal", StringComparison.OrdinalIgnoreCase))
        || (TryGet(tags, "natural", out var natural) && natural is "hot_spring")
        || tags.ContainsKey("hot_spring");

    // Bazı tarihi etiketler daha iyi bir kategoriye taşınabilir:
    // "historic=building + building=mosque" -> cami
    private static string Refine(string category, IReadOnlyDictionary<string, string> tags)
    {
        if (category is not "historic_building")
        {
            return category;
        }

        if (TryGet(tags, "building", out var building))
        {
            return building switch
            {
                "mosque" => "mosque",
                "church" or "cathedral" or "chapel" => "church",
                "synagogue" => "synagogue",
                "castle" or "palace" => "castle",
                _ => category
            };
        }

        return category;
    }

    private static bool TryGet(IReadOnlyDictionary<string, string> tags, string key, out string value)
    {
        if (tags.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(raw))
        {
            value = raw.Trim().ToLowerInvariant();
            return true;
        }

        value = string.Empty;
        return false;
    }
}
