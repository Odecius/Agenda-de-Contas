namespace AgendadorContas.Data.Entities;

public enum FamilyRole { Owner, Admin, Member }

public sealed class FamilyUser
{
    public Guid FamilyId { get; set; }
    public Guid UserId { get; set; }
    public FamilyRole Role { get; set; } = FamilyRole.Member;
    public bool IsActive { get; set; } = true;
    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;
    public Family Family { get; set; } = null!;
    public AppUser User { get; set; } = null!;
}
