namespace AgendadorContas.Data.Entities;

public sealed class Family
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public FamilySettings? Settings { get; set; }
    public TelegramSettings? TelegramSettings { get; set; }
    public ICollection<FamilyUser> Users { get; set; } = [];
    public ICollection<ContaEntity> Contas { get; set; } = [];
}
