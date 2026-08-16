using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgendadorContas.Data;
using AgendadorContas.Data.Entities;
using AgendadorContas.Models;
using Microsoft.EntityFrameworkCore;

namespace AgendadorContas.DataMigration;

public sealed class JsonToPostgresqlMigrator(
    AgendadorDbContext db,
    ILogger<JsonToPostgresqlMigrator> logger) : IJsonToPostgresqlMigrator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<MigrationReport> ImportAsync(
        string sourcePath,
        Guid targetFamilyId,
        MigrationOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        if (targetFamilyId == Guid.Empty)
        {
            throw new ArgumentException("A target family is required.", nameof(targetFamilyId));
        }

        var report = new MigrationReport
        {
            StartedAtUtc = DateTime.UtcNow,
            DryRun = options.DryRun,
            TargetFamilyId = targetFamilyId,
            SourceFile = Path.GetFileName(sourcePath)
        };

        try
        {
            await using var stream = File.OpenRead(sourcePath);
            var source = await JsonSerializer.DeserializeAsync<LegacyStoreData>(stream, JsonOptions, cancellationToken)
                ?? throw new JsonException("The JSON document is empty.");

            if (source.Contas is null || source.Pagamentos is null)
            {
                report.Errors.Add("The JSON structure must contain account and payment collections.");
                return Finish(report);
            }

            report.TotalContasRead = source.Contas.Count;
            report.TotalPagamentosRead = source.Pagamentos.Count;

            if (!await db.Families.AsNoTracking().AnyAsync(x => x.Id == targetFamilyId, cancellationToken))
            {
                report.Errors.Add("Target family does not exist.");
                return Finish(report);
            }

            var legacyIds = new HashSet<Guid>();
            foreach (var conta in source.Contas)
            {
                ValidateConta(conta, legacyIds, report);
            }

            var uniquePayments = new Dictionary<(Guid ContaId, int Ano, int Mes), LegacyPagamento>();
            foreach (var pagamento in source.Pagamentos)
            {
                ValidatePagamento(pagamento, legacyIds, uniquePayments, report);
            }

            if (report.Errors.Count > 0)
            {
                return Finish(report);
            }

            var accountPlans = source.Contas.Select(x => new
            {
                Source = x,
                TargetId = DeterministicId("conta", targetFamilyId, x.Id.ToString("N"))
            }).ToList();
            var targetAccountIds = accountPlans.Select(x => x.TargetId).ToList();
            var existingAccounts = (await db.Contas.AsNoTracking()
                .Where(x => x.FamilyId == targetFamilyId && targetAccountIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken)).ToHashSet();

            var existingPaymentKeys = (await db.Pagamentos.AsNoTracking()
                .Where(x => x.FamilyId == targetFamilyId)
                .Select(x => new { x.ContaId, x.Ano, x.Mes })
                .ToListAsync(cancellationToken))
                .Select(x => (x.ContaId, x.Ano, x.Mes))
                .ToHashSet();
            var paymentPlans = uniquePayments.Values.Select(x => new
            {
                Source = x,
                ContaId = DeterministicId("conta", targetFamilyId, x.ContaId.ToString("N")),
                TargetId = DeterministicId("pagamento", targetFamilyId, $"{x.ContaId:N}:{x.Ano}:{x.Mes}")
            }).ToList();
            var targetPaymentIds = paymentPlans.Select(x => x.TargetId).ToList();
            var existingPayments = (await db.Pagamentos.AsNoTracking()
                .Where(x => x.FamilyId == targetFamilyId && targetPaymentIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken)).ToHashSet();

            report.ContasSkipped = existingAccounts.Count;
            report.ContasInserted = accountPlans.Count - report.ContasSkipped;
            var existingPaymentPlanIds = paymentPlans
                .Where(x => existingPayments.Contains(x.TargetId) || existingPaymentKeys.Contains((x.ContaId, x.Source.Ano, x.Source.Mes)))
                .Select(x => x.TargetId)
                .ToHashSet();
            report.PagamentosSkipped += existingPaymentPlanIds.Count;
            report.PagamentosInserted = paymentPlans.Count - existingPaymentPlanIds.Count;

            if (options.DryRun || (report.ContasInserted == 0 && report.PagamentosInserted == 0))
            {
                return Finish(report);
            }

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                db.Contas.AddRange(accountPlans.Where(x => !existingAccounts.Contains(x.TargetId)).Select(x => new ContaEntity
                {
                    Id = x.TargetId,
                    FamilyId = targetFamilyId,
                    Nome = x.Source.Nome.Trim(),
                    Valor = x.Source.Valor,
                    Country = x.Source.Country,
                    Currency = x.Source.Currency,
                    DiaVencimento = x.Source.DiaVencimento,
                    DataInicio = x.Source.DataInicio,
                    DuracaoMeses = x.Source.DuracaoMeses,
                    Ativa = x.Source.Ativa,
                    Observacoes = string.IsNullOrWhiteSpace(x.Source.Observacoes) ? null : x.Source.Observacoes.Trim()
                }));
                db.Pagamentos.AddRange(paymentPlans.Where(x => !existingPaymentPlanIds.Contains(x.TargetId)).Select(x => new PagamentoEntity
                {
                    Id = x.TargetId,
                    FamilyId = targetFamilyId,
                    ContaId = x.ContaId,
                    Ano = x.Source.Ano,
                    Mes = x.Source.Mes,
                    PagoEmUtc = NormalizeUtc(x.Source.PagoEm)
                }));

                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                report.DatabaseModified = true;
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }

            logger.LogInformation(
                "JSON migration completed for family {FamilyId}: {Accounts} accounts and {Payments} payments inserted.",
                targetFamilyId, report.ContasInserted, report.PagamentosInserted);
            return Finish(report);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or IOException or UnauthorizedAccessException or DbUpdateException)
        {
            db.ChangeTracker.Clear();
            report.DatabaseModified = false;
            report.Errors.Add($"Migration aborted: {exception.GetType().Name}.");
            logger.LogError(exception, "JSON migration aborted for family {FamilyId}.", targetFamilyId);
            return Finish(report);
        }
    }

    private static void ValidateConta(LegacyConta conta, HashSet<Guid> ids, MigrationReport report)
    {
        var errors = new List<string>();
        if (conta.Id == Guid.Empty) errors.Add("empty id");
        if (!ids.Add(conta.Id)) errors.Add("duplicate id");
        if (string.IsNullOrWhiteSpace(conta.Nome) || conta.Nome.Trim().Length > 80) errors.Add("invalid name");
        if (conta.Valor <= 0) errors.Add("value must be positive");
        if (conta.DiaVencimento is < 1 or > 31) errors.Add("invalid due day");
        if (conta.DuracaoMeses < 0) errors.Add("invalid duration");
        if (conta.DataInicio == default) errors.Add("invalid start date");
        if (!Enum.IsDefined(conta.Country)) errors.Add("invalid country");
        if (!Enum.IsDefined(conta.Currency)) errors.Add("invalid currency");
        if (conta.Observacoes?.Trim().Length > 300) errors.Add("notes too long");
        if (errors.Count == 0) return;
        report.ContasInvalid++;
        report.Errors.Add($"Account {SafeId(conta.Id)}: {string.Join(", ", errors)}.");
    }

    private static void ValidatePagamento(
        LegacyPagamento pagamento,
        HashSet<Guid> accountIds,
        Dictionary<(Guid, int, int), LegacyPagamento> unique,
        MigrationReport report)
    {
        var errors = new List<string>();
        if (!accountIds.Contains(pagamento.ContaId)) errors.Add("account not found");
        if (pagamento.Ano is < 1 or > 9999) errors.Add("invalid year");
        if (pagamento.Mes is < 1 or > 12) errors.Add("invalid month");
        if (pagamento.PagoEm == default) errors.Add("invalid payment date");
        if (errors.Count > 0)
        {
            report.PagamentosInvalid++;
            report.Errors.Add($"Payment for account {SafeId(pagamento.ContaId)}: {string.Join(", ", errors)}.");
            return;
        }

        var key = (pagamento.ContaId, pagamento.Ano, pagamento.Mes);
        if (!unique.TryAdd(key, pagamento))
        {
            report.DuplicatePayments++;
            report.PagamentosSkipped++;
            report.Warnings.Add($"Duplicate payment ignored for account {SafeId(pagamento.ContaId)}, {pagamento.Ano:D4}-{pagamento.Mes:D2}.");
        }
    }

    private static Guid DeterministicId(string kind, Guid familyId, string legacyKey)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"agendador:{kind}:{familyId:N}:{legacyKey}"));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[7] = (byte)((bytes[7] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes);
    }

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static string SafeId(Guid id) => id == Guid.Empty ? "<empty>" : id.ToString("N");
    private static MigrationReport Finish(MigrationReport report)
    {
        report.FinishedAtUtc = DateTime.UtcNow;
        return report;
    }

    private sealed class LegacyStoreData
    {
        public List<LegacyConta>? Contas { get; set; }
        public List<LegacyPagamento>? Pagamentos { get; set; }
    }

    private sealed class LegacyConta
    {
        public Guid Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public decimal Valor { get; set; }
        public AccountCountry Country { get; set; } = AccountCountry.UnitedKingdom;
        public AccountCurrency Currency { get; set; } = AccountCurrency.GBP;
        public int DiaVencimento { get; set; }
        public DateOnly DataInicio { get; set; }
        public int DuracaoMeses { get; set; }
        public bool Ativa { get; set; } = true;
        public string? Observacoes { get; set; }
    }

    private sealed class LegacyPagamento
    {
        public Guid ContaId { get; set; }
        public int Ano { get; set; }
        public int Mes { get; set; }
        public DateTime PagoEm { get; set; }
    }
}
