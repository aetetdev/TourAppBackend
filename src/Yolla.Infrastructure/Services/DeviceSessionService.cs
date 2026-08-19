using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Yolla.Application.Auth;
using Yolla.Application.Common;
using Yolla.Domain.Entities;
using Yolla.Infrastructure.Persistence;

namespace Yolla.Infrastructure.Services;

/// <inheritdoc cref="IDeviceSessionService"/>
public sealed class DeviceSessionService(YollaDbContext context, ITokenGenerator tokenGenerator)
    : IDeviceSessionService
{
    private static readonly string[] AllowedPlatforms = ["ios", "android", "web"];

    public async Task<DeviceSessionDto> RegisterAsync(
        DeviceRegistrationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Validate(request);

        var platform = request.Platform.Trim().ToLowerInvariant();
        var language = NormalizeLanguage(request.Language);

        var device = await context.Devices
            .FirstOrDefaultAsync(x => x.DeviceUuid == request.DeviceUuid, cancellationToken);

        if (device is null)
        {
            device = new Device
            {
                DeviceUuid = request.DeviceUuid,
                Platform = platform,
                AppVersion = request.AppVersion,
                Language = language
            };

            context.Devices.Add(device);
        }
        else
        {
            // Aynı cihaz tekrar kayıt olursa yeni kayıt açılmaz, bilgileri tazelenir
            device.Platform = platform;
            device.AppVersion = request.AppVersion;
            device.Language = language;
            device.LastSeenAt = DateTimeOffset.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);

        // Cihaz bir hesaba bağlıysa o hesabın rolleri de jetona giriyor;
        // yoksa moderatör uygulamayı yeniden açtığında yetkisini kaybederdi.
        var roles = device.UserId is { } userId
            ? await context.Set<IdentityUserRole<int>>()
                .Where(x => x.UserId == userId)
                .Join(context.Roles, x => x.RoleId, r => r.Id, (_, r) => r.Name!)
                .ToListAsync(cancellationToken)
            : [];

        var (token, expiresAt) = tokenGenerator.CreateDeviceToken(
            device.Id, device.DeviceUuid, device.UserId, roles);

        return new DeviceSessionDto
        {
            DeviceId = device.Id,
            AccessToken = token,
            ExpiresAt = expiresAt,
            UserId = device.UserId
        };
    }

    private static void Validate(DeviceRegistrationRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.DeviceUuid == Guid.Empty)
        {
            errors[nameof(request.DeviceUuid)] = ["Cihaz kimliği boş olamaz."];
        }

        var platform = request.Platform?.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(platform) || !AllowedPlatforms.Contains(platform))
        {
            errors[nameof(request.Platform)] =
                [$"Platform şunlardan biri olmalı: {string.Join(", ", AllowedPlatforms)}."];
        }

        if (request.AppVersion is { Length: > 30 })
        {
            errors[nameof(request.AppVersion)] = ["Sürüm bilgisi en fazla 30 karakter olabilir."];
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }
    }

    private static string NormalizeLanguage(string? language)
    {
        var value = language?.Trim().ToLowerInvariant();

        // Desteklenmeyen dillerde Türkçe içerik gösterilir
        return value is "en" ? "en" : "tr";
    }
}
