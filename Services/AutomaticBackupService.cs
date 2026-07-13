using AgendadorContas.Options;
using Microsoft.Extensions.Options;

namespace AgendadorContas.Services;

public sealed class AutomaticBackupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<BackupOptions> _options;
    private readonly ILogger<AutomaticBackupService> _logger;
    private DateOnly? _lastBackupDate;
    private bool _startupBackupAttempted;

    public AutomaticBackupService(
        IServiceProvider serviceProvider,
        IOptions<BackupOptions> options,
        ILogger<AutomaticBackupService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TryCreateBackupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao executar backup automatico.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task TryCreateBackupAsync(CancellationToken cancellationToken)
    {
        var backupOptions = _options.Value;
        if (!backupOptions.AutomaticEnabled)
        {
            return;
        }

        var now = GetLocalNow(backupOptions);
        var shouldRunOnStartup = backupOptions.RunOnStartup && !_startupBackupAttempted;
        var shouldRunScheduled = now.Hour == backupOptions.Hour && now.Minute == backupOptions.Minute;

        if (!shouldRunOnStartup && !shouldRunScheduled)
        {
            return;
        }

        _startupBackupAttempted = true;
        var today = DateOnly.FromDateTime(now);
        if (_lastBackupDate == today)
        {
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ContaStore>();
        var backup = await store.CriarBackupAsync("auto");
        var removed = await store.RemoverBackupsAutomaticosAntigosAsync(
            backupOptions.RetentionDays,
            backupOptions.MinimumBackupsToKeep,
            cancellationToken);

        _lastBackupDate = today;
        _logger.LogInformation(
            "Backup automatico criado: {BackupFile}. Backups automaticos removidos pela retencao: {RemovedCount}.",
            backup.FileName,
            removed);
    }

    private DateTime GetLocalNow(BackupOptions backupOptions)
    {
        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(backupOptions.TimeZoneId);
            return TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone).DateTime;
        }
        catch (TimeZoneNotFoundException)
        {
            _logger.LogWarning("Time zone {TimeZoneId} nao encontrada. Usando horario local.", backupOptions.TimeZoneId);
            return DateTime.Now;
        }
    }
}
