using AgendadorContas.Models;
using AgendadorContas.Options;
using AgendadorContas.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

var tests = new (string Name, Func<Task> Run)[]
{
    ("Conta criada usa pais e moeda padrao", AccountDefaultsAreAppliedAsync),
    ("Vencimento respeita ultimo dia do mes", DueDateUsesLastDayOfShortMonthAsync),
    ("Pagamento marcado altera vencimento para pago", PaymentMarksDueAsPaidAsync),
    ("Backup restaura estado anterior", BackupRestoreRevertsDataAsync),
    ("Retencao remove apenas backups automaticos antigos", BackupRetentionRemovesOnlyOldAutomaticBackupsAsync),
    ("Lembrete agrupa totais por moeda", ReminderGroupsTotalsByCurrency),
    ("Protecao mantem apenas rotas anonimas esperadas", AccessProtectionAnonymousPathsAreLimited),
    ("Protecao ativa exige senha configurada", AccessProtectionRequiresPasswordWhenEnabled)
};

var failed = 0;

foreach (var test in tests)
{
    try
    {
        await test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL {test.Name}");
        Console.WriteLine(ex.Message);
    }
}

if (failed > 0)
{
    Console.WriteLine($"{failed} teste(s) falharam.");
    return 1;
}

Console.WriteLine($"{tests.Length} teste(s) passaram.");
return 0;

static async Task AccountDefaultsAreAppliedAsync()
{
    using var scope = new TestScope();
    var store = scope.CreateStore();

    var conta = await store.CriarContaAsync(new ContaCreateRequest
    {
        Nome = "Internet",
        Valor = 35,
        DiaVencimento = 5,
        DataInicio = new DateOnly(2026, 1, 1),
        DuracaoMeses = 0
    });

    AssertEqual(AccountCountry.UnitedKingdom, conta.Country, "Pais padrao incorreto.");
    AssertEqual(AccountCurrency.GBP, conta.Currency, "Moeda padrao incorreta.");
}

static async Task DueDateUsesLastDayOfShortMonthAsync()
{
    using var scope = new TestScope();
    var store = scope.CreateStore();

    await store.CriarContaAsync(new ContaCreateRequest
    {
        Nome = "Cartao",
        Valor = 100,
        Currency = AccountCurrency.EUR,
        Country = AccountCountry.Portugal,
        DiaVencimento = 31,
        DataInicio = new DateOnly(2026, 1, 1),
        DuracaoMeses = 0
    });

    var vencimentos = await store.ListarVencimentosAsync(new DateOnly(2026, 2, 1));

    AssertEqual(1, vencimentos.Count, "Quantidade de vencimentos incorreta.");
    AssertEqual(new DateOnly(2026, 2, 28), vencimentos[0].DataVencimento, "Vencimento deveria cair no ultimo dia de fevereiro.");
}

static async Task PaymentMarksDueAsPaidAsync()
{
    using var scope = new TestScope();
    var store = scope.CreateStore();

    var conta = await store.CriarContaAsync(new ContaCreateRequest
    {
        Nome = "Energia",
        Valor = 55,
        DiaVencimento = 12,
        DataInicio = new DateOnly(2026, 7, 1),
        DuracaoMeses = 0
    });

    var marked = await store.MarcarPagamentoAsync(conta.Id, 2026, 7);
    var vencimentos = await store.ListarVencimentosAsync(new DateOnly(2026, 7, 1));

    AssertTrue(marked, "Pagamento deveria ser marcado.");
    AssertTrue(vencimentos.Single().Pago, "Vencimento deveria estar pago.");
    AssertTrue(vencimentos.Single().PagoEm is not null, "Data de pagamento deveria existir.");
}

static async Task BackupRestoreRevertsDataAsync()
{
    using var scope = new TestScope();
    var store = scope.CreateStore();

    await store.CriarContaAsync(new ContaCreateRequest
    {
        Nome = "Original",
        Valor = 10,
        DiaVencimento = 1,
        DataInicio = new DateOnly(2026, 7, 1),
        DuracaoMeses = 0
    });

    var backup = await store.CriarBackupAsync();

    await store.CriarContaAsync(new ContaCreateRequest
    {
        Nome = "Extra",
        Valor = 20,
        DiaVencimento = 2,
        DataInicio = new DateOnly(2026, 7, 1),
        DuracaoMeses = 0
    });

    var restored = await store.RestaurarBackupAsync(backup.FileName);
    var contas = await store.ListarContasAsync();
    var backups = await store.ListarBackupsAsync();

    AssertTrue(restored, "Backup deveria ser restaurado.");
    AssertEqual(1, contas.Count, "Restauracao deveria voltar para uma conta.");
    AssertTrue(backups.Any(item => item.FileName.Contains("pre-restore", StringComparison.Ordinal)), "Backup pre-restore deveria ser criado.");
}

static async Task BackupRetentionRemovesOnlyOldAutomaticBackupsAsync()
{
    using var scope = new TestScope();
    var store = scope.CreateStore();

    await store.CriarContaAsync(new ContaCreateRequest
    {
        Nome = "Seguro",
        Valor = 30,
        DiaVencimento = 10,
        DataInicio = new DateOnly(2026, 7, 1),
        DuracaoMeses = 0
    });

    var manual = await store.CriarBackupAsync();
    await Task.Delay(5);
    var oldAuto1 = await store.CriarBackupAsync("auto");
    await Task.Delay(5);
    var oldAuto2 = await store.CriarBackupAsync("auto");
    await Task.Delay(5);
    var oldAuto3 = await store.CriarBackupAsync("auto");
    await Task.Delay(5);
    _ = await store.CriarBackupAsync("auto");

    var backupDirectory = Path.Combine(scope.RootPath, "backups");
    var oldDate = DateTime.UtcNow.AddDays(-60);
    foreach (var fileName in new[] { manual.FileName, oldAuto1.FileName, oldAuto2.FileName, oldAuto3.FileName })
    {
        var path = Path.Combine(backupDirectory, fileName);
        File.SetCreationTimeUtc(path, oldDate);
        File.SetLastWriteTimeUtc(path, oldDate);
    }

    var removed = await store.RemoverBackupsAutomaticosAntigosAsync(retentionDays: 30, minimumBackupsToKeep: 2);
    var backups = await store.ListarBackupsAsync();

    AssertEqual(2, removed, "Retencao deveria remover dois backups automaticos antigos.");
    AssertTrue(backups.Any(item => item.FileName == manual.FileName), "Backup manual antigo nao deveria ser removido.");
    AssertEqual(2, backups.Count(item => item.FileName.StartsWith("contas.auto.", StringComparison.Ordinal)), "Deveriam restar dois backups automaticos.");
}

static Task ReminderGroupsTotalsByCurrency()
{
    var builder = new ReminderMessageBuilder(new MoneyFormatter());
    var message = builder.BuildDailyMessage(
    [
        new ContaVencimento
        {
            Conta = new Conta { Nome = "Rent", Valor = 950, Currency = AccountCurrency.GBP },
            DataVencimento = new DateOnly(2026, 7, 12),
            Pago = false
        },
        new ContaVencimento
        {
            Conta = new Conta { Nome = "Luz", Valor = 120, Currency = AccountCurrency.EUR },
            DataVencimento = new DateOnly(2026, 7, 12),
            Pago = false
        }
    ], new DateOnly(2026, 7, 12));

    AssertContains("£950.00", message, "Mensagem deveria conter valor em GBP.");
    AssertContains("120,00", message, "Mensagem deveria conter valor em EUR.");
    AssertContains("Total do dia", message, "Mensagem deveria conter totais.");
    return Task.CompletedTask;
}

static Task AccessProtectionAnonymousPathsAreLimited()
{
    AssertTrue(AccessProtectionMiddlewareExtensions.IsAnonymousPath("/health"), "Health check deveria ser anonimo.");
    AssertTrue(AccessProtectionMiddlewareExtensions.IsAnonymousPath("/login.html"), "Login deveria ser anonimo.");
    AssertTrue(AccessProtectionMiddlewareExtensions.IsAnonymousPath("/api/auth/login"), "Endpoint de login deveria ser anonimo.");
    AssertTrue(!AccessProtectionMiddlewareExtensions.IsAnonymousPath("/api/contas"), "API de contas nao deveria ser anonima.");
    AssertTrue(!AccessProtectionMiddlewareExtensions.IsAnonymousPath("/"), "Pagina principal nao deveria ser anonima quando protecao estiver ativa.");
    return Task.CompletedTask;
}

static Task AccessProtectionRequiresPasswordWhenEnabled()
{
    var validator = new AccessProtectionOptionsValidator();
    var result = validator.Validate(null, new AccessProtectionOptions
    {
        Enabled = true,
        Username = "admin",
        Password = "",
        SessionHours = 12
    });

    AssertTrue(result.Failed, "Protecao ativa sem senha deveria falhar validacao.");
    AssertContains("Password", string.Join(" ", result.Failures ?? []), "Falha deveria mencionar senha.");
    return Task.CompletedTask;
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{message} Esperado: {expected}. Atual: {actual}.");
    }
}

static void AssertContains(string expected, string actual, string message)
{
    if (!actual.Contains(expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"{message} Trecho esperado: {expected}.");
    }
}

internal sealed class TestScope : IDisposable
{
    private readonly string _rootPath;

    public string RootPath => _rootPath;

    public TestScope()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "agendador-contas-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
    }

    public ContaStore CreateStore()
    {
        var dataPath = Path.Combine(_rootPath, "contas.json");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Data:FilePath"] = dataPath
            })
            .Build();

        return new ContaStore(configuration, new FakeWebHostEnvironment(_rootPath), NullLogger<ContaStore>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }
}

internal sealed class FakeWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = "AgendadorContas.Tests";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public string ContentRootPath { get; set; } = contentRootPath;
    public string EnvironmentName { get; set; } = "Testing";
    public string WebRootPath { get; set; } = contentRootPath;
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
}
