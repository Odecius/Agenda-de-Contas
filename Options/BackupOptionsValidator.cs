using Microsoft.Extensions.Options;

namespace AgendadorContas.Options;

public sealed class BackupOptionsValidator : IValidateOptions<BackupOptions>
{
    public ValidateOptionsResult Validate(string? name, BackupOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.TimeZoneId))
        {
            failures.Add("Backup:TimeZoneId e obrigatorio.");
        }

        if (options.MinimumBackupsToKeep < 1)
        {
            failures.Add("Backup:MinimumBackupsToKeep deve ser maior que zero.");
        }

        if (options.RetentionDays < 1)
        {
            failures.Add("Backup:RetentionDays deve ser maior que zero.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
