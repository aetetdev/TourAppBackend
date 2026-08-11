using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Yolla.Application.Auth;

namespace Yolla.Infrastructure.Auth;

/// <inheritdoc cref="ITokenGenerator"/>
public sealed class JwtTokenGenerator(IOptions<JwtOptions> options) : ITokenGenerator
{
    /// <summary>Cihaz kimliğini taşıyan özel talep adı.</summary>
    public const string DeviceIdClaim = "device_id";

    /// <summary>Cihazın istemci tarafındaki kalıcı kimliği.</summary>
    public const string DeviceUuidClaim = "device_uuid";

    private readonly JwtOptions _options = options.Value;

    public (string Token, DateTimeOffset ExpiresAt) CreateDeviceToken(
        int deviceId,
        Guid deviceUuid,
        int? userId)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddDays(_options.DeviceTokenDays);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(DeviceIdClaim, deviceId.ToString()),
            new(DeviceUuidClaim, deviceUuid.ToString())
        };

        // Cihaz bir hesaba bağlandıysa kullanıcı kimliği de taşınır
        if (userId is { } id)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, id.ToString()));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
