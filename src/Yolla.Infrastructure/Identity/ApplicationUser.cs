using Microsoft.AspNetCore.Identity;

namespace Yolla.Infrastructure.Identity;

// Identity kullanıcısı. Domain katmanı Identity'ye bağımlı olmasın diye burada duruyor;
// Domain tarafındaki entity'ler kullanıcıya sadece UserId (int?) ile bakar.
public class ApplicationUser : IdentityUser<int>
{
    public string? DisplayName { get; set; }

    public string Language { get; set; } = "tr";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
