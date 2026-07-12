namespace AgendadorContas.Models;

public sealed class BackupInfo
{
    public required string FileName { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public required long SizeBytes { get; init; }
}
