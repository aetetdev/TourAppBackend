using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
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
// İzin verilen adresler yapılandırmadan gelir; joker karakter kullanılmaz.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        }
        else
        {
            // Geliştirme ortamında kimlik bilgisi taşımayan isteklere izin verilir
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
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

/// <summary>Entegrasyon testlerinin uygulamayı ayağa kaldırabilmesi için görünür kılınır.</summary>
public partial class Program;
