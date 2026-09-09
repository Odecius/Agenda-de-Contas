using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace AgendadorContas.Options;

public sealed class PasswordRecoveryOptions
{
    public const string SectionName = "PasswordRecovery";
    public bool Enabled { get; set; } = true;
    [Required] public string PublicBaseUrl { get; set; } = "https://localhost";
    [Range(5, 1440)] public int TokenLifespanMinutes { get; set; } = 60;
}

public sealed class PasswordRecoveryOptionsValidator : IValidateOptions<PasswordRecoveryOptions>
{
    public ValidateOptionsResult Validate(string? name, PasswordRecoveryOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        if (!Uri.TryCreate(options.PublicBaseUrl, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(uri.Host)
            || Uri.CheckHostName(uri.Host) == UriHostNameType.Unknown
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            return ValidateOptionsResult.Fail(
                "PasswordRecovery:PublicBaseUrl must be an absolute HTTPS URL without credentials, query, or fragment.");
        }

        return ValidateOptionsResult.Success;
    }
}
