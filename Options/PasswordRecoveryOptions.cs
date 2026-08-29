using System.ComponentModel.DataAnnotations;

namespace AgendadorContas.Options;

public sealed class PasswordRecoveryOptions
{
    public const string SectionName = "PasswordRecovery";
    public bool Enabled { get; set; } = true;
    [Required, Url] public string PublicBaseUrl { get; set; } = "https://localhost";
    [Range(5, 1440)] public int TokenLifespanMinutes { get; set; } = 60;
}
