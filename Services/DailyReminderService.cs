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
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        await notificationService.SendAsync(MontarMensagem(vencimentos, hoje), cancellationToken);
        await store.RegistrarLembreteEnviadoAsync(hoje);
        _logger.LogInformation("Lembrete diario processado para {Data}.", hoje);
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

    private static string MontarMensagem(IReadOnlyList<Models.ContaVencimento> vencimentos, DateOnly data)
    {
        if (vencimentos.Count == 0)
        {
            return $"Bom dia! Nao existem contas para pagar hoje ({data:dd/MM/yyyy}).";
        }

        var linhas = new List<string>
        {
            $"Bom dia! Existem {vencimentos.Count} conta(s) para pagar hoje ({data:dd/MM/yyyy}):",
            string.Empty
        };

        linhas.AddRange(vencimentos.Select(v => $"- {v.Conta.Nome}: {v.Conta.Valor:C}"));
        return string.Join(Environment.NewLine, linhas);
    }
}
