namespace AgendadorContas.Data.Entities;

public sealed class TelegramSettings
{
    public Guid FamilyId { get; set; }
    public bool IsEnabled { get; set; }
    public string? ChatId { get; set; }
    public string? BotTokenSecretReference { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public Family Family { get; set; } = null!;
}
