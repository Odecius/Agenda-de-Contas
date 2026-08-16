namespace AgendadorContas.DataMigration;

public interface IJsonToPostgresqlMigrator
{
    Task<MigrationReport> ImportAsync(
        string sourcePath,
        Guid targetFamilyId,
        MigrationOptions options,
        CancellationToken cancellationToken = default);
}

public sealed record MigrationOptions(bool DryRun = false);

public sealed class MigrationReport
{
    public DateTime StartedAtUtc { get; init; }
    public DateTime FinishedAtUtc { get; internal set; }
    public bool DryRun { get; init; }
    public Guid TargetFamilyId { get; init; }
    public string SourceFile { get; init; } = string.Empty;
    public int TotalContasRead { get; internal set; }
    public int ContasInserted { get; internal set; }
    public int ContasSkipped { get; internal set; }
    public int ContasInvalid { get; internal set; }
    public int TotalPagamentosRead { get; internal set; }
    public int PagamentosInserted { get; internal set; }
    public int PagamentosSkipped { get; internal set; }
    public int PagamentosInvalid { get; internal set; }
    public int DuplicatePayments { get; internal set; }
    public List<string> Warnings { get; } = [];
    public List<string> Errors { get; } = [];
    public bool DatabaseModified { get; internal set; }
    public bool Success => Errors.Count == 0;
}
