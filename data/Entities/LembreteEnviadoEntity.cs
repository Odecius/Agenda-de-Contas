namespace AgendadorContas.Data.Entities;

public sealed class LembreteEnviadoEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FamilyId { get; set; }
    public DateOnly LocalDate { get; set; }
    public string Channel { get; set; } = "Telegram";
    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
    public Family Family { get; set; } = null!;
}
