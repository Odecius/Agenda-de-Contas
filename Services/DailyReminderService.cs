namespace AgendadorContas.Services;

public sealed class DailyReminderService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DailyReminderService> _logger;

    public DailyReminderService(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<DailyReminderService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
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
        var agora = ObterAgoraLocal();
        var hora = _configuration.GetValue("Reminder:Hour", 8);
        var minuto = _configuration.GetValue("Reminder:Minute", 0);

        if (agora.Hour != hora || agora.Minute != minuto)
        {
            return;
        }

        var hoje = DateOnly.FromDateTime(agora);

        using var scope = _serviceProvider.CreateScope();
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

    private DateTime ObterAgoraLocal()
    {
        var timeZoneId = _configuration["Reminder:TimeZoneId"];
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
