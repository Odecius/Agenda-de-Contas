namespace AgendadorContas.Services;

public sealed class DailyReminderService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DailyReminderService> _logger;

    public DailyReminderService(IServiceProvider serviceProvider, ILogger<DailyReminderService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TentarEnviarLembreteAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar lembretes diarios.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task TentarEnviarLembreteAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var settingsStore = scope.ServiceProvider.GetRequiredService<ReminderSettingsStore>();
        var settings = await settingsStore.GetAsync();
        var agora = ObterAgoraLocal(settings.TimeZoneId);

        if (agora.Hour != settings.Hour || agora.Minute != settings.Minute)
        {
            return;
        }

        var hoje = DateOnly.FromDateTime(agora);

        var store = scope.ServiceProvider.GetRequiredService<ContaStore>();
        if (await store.LembreteJaEnviadoAsync(hoje))
        {
            return;
        }

        var vencimentos = await store.ListarVencimentosDoDiaAsync(hoje);
        var messageBuilder = scope.ServiceProvider.GetRequiredService<IReminderMessageBuilder>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var message = messageBuilder.BuildDailyMessage(vencimentos, hoje);

        var sent = await notificationService.SendAsync(message, cancellationToken);
        if (!sent)
        {
            _logger.LogWarning("Lembrete diario nao foi marcado como enviado porque nenhuma notificacao foi enviada para {Data}.", hoje);
            return;
        }

        await store.RegistrarLembreteEnviadoAsync(hoje);
        _logger.LogInformation("Lembrete diario enviado e registrado para {Data}.", hoje);
    }

    private DateTime ObterAgoraLocal(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return DateTime.Now;
        }

        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone).DateTime;
        }
        catch (TimeZoneNotFoundException)
        {
            _logger.LogWarning("Time zone {TimeZoneId} nao encontrada. Usando horario local.", timeZoneId);
            return DateTime.Now;
        }
    }
}
