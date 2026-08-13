using AgendadorContas.Models;

namespace AgendadorContas.Data.Entities;

public sealed class FamilySettings
{
    public Guid FamilyId { get; set; }
    public AccountCurrency DefaultCurrency { get; set; } = AccountCurrency.GBP;
    public string TimeZoneId { get; set; } = "Europe/London";
    public int ReminderHour { get; set; } = 8;
    public int ReminderMinute { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public Family Family { get; set; } = null!;
}
