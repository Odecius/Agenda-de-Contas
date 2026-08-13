using System.ComponentModel.DataAnnotations;

namespace AgendadorContas.Options;

public sealed class MultiFamilyOptions
{
    public const string SectionName = "MultiFamily";

    public bool Enabled { get; set; }

    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    [Range(1, 168)]
    public int SessionHours { get; set; } = 8;
}
