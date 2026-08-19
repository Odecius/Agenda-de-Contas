using AgendadorContas.Data;
using AgendadorContas.Data.Entities;
using AgendadorContas.Models;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;

namespace AgendadorContas.Services;

public interface IFamilyTelegramSender
{
    Task<bool> SendAsync(TelegramSettings settings, string message, CancellationToken cancellationToken = default);
}

public interface IMultiFamilyReminderProcessor
{
    Task ProcessAsync(DateTimeOffset utcNow, CancellationToken cancellationToken = default);
}

public sealed class MultiFamilyReminderProcessor(
    AgendadorDbContext db,
    IFamilyTelegramSender sender,
    IReminderMessageBuilder messageBuilder,
    ILogger<MultiFamilyReminderProcessor> logger) : IMultiFamilyReminderProcessor
{
    public async Task ProcessAsync(DateTimeOffset utcNow, CancellationToken cancellationToken = default)
    {
        var familyIds = await db.Families.AsNoTracking().Where(x => x.IsActive).Select(x => x.Id).ToListAsync(cancellationToken);
        foreach (var familyId in familyIds)
        {
            try { await ProcessFamilyAsync(familyId, utcNow, cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex) { logger.LogError(ex, "Reminder processing failed for family {FamilyId}; continuing.", familyId); }
        }
    }

    private async Task ProcessFamilyAsync(Guid familyId, DateTimeOffset utcNow, CancellationToken cancellationToken)
    {
        var settings = await db.FamilySettings.AsNoTracking().SingleOrDefaultAsync(x => x.FamilyId == familyId, cancellationToken);
        var telegram = await db.TelegramSettings.AsNoTracking().SingleOrDefaultAsync(x => x.FamilyId == familyId, cancellationToken);
        if (settings is null || telegram is null || !telegram.IsEnabled) return;
        var zone = TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZoneId);
        var local = TimeZoneInfo.ConvertTime(utcNow, zone);
        if (local.Hour != settings.ReminderHour || local.Minute != settings.ReminderMinute) return;
        var date = DateOnly.FromDateTime(local.DateTime);
        if (await db.LembretesEnviados.AsNoTracking().AnyAsync(x => x.FamilyId == familyId && x.LocalDate == date && x.Channel == "Telegram", cancellationToken)) return;

        var contas = await db.Contas.AsNoTracking().Where(x => x.FamilyId == familyId && x.Ativa).ToListAsync(cancellationToken);
        var paidIds = await db.Pagamentos.AsNoTracking()
            .Where(x => x.FamilyId == familyId && x.Ano == date.Year && x.Mes == date.Month)
            .Select(x => x.ContaId).ToListAsync(cancellationToken);
        var paid = paidIds.ToHashSet();
        var due = contas.Where(x => IsActiveInMonth(x, date) && Math.Min(x.DiaVencimento, DateTime.DaysInMonth(date.Year, date.Month)) == date.Day && !paid.Contains(x.Id))
            .Select(x => new ContaVencimento
            {
                Conta = new Conta { Id = x.Id, Nome = x.Nome, Valor = x.Valor, Country = x.Country, Currency = x.Currency, DiaVencimento = x.DiaVencimento, DataInicio = x.DataInicio, DuracaoMeses = x.DuracaoMeses, Ativa = x.Ativa, Observacoes = x.Observacoes },
                DataVencimento = date,
                Pago = false
            }).ToList();
        var sent = await sender.SendAsync(telegram, messageBuilder.BuildDailyMessage(due, date), cancellationToken);
        if (!sent) return;
        db.LembretesEnviados.Add(new LembreteEnviadoEntity { FamilyId = familyId, LocalDate = date, Channel = "Telegram", SentAtUtc = utcNow.UtcDateTime });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static bool IsActiveInMonth(ContaEntity conta, DateOnly date)
    {
        var months = ((date.Year - conta.DataInicio.Year) * 12) + date.Month - conta.DataInicio.Month;
        return months >= 0 && (conta.DuracaoMeses == 0 || months < conta.DuracaoMeses);
    }
}

public sealed class MultiFamilyReminderWorker(IServiceProvider services, TimeProvider timeProvider, ILogger<MultiFamilyReminderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IMultiFamilyReminderProcessor>().ProcessAsync(timeProvider.GetUtcNow(), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogError(ex, "Multi-family reminder cycle failed."); }
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}

public sealed class FamilyTelegramSender(IHttpClientFactory clients, IConfiguration configuration, ILogger<FamilyTelegramSender> logger) : IFamilyTelegramSender
{
    public async Task<bool> SendAsync(TelegramSettings settings, string message, CancellationToken cancellationToken = default)
    {
        if (!settings.IsEnabled || string.IsNullOrWhiteSpace(settings.ChatId) || string.IsNullOrWhiteSpace(settings.BotTokenSecretReference)) return false;
        var token = configuration[$"MultiFamilyTelegramSecrets:{settings.FamilyId:D}:{settings.BotTokenSecretReference}"];
        if (string.IsNullOrWhiteSpace(token))
        {
            logger.LogWarning("Telegram secret reference is unresolved for family {FamilyId}.", settings.FamilyId);
            return false;
        }
        using var response = await clients.CreateClient("Telegram").PostAsJsonAsync($"/bot{token}/sendMessage", new { chat_id = settings.ChatId, text = message, parse_mode = "HTML" }, cancellationToken);
        if (!response.IsSuccessStatusCode) logger.LogWarning("Telegram send failed for family {FamilyId} with status {StatusCode}.", settings.FamilyId, response.StatusCode);
        return response.IsSuccessStatusCode;
    }
}
