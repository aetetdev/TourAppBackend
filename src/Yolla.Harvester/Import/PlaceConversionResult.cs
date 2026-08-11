namespace Yolla.Harvester.Import;

/// <summary>Bir OSM kaydının neden içe aktarılmadığı.</summary>
public enum PlaceSkipReason
{
    /// <summary>Elenmedi, kayıt aktarılabilir.</summary>
    None = 0,

    /// <summary>Etiketleri hiçbir Yolla kategorisine karşılık gelmiyor (restoran, benzinlik...).</summary>
    NoCategory,

    /// <summary>Otel, turizm bürosu, kamp alanı gibi kullanıcıya gösterilmeyen bir tür.</summary>
    HiddenCategory,

    /// <summary>Adı yok; kart olarak gösterilemez.</summary>
    NoName,

    /// <summary>Adından URL parçası üretilemedi.</summary>
    NoSlug
}

/// <summary>Dönüşüm sonucu: ya aktarılabilir satır ya da eleme sebebi.</summary>
public readonly record struct PlaceConversionResult
{
    private PlaceConversionResult(PlaceImportRow? row, PlaceSkipReason skipReason)
    {
        Row = row;
        SkipReason = skipReason;
    }

    public PlaceImportRow? Row { get; }

    public PlaceSkipReason SkipReason { get; }

    public bool IsConverted => Row is not null;

    public static PlaceConversionResult Converted(PlaceImportRow row) =>
        new(row, PlaceSkipReason.None);

    public static PlaceConversionResult Skipped(PlaceSkipReason reason) =>
        new(null, reason);
}
