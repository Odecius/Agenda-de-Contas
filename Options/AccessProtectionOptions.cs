using System.ComponentModel.DataAnnotations;

namespace AgendadorContas.Options;

public sealed class AccessProtectionOptions
{
    public const string SectionName = "AccessProtection";

    public bool Enabled { get; set; }

    [Required]
    public string Username { get; set; } = "admin";

    public string Password { get; set; } = string.Empty;

    public int SessionHours { get; set; } = 12;
}
