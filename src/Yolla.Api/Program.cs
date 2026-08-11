using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Scalar.AspNetCore;
using Serilog;
using Yolla.Api.Middleware;
using Yolla.Infrastructure;
using Yolla.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration));

// Identity token sağlayıcıları (şifre sıfırlama, e-posta doğrulama) DataProtection'a bağlı
builder.Services.AddDataProtection();

// Postgres/PostGIS, Identity, Redis, uygulama servisleri
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<YollaDbContext>();

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
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

/// <summary>Entegrasyon testlerinin uygulamayı ayağa kaldırabilmesi için görünür kılınır.</summary>
public partial class Program;
