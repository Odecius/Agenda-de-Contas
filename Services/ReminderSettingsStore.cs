using System.Text.Json;
using AgendadorContas.Models;

namespace AgendadorContas.Services;

public sealed class ReminderSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;
    private readonly ReminderSettings _defaults;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public ReminderSettingsStore(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var dataFilePath = ResolveDataFilePath(configuration, environment);
        var dataDirectory = Path.GetDirectoryName(dataFilePath)!;

        _settingsPath = Path.Combine(dataDirectory, "settings.json");
        _defaults = new ReminderSettings
        {
            Hour = configuration.GetValue("Reminder:Hour", 8),
            Minute = configuration.GetValue("Reminder:Minute", 0),
            TimeZoneId = configuration["Reminder:TimeZoneId"] ?? "Europe/London"
        };
    }

    public async Task<ReminderSettings> GetAsync()
    {
        await _lock.WaitAsync();
        try
        {
            return await ReadAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<ReminderSettings> UpdateAsync(ReminderSettingsUpdateRequest request)
    {
        Validate(request);

        await _lock.WaitAsync();
        try
        {
            var current = await ReadAsync();
            var settings = new ReminderSettings
            {
                Hour = request.Hour,
                Minute = request.Minute,
                TimeZoneId = string.IsNullOrWhiteSpace(request.TimeZoneId)
                    ? current.TimeZoneId
                    : request.TimeZoneId.Trim()
            };

            await WriteAsync(settings);
            return settings;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<ReminderSettings> ReadAsync()
    {
        if (!File.Exists(_settingsPath))
        {
            return CloneDefaults();
        }

        await using var stream = File.OpenRead(_settingsPath);
        var settings = await JsonSerializer.DeserializeAsync<ReminderSettings>(stream, JsonOptions) ?? CloneDefaults();

        return Normalize(settings);
    }

    private async Task WriteAsync(ReminderSettings settings)
    {
        var directory = Path.GetDirectoryName(_settingsPath)!;
        Directory.CreateDirectory(directory);

        var tempPath = Path.Combine(directory, $"{Path.GetFileName(_settingsPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, settings, JsonOptions);
            }

            File.Move(tempPath, _settingsPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private ReminderSettings Normalize(ReminderSettings settings)
    {
        if (settings.Hour is < 0 or > 23 || settings.Minute is < 0 or > 59)
        {
            return CloneDefaults();
        }

        if (string.IsNullOrWhiteSpace(settings.TimeZoneId))
        {
            settings.TimeZoneId = _defaults.TimeZoneId;
        }

        return settings;
    }

    private ReminderSettings CloneDefaults()
    {
        return new ReminderSettings
        {
            Hour = _defaults.Hour,
            Minute = _defaults.Minute,
            TimeZoneId = _defaults.TimeZoneId
        };
    }

    private static void Validate(ReminderSettingsUpdateRequest request)
    {
        if (request.Hour is < 0 or > 23)
        {
            throw new ArgumentException("A hora do lembrete deve estar entre 0 e 23.");
        }

        if (request.Minute is < 0 or > 59)
        {
            throw new ArgumentException("O minuto do lembrete deve estar entre 0 e 59.");
        }

        if (!string.IsNullOrWhiteSpace(request.TimeZoneId))
        {
            try
            {
                _ = TimeZoneInfo.FindSystemTimeZoneById(request.TimeZoneId.Trim());
            }
            catch (TimeZoneNotFoundException)
            {
                throw new ArgumentException("O fuso horario do lembrete nao foi encontrado.");
            }
            catch (InvalidTimeZoneException)
            {
                throw new ArgumentException("O fuso horario do lembrete e invalido.");
            }
        }
    }

    private static string ResolveDataFilePath(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configuredPath = configuration["Data:FilePath"];
        return string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(environment.ContentRootPath, "data", "contas.json")
            : Path.GetFullPath(configuredPath, environment.ContentRootPath);
    }
}
