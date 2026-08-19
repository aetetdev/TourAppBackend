using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.FileProviders;
using Scalar.AspNetCore;
using Serilog;
using Yolla.Api.Middleware;
using Yolla.Infrastructure;
using Yolla.Infrastructure.Caching;
using Yolla.Infrastructure.HealthChecks;
using Yolla.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration));

// Identity token sağlayıcıları (şifre sıfırlama, e-posta doğrulama) DataProtection'a bağlı
builder.Services.AddDataProtection();

// Postgres/PostGIS, Identity, Redis, kimlik doğrulama, uygulama servisleri
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Enum'lar sayı değil metin olarak taşınır: "Like", "Pass", "City".
        // Sayısal değerler istemcide anlamsız ve sıralama değişirse sessizce bozulur.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddOpenApi();

// Sağlık kontrolü tüm bağımlılıkları kapsar. Veritabanı çökerse "sağlıksız" (trafik
// alınmamalı), Redis veya rota motoru çökerse "uyarı" (ürün kısıtlı çalışır).
var healthChecks = builder.Services.AddHealthChecks()
    .AddDbContextCheck<YollaDbContext>("veritabani");

healthChecks.AddCheck<OsrmHealthCheck>("rota-motoru", tags: ["hazir"]);

if (!string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("Redis")))
{
    healthChecks.AddCheck<RedisHealthCheck>("onbellek", tags: ["hazir"]);
}

builder.Services.AddHttpClient<OsrmHealthCheck>(client =>
    client.Timeout = TimeSpan.FromSeconds(5));

// --- CORS ---
// Web istemcisi tarayıcıda çalıştığı için bu ayar olmadan hiçbir istek geçmez.
// İzin verilen adresler yapılandırmadan gelir; joker karakter kullanılmaz.
var allowedOrigins = (builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    // Tanımsız ortam değişkeni boş dizge olarak geliyor; süzülmezse "izin verilen adres
    // var" sanılır ve hiçbir istek geçmez
    .Where(x => !string.IsNullOrWhiteSpace(x))
    // Tarayıcı Origin başlığını sondaki eğik çizgi olmadan gönderir; adresin sonunda
    // kalan bir "/" eşleşmeyi sessizce bozar
    .Select(x => x.Trim().TrimEnd('/'))
    .ToArray();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader()
            .AllowAnyMethod()
            // Ön kontrol (preflight) yanıtı bir saat önbelleklenir: her istekten önce
            // ikinci bir gidiş dönüş yapmak mobil bağlantıda gözle görülür gecikme
            .SetPreflightMaxAge(TimeSpan.FromHours(1));

        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins).AllowCredentials();
            return;
        }

        if (builder.Environment.IsDevelopment())
        {
            // Geliştirmede web istemcisinin portu her çalıştırmada değişiyor
            // (`flutter run -d chrome` rastgele port seçer). Adresleri tek tek yazmak
            // yerine tüm yerel adreslere izin veriliyor; dışarıya açık değil.
            policy.SetIsOriginAllowed(IsLocalOrigin).AllowCredentials();
            return;
        }

        // Üretimde yapılandırma eksikse hiçbir kaynağa izin verilmez. Eskiden burada
        // tüm kaynaklara açılıyordu; eksik yapılandırmanın sessizce en gevşek ayara
        // düşmesi yanlış yönde bir varsayılan.
    });
});

// --- Hız sınırlama ---
// Rota motoru ve veritabanı sorguları maliyetli; tek istemcinin sistemi doldurmasını engeller.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            // İstemci kimliği: kimlik doğrulanmışsa kullanıcı, değilse IP adresi
            partitionKey: context.User.Identity?.Name
                          ?? context.Connection.RemoteIpAddress?.ToString()
                          ?? "bilinmeyen",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // Rota hesaplama ayrı ve daha dar sınırda: her istek OSRM'e iş yüklüyor
    options.AddFixedWindowLimiter("routing", limiter =>
    {
        limiter.PermitLimit = 20;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });

    // Kimlik doğrulama en dar sınırda: şifre deneme saldırılarını yavaşlatır
    options.AddFixedWindowLimiter("auth", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(5);
        limiter.QueueLimit = 0;
    });
});

var app = builder.Build();

// Eksik CORS ayarı tarayıcıda anlaşılması güç hatalara yol açıyor; başlangıçta söylenir
if (allowedOrigins.Length == 0 && !app.Environment.IsDevelopment())
{
    app.Logger.LogWarning(
        "Cors:AllowedOrigins tanımlı değil. Web istemcisi API'ye erişemez. " +
        "Cors__AllowedOrigins__0 ortam değişkeniyle web adresi verilmeli.");
}

// Hata yönetimi en dışta: sonraki katmanların hatalarını da yakalamalı
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.WithTitle("Yolla API"));
}

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();

// Redis varsa sunucular arası ortak sayaç devreye girer; kimlik doğrulamadan sonra
// çalışır ki istemci IP yerine cihaz kimliğiyle ayrıştırılabilsin
if (app.Services.GetService<RedisRateLimiter>() is not null)
{
    app.UseMiddleware<RateLimitingMiddleware>();
}

app.UseAuthorization();

// Moderasyon sayfası (wwwroot/moderasyon.html).
//
// Sayfanın kendisi herkese açık; işe yarar hale gelmesi için moderatör
// rolündeki bir hesapla giriş yapılması gerekiyor. Yetki kontrolü uçlarda,
// dosyada değil — statik bir sayfayı gizlemek güvenlik sağlamaz.
app.UseStaticFiles();

// Kullanıcıların gönderdiği fotoğraflar.
//
// Kimlik doğrulaması aranmıyor: onaylanan fotoğraf zaten yerin kartında
// herkese görünüyor, adresi de tahmin edilemeyen bir GUID.
var photoRoot = builder.Configuration["Storage:PhotoRoot"]
                ?? Path.Combine(AppContext.BaseDirectory, "uploads");

Directory.CreateDirectory(photoRoot);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.GetFullPath(photoRoot)),
    RequestPath = "/uploads",
    // Dosyalar hiç değişmiyor (her gönderi yeni GUID); uzun önbellek güvenli.
    OnPrepareResponse = ctx =>
        ctx.Context.Response.Headers.CacheControl = "public,max-age=604800"
});

app.MapControllers();

// Yük dengeleyici için: veritabanı çalışıyorsa örnek trafik alabilir
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = check => !check.Tags.Contains("hazir")
});

// İzleme için: bağımlılıkların tümünü ayrıntılı gösterir
app.MapHealthChecks("/health/detay", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(new
        {
            durum = report.Status.ToString(),
            sureMs = report.TotalDuration.TotalMilliseconds,
            kontroller = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new { durum = entry.Value.Status.ToString(), aciklama = entry.Value.Description })
        });
    }
});

app.Run();

// Adresin yerel makineye ait olup olmadığı; yalnızca geliştirmede kullanılır.
static bool IsLocalOrigin(string origin) =>
    Uri.TryCreate(origin, UriKind.Absolute, out var uri)
    && (uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase));

/// <summary>Entegrasyon testlerinin uygulamayı ayağa kaldırabilmesi için görünür kılınır.</summary>
public partial class Program;
