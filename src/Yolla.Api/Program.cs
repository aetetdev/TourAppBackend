using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using Yolla.Infrastructure;
using Yolla.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration));

// Identity token sağlayıcıları (şifre sıfırlama, e-posta doğrulama) DataProtection'a bağlı
builder.Services.AddDataProtection();

// Postgres/PostGIS, Identity, Redis
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<YollaDbContext>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
