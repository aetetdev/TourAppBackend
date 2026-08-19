using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Yolla.Application.Auth;
using Yolla.Application.Common;
using Yolla.Domain.Entities;
using Yolla.Infrastructure.Identity;
using Yolla.Infrastructure.Persistence;

namespace Yolla.Infrastructure.Services;

/// <inheritdoc cref="IAccountService"/>
public sealed class AccountService(
    YollaDbContext context,
    UserManager<ApplicationUser> userManager,
    ITokenGenerator tokenGenerator) : IAccountService
{
    private const int MinPasswordLength = 8;

    public async Task<AuthResultDto> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        ValidateCredentials(request.Email, request.Password);

        var email = request.Email.Trim().ToLowerInvariant();

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            throw RequestValidationException.Single(
                nameof(request.Email), "Bu e-posta ile bir hesap zaten var.");
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = request.DisplayName?.Trim()
        };

        var result = await userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            throw ToValidationException(result);
        }

        // Kayıt öncesi kullanılan cihaz hesaba bağlanır; planlar ve kaydırmalar korunur
        var device = await AttachDeviceAsync(user.Id, request.DeviceUuid, cancellationToken);

        return await BuildResultAsync(user, device, cancellationToken);
    }

    public async Task<AuthResultDto> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var email = request.Email?.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw InvalidCredentials();
        }

        var user = await userManager.FindByEmailAsync(email);

        // Kullanıcı yoksa da şifre doğrulaması yapılır: yanıt süresinden hesabın
        // var olup olmadığı anlaşılmasın
        if (user is null)
        {
            await userManager.CheckPasswordAsync(new ApplicationUser { PasswordHash = null }, request.Password);

            throw InvalidCredentials();
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            throw InvalidCredentials();
        }

        var device = await AttachDeviceAsync(user.Id, request.DeviceUuid, cancellationToken);

        return await BuildResultAsync(user, device, cancellationToken);
    }

    public async Task<AccountDto> GetAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await context.Users
                       .AsNoTracking()
                       .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
                   ?? throw new NotFoundException("Hesap", userId);

        return await BuildAccountAsync(user, cancellationToken);
    }

    public async Task ChangePasswordAsync(
        int userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await userManager.FindByIdAsync(userId.ToString())
                   ?? throw new NotFoundException("Hesap", userId);

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < MinPasswordLength)
        {
            throw RequestValidationException.Single(
                nameof(request.NewPassword), $"Şifre en az {MinPasswordLength} karakter olmalı.");
        }

        var result = await userManager.ChangePasswordAsync(
            user, request.CurrentPassword, request.NewPassword);

        if (!result.Succeeded)
        {
            throw ToValidationException(result);
        }
    }

    public async Task DeleteAccountAsync(
        int userId,
        DeleteAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await userManager.FindByIdAsync(userId.ToString())
                   ?? throw new NotFoundException("Hesap", userId);

        // Silme geri alınamaz; şifre doğrulaması olmadan yapılamaz
        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            throw RequestValidationException.Single(
                nameof(request.Password), "Şifre doğrulanamadı.");
        }

        var devices = await context.Devices
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);

        var deviceIds = devices.Select(x => x.Id).ToList();

        // Kişisel verinin tamamı siliniyor: planlar, duraklar, kaydırmalar, cihaz kayıtları.
        // Cihazlar silindiğinde planlar ve kaydırmalar da veritabanı kuralıyla düşüyor.
        var trips = await context.Trips
            .Where(x => deviceIds.Contains(x.DeviceId) || x.UserId == userId)
            .ToListAsync(cancellationToken);

        context.Trips.RemoveRange(trips);
        context.Devices.RemoveRange(devices);

        await context.SaveChangesAsync(cancellationToken);

        var result = await userManager.DeleteAsync(user);

        if (!result.Succeeded)
        {
            throw ToValidationException(result);
        }
    }

    /// <summary>
    /// Cihazı hesaba bağlar; cihaz kaydı yoksa oluşturur.
    /// </summary>
    private async Task<Device> AttachDeviceAsync(
        int userId,
        Guid? deviceUuid,
        CancellationToken cancellationToken)
    {
        Device? device = null;

        if (deviceUuid is { } uuid && uuid != Guid.Empty)
        {
            device = await context.Devices.FirstOrDefaultAsync(
                x => x.DeviceUuid == uuid, cancellationToken);
        }

        if (device is null)
        {
            device = new Device
            {
                DeviceUuid = deviceUuid ?? Guid.NewGuid(),
                Platform = "unknown",
                UserId = userId
            };

            context.Devices.Add(device);
        }
        else
        {
            device.UserId = userId;
            device.LastSeenAt = DateTimeOffset.UtcNow;
        }

        // Cihazın önceki planları da hesaba bağlanır
        var trips = await context.Trips
            .Where(x => x.DeviceId == device.Id && x.UserId == null)
            .ToListAsync(cancellationToken);

        foreach (var trip in trips)
        {
            trip.UserId = userId;
        }

        await context.SaveChangesAsync(cancellationToken);

        return device;
    }

    private async Task<AuthResultDto> BuildResultAsync(
        ApplicationUser user,
        Device device,
        CancellationToken cancellationToken)
    {
        // Roller jetona yazılıyor; moderasyon uçları buna bakıyor.
        var roles = await userManager.GetRolesAsync(user);

        var (token, expiresAt) = tokenGenerator.CreateDeviceToken(
            device.Id, device.DeviceUuid, user.Id, roles);

        return new AuthResultDto
        {
            Account = await BuildAccountAsync(user, cancellationToken),
            AccessToken = token,
            ExpiresAt = expiresAt,
            DeviceId = device.Id
        };
    }

    private async Task<AccountDto> BuildAccountAsync(
        ApplicationUser user,
        CancellationToken cancellationToken) => new()
    {
        UserId = user.Id,
        Email = user.Email ?? string.Empty,
        DisplayName = user.DisplayName,
        CreatedAt = user.CreatedAt,
        DeviceCount = await context.Devices.CountAsync(x => x.UserId == user.Id, cancellationToken),
        TripCount = await context.Trips.CountAsync(x => x.UserId == user.Id, cancellationToken)
    };

    private static void ValidateCredentials(string? email, string? password)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(email) || !IsValidEmail(email))
        {
            errors["Email"] = ["Geçerli bir e-posta adresi gerekli."];
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < MinPasswordLength)
        {
            errors["Password"] = [$"Şifre en az {MinPasswordLength} karakter olmalı."];
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }
    }

    private static bool IsValidEmail(string email)
    {
        var atIndex = email.IndexOf('@');

        return atIndex > 0
               && atIndex < email.Length - 1
               && email.IndexOf('.', atIndex) > atIndex + 1
               && !email.Contains(' ');
    }

    /// <summary>
    /// Giriş hatalarında e-postanın kayıtlı olup olmadığı belli edilmez;
    /// aksi halde hesap listesi çıkarılabilir.
    /// </summary>
    private static RequestValidationException InvalidCredentials() =>
        RequestValidationException.Single("credentials", "E-posta veya şifre hatalı.");

    private static RequestValidationException ToValidationException(IdentityResult result)
    {
        var errors = result.Errors
            .GroupBy(x => MapErrorCode(x.Code))
            .ToDictionary(g => g.Key, g => g.Select(x => Translate(x)).ToArray());

        return new RequestValidationException(errors);
    }

    private static string MapErrorCode(string code) => code switch
    {
        var c when c.Contains("Password", StringComparison.Ordinal) => "Password",
        var c when c.Contains("Email", StringComparison.Ordinal) => "Email",
        var c when c.Contains("UserName", StringComparison.Ordinal) => "Email",
        _ => "Account"
    };

    private static string Translate(IdentityError error) => error.Code switch
    {
        "PasswordTooShort" => $"Şifre en az {MinPasswordLength} karakter olmalı.",
        "PasswordRequiresDigit" => "Şifre en az bir rakam içermeli.",
        "PasswordRequiresLower" => "Şifre en az bir küçük harf içermeli.",
        "PasswordRequiresUpper" => "Şifre en az bir büyük harf içermeli.",
        "PasswordRequiresNonAlphanumeric" => "Şifre en az bir özel karakter içermeli.",
        "PasswordMismatch" => "Mevcut şifre hatalı.",
        "DuplicateEmail" or "DuplicateUserName" => "Bu e-posta ile bir hesap zaten var.",
        "InvalidEmail" => "Geçerli bir e-posta adresi gerekli.",
        _ => error.Description
    };
}
