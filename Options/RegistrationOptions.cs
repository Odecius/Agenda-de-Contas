using System.ComponentModel.DataAnnotations;

namespace AgendadorContas.Options;

public sealed class RegistrationOptions
{
    public const string SectionName = "Registration";

    public bool Enabled { get; set; }

    [Range(1, 20)]
    public int AttemptsPerHour { get; set; } = 5;
}
