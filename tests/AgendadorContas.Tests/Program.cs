using AgendadorContas.Models;
using AgendadorContas.Options;
using AgendadorContas.Services;
using AgendadorContas.Data;
using AgendadorContas.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

var tests = new (string Name, Func<Task> Run)[]
{
    ("Conta criada usa pais e moeda padrao", AccountDefaultsAreAppliedAsync),
    ("Vencimento respeita ultimo dia do mes", DueDateUsesLastDayOfShortMonthAsync),
    ("Pagamento marcado altera vencimento para pago", PaymentMarksDueAsPaidAsync),
    ("Backup restaura estado anterior", BackupRestoreRevertsDataAsync),
    ("Backup automatico ignora duplicata e forca copia semanal", BackupAutomaticDeduplicationAsync),
    ("Retencao remove apenas backups automaticos antigos", BackupRetentionRemovesOnlyOldAutomaticBackupsAsync),
    ("Lembrete agrupa totais por moeda", ReminderGroupsTotalsByCurrency),
    ("Configuracao do lembrete usa padroes", ReminderSettingsUsesDefaultsAsync),
    ("Configuracao do lembrete salva e valida horario", ReminderSettingsPersistsAndValidatesAsync),
    ("Protecao mantem apenas rotas anonimas esperadas", AccessProtectionAnonymousPathsAreLimited),
    ("Protecao ativa exige senha configurada", AccessProtectionRequiresPasswordWhenEnabled),
    ("HSTS e aplicado apenas quando solicitado", SecurityHeadersApplyHstsWhenRequestedAsync),
    ("Modelo relacional isola familias e settings", MultiTenantFamiliesAreIsolatedAsync),
    ("Modelo relacional impede pagamento cross-family", CrossFamilyPaymentIsRejectedAsync),
    ("Modelo relacional separa roles e lembretes", FamilyRolesAndRemindersAreSeparatedAsync),
    ("Membership duplicado e impedido", DuplicateMembershipIsRejectedAsync),
    ("Settings duplicados por familia sao impedidos", DuplicateFamilySettingsAreRejectedAsync),
    ("Telegram settings duplicados por familia sao impedidos", DuplicateTelegramSettingsAreRejectedAsync),
    ("Pagamento duplicado no mesmo mes e impedido", DuplicateMonthlyPaymentIsRejectedAsync),
    ("Roles e valores invalidos sao impedidos", InvalidRelationalValuesAreRejectedAsync),
    ("Deletes respeitam cascades e restricoes", RelationalDeleteBehaviorsAreEnforcedAsync),
    ("Migration inicial contem schema multi-tenant", InitialMigrationContainsExpectedSchema)
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

static async Task BackupAutomaticDeduplicationAsync()
{
    using var scope = new TestScope();
    var store = scope.CreateStore();

    await store.CriarContaAsync(new ContaCreateRequest
    {
        Nome = "Internet",
        Valor = 35,
        DiaVencimento = 15,
        DataInicio = new DateOnly(2026, 7, 1),
        DuracaoMeses = 0
    });

    var first = await store.CriarBackupAutomaticoSeAlteradoAsync(7);
    var duplicate = await store.CriarBackupAutomaticoSeAlteradoAsync(7);

    AssertTrue(first is not null, "Primeiro backup automatico deveria ser criado.");
    AssertTrue(duplicate is null, "Backup automatico identico deveria ser ignorado.");

    var firstPath = Path.Combine(scope.RootPath, "backups", first!.FileName);
    var oldDate = DateTime.UtcNow.AddDays(-8);
    File.SetCreationTimeUtc(firstPath, oldDate);
    File.SetLastWriteTimeUtc(firstPath, oldDate);

    var forced = await store.CriarBackupAutomaticoSeAlteradoAsync(7);
    AssertTrue(forced is not null, "Backup semanal deveria ser forcado mesmo sem alteracoes.");

    await store.CriarContaAsync(new ContaCreateRequest
    {
        Nome = "Energia",
        Valor = 50,
        DiaVencimento = 20,
        DataInicio = new DateOnly(2026, 7, 1),
        DuracaoMeses = 0
    });

    var changed = await store.CriarBackupAutomaticoSeAlteradoAsync(7);
    AssertTrue(changed is not null, "Alteracao nos dados deveria criar novo backup.");
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

static async Task ReminderSettingsUsesDefaultsAsync()
{
    using var scope = new TestScope();
    var store = scope.CreateReminderSettingsStore(hour: 7, minute: 45, timeZoneId: "Europe/London");

    var settings = await store.GetAsync();

    AssertEqual(7, settings.Hour, "Hora padrao do lembrete incorreta.");
    AssertEqual(45, settings.Minute, "Minuto padrao do lembrete incorreto.");
    AssertEqual("Europe/London", settings.TimeZoneId, "Fuso horario padrao do lembrete incorreto.");
}

static async Task ReminderSettingsPersistsAndValidatesAsync()
{
    using var scope = new TestScope();
    var store = scope.CreateReminderSettingsStore();

    var saved = await store.UpdateAsync(new ReminderSettingsUpdateRequest
    {
        Hour = 12,
        Minute = 30,
        TimeZoneId = "Europe/London"
    });
    var loaded = await store.GetAsync();

    AssertEqual(12, saved.Hour, "Hora salva incorreta.");
    AssertEqual(30, loaded.Minute, "Minuto persistido incorreto.");
    AssertEqual("Europe/London", loaded.TimeZoneId, "Fuso horario persistido incorreto.");

    try
    {
        await store.UpdateAsync(new ReminderSettingsUpdateRequest { Hour = 24, Minute = 0 });
        throw new InvalidOperationException("Horario invalido deveria falhar validacao.");
    }
    catch (ArgumentException)
    {
    }
}

static Task AccessProtectionAnonymousPathsAreLimited()
{
    AssertTrue(AccessProtectionMiddlewareExtensions.IsAnonymousPath("/health"), "Health check deveria ser anonimo.");
    AssertTrue(AccessProtectionMiddlewareExtensions.IsAnonymousPath("/login.html"), "Login deveria ser anonimo.");
    AssertTrue(AccessProtectionMiddlewareExtensions.IsAnonymousPath("/login.js"), "JavaScript do login deveria ser anonimo.");
    AssertTrue(AccessProtectionMiddlewareExtensions.IsAnonymousPath("/login.css"), "CSS do login deveria ser anonimo.");
    AssertTrue(AccessProtectionMiddlewareExtensions.IsAnonymousPath("/api/auth/login"), "Endpoint de login deveria ser anonimo.");
    AssertTrue(!AccessProtectionMiddlewareExtensions.IsAnonymousPath("/app.js"), "JavaScript principal deveria continuar protegido.");
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

static async Task SecurityHeadersApplyHstsWhenRequestedAsync()
{
    var productionHeaders = new HeaderDictionary();
    SecurityHeadersMiddlewareExtensions.ApplySecurityHeaders(
        productionHeaders,
        includeHsts: true);

    AssertEqual(
        "max-age=31536000; includeSubDomains",
        productionHeaders["Strict-Transport-Security"].ToString(),
        "HSTS deveria ser aplicado em producao.");

    var developmentHeaders = new HeaderDictionary();
    SecurityHeadersMiddlewareExtensions.ApplySecurityHeaders(
        developmentHeaders,
        includeHsts: false);

    AssertTrue(
        !developmentHeaders.ContainsKey("Strict-Transport-Security"),
        "HSTS nao deveria ser aplicado quando desativado.");

    await Task.CompletedTask;
}

static async Task MultiTenantFamiliesAreIsolatedAsync()
{
    await using var scope = await RelationalTestScope.CreateAsync();
    var familyA = NewFamily("Family A");
    var familyB = NewFamily("Family B");
    scope.Db.Families.AddRange(familyA, familyB);
    scope.Db.FamilySettings.AddRange(
        new FamilySettings { FamilyId = familyA.Id, TimeZoneId = "Europe/London", ReminderHour = 8 },
        new FamilySettings { FamilyId = familyB.Id, TimeZoneId = "Europe/Lisbon", ReminderHour = 9 });
    scope.Db.Contas.AddRange(NewConta(familyA.Id, "Bill A"), NewConta(familyB.Id, "Bill B"));
    await scope.Db.SaveChangesAsync();

    AssertEqual(1, await scope.Db.Contas.CountAsync(x => x.FamilyId == familyA.Id), "Family A deveria ter uma conta.");
    AssertEqual("Europe/Lisbon", (await scope.Db.FamilySettings.SingleAsync(x => x.FamilyId == familyB.Id)).TimeZoneId, "Settings deveriam permanecer separados.");
}

static async Task CrossFamilyPaymentIsRejectedAsync()
{
    await using var scope = await RelationalTestScope.CreateAsync();
    var familyA = NewFamily("Family A");
    var familyB = NewFamily("Family B");
    var billA = NewConta(familyA.Id, "Bill A");
    scope.Db.AddRange(familyA, familyB, billA);
    await scope.Db.SaveChangesAsync();

    scope.Db.Pagamentos.Add(new PagamentoEntity
    {
        FamilyId = familyB.Id,
        ContaId = billA.Id,
        Ano = 2026,
        Mes = 8
    });

    try
    {
        await scope.Db.SaveChangesAsync();
        throw new InvalidOperationException("Pagamento cross-family deveria violar a FK composta.");
    }
    catch (DbUpdateException)
    {
    }
}

static async Task FamilyRolesAndRemindersAreSeparatedAsync()
{
    await using var scope = await RelationalTestScope.CreateAsync();
    var familyA = NewFamily("Family A");
    var familyB = NewFamily("Family B");
    var owner = new AppUser { Id = Guid.NewGuid(), UserName = "owner-a", NormalizedUserName = "OWNER-A" };
    var member = new AppUser { Id = Guid.NewGuid(), UserName = "member-b", NormalizedUserName = "MEMBER-B" };
    scope.Db.AddRange(familyA, familyB, owner, member);
    scope.Db.FamilyUsers.AddRange(
        new FamilyUser { FamilyId = familyA.Id, UserId = owner.Id, Role = FamilyRole.Owner },
        new FamilyUser { FamilyId = familyB.Id, UserId = member.Id, Role = FamilyRole.Member });
    scope.Db.LembretesEnviados.AddRange(
        new LembreteEnviadoEntity { FamilyId = familyA.Id, LocalDate = new DateOnly(2026, 8, 13) },
        new LembreteEnviadoEntity { FamilyId = familyB.Id, LocalDate = new DateOnly(2026, 8, 13) });
    await scope.Db.SaveChangesAsync();

    AssertEqual(FamilyRole.Owner, (await scope.Db.FamilyUsers.SingleAsync(x => x.FamilyId == familyA.Id)).Role, "Owner deveria permanecer associado a A.");
    AssertEqual(1, await scope.Db.LembretesEnviados.CountAsync(x => x.FamilyId == familyB.Id), "Lembretes deveriam permanecer separados.");
    AssertEqual(3, Enum.GetValues<FamilyRole>().Length, "Somente Owner, Admin e Member sao roles validos.");
}

static async Task DuplicateMembershipIsRejectedAsync()
{
    await using var scope = await RelationalTestScope.CreateAsync();
    var family = NewFamily("Family A");
    var user = new AppUser { Id = Guid.NewGuid(), UserName = "member", NormalizedUserName = "MEMBER" };
    scope.Db.AddRange(family, user);
    scope.Db.FamilyUsers.Add(new FamilyUser { FamilyId = family.Id, UserId = user.Id, Role = FamilyRole.Member });
    await scope.Db.SaveChangesAsync();

    scope.Db.ChangeTracker.Clear();
    scope.Db.FamilyUsers.Add(new FamilyUser { FamilyId = family.Id, UserId = user.Id, Role = FamilyRole.Admin });
    await AssertDbUpdateRejectedAsync(scope.Db, "Membership duplicado deveria violar a PK composta.");
}

static async Task DuplicateFamilySettingsAreRejectedAsync()
{
    await using var scope = await RelationalTestScope.CreateAsync();
    var family = NewFamily("Family A");
    scope.Db.Add(family);
    scope.Db.FamilySettings.Add(new FamilySettings { FamilyId = family.Id, TimeZoneId = "Europe/London" });
    await scope.Db.SaveChangesAsync();

    scope.Db.ChangeTracker.Clear();
    scope.Db.FamilySettings.Add(new FamilySettings { FamilyId = family.Id, TimeZoneId = "Europe/Lisbon" });
    await AssertDbUpdateRejectedAsync(scope.Db, "Settings duplicados deveriam violar a PK por familia.");
}

static async Task DuplicateTelegramSettingsAreRejectedAsync()
{
    await using var scope = await RelationalTestScope.CreateAsync();
    var family = NewFamily("Family A");
    scope.Db.Add(family);
    scope.Db.TelegramSettings.Add(new TelegramSettings { FamilyId = family.Id });
    await scope.Db.SaveChangesAsync();

    scope.Db.ChangeTracker.Clear();
    scope.Db.TelegramSettings.Add(new TelegramSettings { FamilyId = family.Id });
    await AssertDbUpdateRejectedAsync(scope.Db, "Telegram settings duplicados deveriam violar a PK por familia.");
}

static async Task DuplicateMonthlyPaymentIsRejectedAsync()
{
    await using var scope = await RelationalTestScope.CreateAsync();
    var family = NewFamily("Family A");
    var conta = NewConta(family.Id, "Bill A");
    scope.Db.AddRange(family, conta);
    scope.Db.Pagamentos.Add(new PagamentoEntity { FamilyId = family.Id, ContaId = conta.Id, Ano = 2026, Mes = 8 });
    await scope.Db.SaveChangesAsync();

    scope.Db.ChangeTracker.Clear();
    scope.Db.Pagamentos.Add(new PagamentoEntity { FamilyId = family.Id, ContaId = conta.Id, Ano = 2026, Mes = 8 });
    await AssertDbUpdateRejectedAsync(scope.Db, "Pagamento mensal duplicado deveria violar o indice unico.");
}

static async Task InvalidRelationalValuesAreRejectedAsync()
{
    await using var scope = await RelationalTestScope.CreateAsync();
    var family = NewFamily("Family A");
    var user = new AppUser { Id = Guid.NewGuid(), UserName = "member", NormalizedUserName = "MEMBER" };
    scope.Db.AddRange(family, user);
    await scope.Db.SaveChangesAsync();

    scope.Db.FamilyUsers.Add(new FamilyUser { FamilyId = family.Id, UserId = user.Id, Role = (FamilyRole)999 });
    await AssertDbUpdateRejectedAsync(scope.Db, "Role fora de Owner/Admin/Member deveria violar o check constraint.");

    scope.Db.ChangeTracker.Clear();
    var invalidConta = NewConta(family.Id, "Invalid");
    invalidConta.DiaVencimento = 32;
    scope.Db.Contas.Add(invalidConta);
    await AssertDbUpdateRejectedAsync(scope.Db, "Dia de vencimento invalido deveria violar o check constraint.");
}

static async Task RelationalDeleteBehaviorsAreEnforcedAsync()
{
    await using var scope = await RelationalTestScope.CreateAsync();
    var family = NewFamily("Family A");
    var conta = NewConta(family.Id, "Bill A");
    scope.Db.AddRange(family, conta);
    scope.Db.FamilySettings.Add(new FamilySettings { FamilyId = family.Id, TimeZoneId = "Europe/London" });
    scope.Db.Pagamentos.Add(new PagamentoEntity { FamilyId = family.Id, ContaId = conta.Id, Ano = 2026, Mes = 8 });
    await scope.Db.SaveChangesAsync();

    scope.Db.ChangeTracker.Clear();
    scope.Db.Families.Remove(new Family { Id = family.Id, Name = family.Name });
    await AssertDbUpdateRejectedAsync(scope.Db, "Familia com conta deveria ser protegida por delete Restrict.");

    scope.Db.ChangeTracker.Clear();
    var persistedConta = await scope.Db.Contas.SingleAsync(x => x.Id == conta.Id);
    scope.Db.Contas.Remove(persistedConta);
    await scope.Db.SaveChangesAsync();
    AssertEqual(0, await scope.Db.Pagamentos.CountAsync(), "Remover conta deveria remover seus pagamentos.");

    var persistedFamily = await scope.Db.Families.SingleAsync(x => x.Id == family.Id);
    scope.Db.Families.Remove(persistedFamily);
    await scope.Db.SaveChangesAsync();
    AssertEqual(0, await scope.Db.FamilySettings.CountAsync(), "Remover familia deveria remover seus settings dependentes.");
}

static async Task AssertDbUpdateRejectedAsync(AgendadorDbContext db, string message)
{
    try
    {
        await db.SaveChangesAsync();
        throw new InvalidOperationException(message);
    }
    catch (DbUpdateException)
    {
    }
}

static Task InitialMigrationContainsExpectedSchema()
{
    var options = new DbContextOptionsBuilder<AgendadorDbContext>()
        .UseNpgsql("Host=localhost;Database=schema_test;Username=schema_test")
        .Options;
    using var db = new AgendadorDbContext(options);
    var migrations = db.GetService<IMigrationsAssembly>().Migrations;
    AssertTrue(migrations.Keys.Any(x => x.EndsWith("_InitialMultiTenantSchema", StringComparison.Ordinal)), "Migration inicial nao foi encontrada.");

    var tables = db.Model.GetEntityTypes().Select(x => x.GetTableName()).Where(x => x is not null).ToHashSet();
    foreach (var table in new[] { "families", "family_users", "family_settings", "telegram_settings", "contas", "pagamentos", "lembretes_enviados", "app_users" })
    {
        AssertTrue(tables.Contains(table), $"Tabela {table} deveria existir no modelo da migration.");
    }

    return Task.CompletedTask;
}

static Family NewFamily(string name) => new() { Id = Guid.NewGuid(), Name = name };

static ContaEntity NewConta(Guid familyId, string name) => new()
{
    Id = Guid.NewGuid(), FamilyId = familyId, Nome = name, Valor = 10,
    DiaVencimento = 10, DataInicio = new DateOnly(2026, 1, 1)
};

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

    public ReminderSettingsStore CreateReminderSettingsStore(int hour = 8, int minute = 0, string timeZoneId = "Europe/London")
    {
        var dataPath = Path.Combine(_rootPath, "contas.json");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Data:FilePath"] = dataPath,
                ["Reminder:Hour"] = hour.ToString(),
                ["Reminder:Minute"] = minute.ToString(),
                ["Reminder:TimeZoneId"] = timeZoneId
            })
            .Build();

        return new ReminderSettingsStore(configuration, new FakeWebHostEnvironment(_rootPath));
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

internal sealed class RelationalTestScope : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    public AgendadorDbContext Db { get; }

    private RelationalTestScope(SqliteConnection connection, AgendadorDbContext db)
    {
        _connection = connection;
        Db = db;
    }

    public static async Task<RelationalTestScope> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA foreign_keys = ON;";
            await command.ExecuteNonQueryAsync();
        }
        var options = new DbContextOptionsBuilder<AgendadorDbContext>().UseSqlite(connection).Options;
        var db = new AgendadorDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return new RelationalTestScope(connection, db);
    }

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
