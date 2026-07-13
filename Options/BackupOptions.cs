using System.ComponentModel.DataAnnotations;

namespace AgendadorContas.Options;

public sealed class BackupOptions
{
    public const string SectionName = "Backup";

    public bool AutomaticEnabled { get; set; }

    [Range(0, 23)]
    public int Hour { get; set; } = 2;

    [Range(0, 59)]
    public int Minute { get; set; }

    public string TimeZoneId { get; set; } = "Europe/London";

    [Range(1, 3650)]
    public int RetentionDays { get; set; } = 30;

    [Range(1, 500)]
    public int MinimumBackupsToKeep { get; set; } = 10;

    public bool RunOnStartup { get; set; }
}
