namespace Yolla.Infrastructure.Routing;

/// <summary>Rota motoru adresleri.</summary>
/// <remarks>
/// Kendi altyapımızda çalışan OSRM örnekleri. Genel demo sunucusu
/// (router.project-osrm.org) kullanılmıyor: kullanım şartları üretim kullanımını
/// yasaklıyor ve hız sınırı uyguluyor.
/// </remarks>
public sealed class OsrmOptions
{
    public const string SectionName = "Osrm";

    /// <summary>Araç profili; şehirlerarası rota için.</summary>
    public string CarBaseUrl { get; set; } = "http://localhost:5000";

    /// <summary>Yürüme profili; şehir içi gezi için.</summary>
    public string FootBaseUrl { get; set; } = "http://localhost:5001";

    /// <summary>Tek istekte gönderilebilecek en fazla durak sayısı.</summary>
    /// <remarks>
    /// Gezgin satıcı çözümünün maliyeti durak sayısıyla hızla artar; sınır hem sunucuyu
    /// hem yanıt süresini korur. Bir günlük gezide 25 durak zaten fazlasıyla yeterli.
    /// </remarks>
    public int MaxWaypoints { get; set; } = 25;

    public int TimeoutSeconds { get; set; } = 30;
}
