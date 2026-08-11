using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Yolla.Application.Auth;

namespace Yolla.Infrastructure.Auth;

public static class AuthenticationSetup
{
    /// <summary>
    /// Geliştirme imza anahtarı. Üretimde <c>Jwt__Key</c> ortam değişkeniyle ezilmesi zorunlu;
    /// aksi halde uygulama açılmaz.
    /// </summary>
    internal const string DevelopmentKey = "yolla-gelistirme-imza-anahtari-en-az-32-karakter-olmali";

    private const int MinimumKeyLength = 32;

    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        string environmentName)
    {
        var options = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        if (string.IsNullOrWhiteSpace(options.Key))
        {
            options.Key = DevelopmentKey;
        }

        var isDevelopment = string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase);

        // Jeton imza anahtarı sızarsa herkes istediği cihaz adına jeton üretebilir;
        // geliştirme anahtarının üretime taşınması bu yüzden engelleniyor
        if (!isDevelopment && options.Key == DevelopmentKey)
        {
            throw new InvalidOperationException(
                $"'{environmentName}' ortamında geliştirme imza anahtarı kullanılamaz. "
                + "Jwt__Key ortam değişkenini tanımlayın.");
        }

        if (options.Key.Length < MinimumKeyLength)
        {
            throw new InvalidOperationException(
                $"Jwt imza anahtarı en az {MinimumKeyLength} karakter olmalıdır.");
        }

        services.Configure<JwtOptions>(config =>
        {
            config.Key = options.Key;
            config.Issuer = options.Issuer;
            config.Audience = options.Audience;
            config.DeviceTokenDays = options.DeviceTokenDays;
        });

        services.AddScoped<ITokenGenerator, JwtTokenGenerator>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(bearer =>
            {
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = options.Issuer,
                    ValidAudience = options.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key)),
                    // Varsayılan 5 dakikalık tolerans, süresi dolmuş jetonun kabul edilmesine yol açar
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        services.AddAuthorization();

        return services;
    }
}
