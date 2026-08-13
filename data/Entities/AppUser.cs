using Microsoft.AspNetCore.Identity;

namespace AgendadorContas.Data.Entities;

public sealed class AppUser : IdentityUser<Guid>
{
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<FamilyUser> Families { get; set; } = [];
}
