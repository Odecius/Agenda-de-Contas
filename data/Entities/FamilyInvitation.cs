namespace AgendadorContas.Data.Entities;

public sealed class FamilyInvitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FamilyId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public FamilyRole Role { get; set; } = FamilyRole.Member;
    public string TokenHash { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? AcceptedAtUtc { get; set; }
    public Guid? AcceptedByUserId { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public Family Family { get; set; } = null!;
    public AppUser CreatedByUser { get; set; } = null!;
    public AppUser? AcceptedByUser { get; set; }
}
