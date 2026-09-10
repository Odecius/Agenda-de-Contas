using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace AgendadorContas.Options;

public sealed class DeliveryOptions
{
    public const string SectionName = "Delivery";
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "Disabled";
    [Required, Url] public string PublicBaseUrl { get; set; } = "https://localhost";
    [Range(1, 30)] public int TimeoutSeconds { get; set; } = 10;
    [Range(0, 3)] public int MaxRetries { get; set; } = 2;
    public HttpDeliveryOptions Http { get; set; } = new();
}

public sealed class HttpDeliveryOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
}

public sealed class DeliveryOptionsValidator : IValidateOptions<DeliveryOptions>
{
    public ValidateOptionsResult Validate(string? name, DeliveryOptions options)
    {
        if (!options.Enabled) return ValidateOptionsResult.Success;
        if (!string.Equals(options.Provider, "HttpEmail", StringComparison.Ordinal))
            return ValidateOptionsResult.Fail("Delivery provider is unsupported.");
        if (!IsSafeHttpsOrigin(options.PublicBaseUrl))
            return ValidateOptionsResult.Fail("Delivery public base URL must be an absolute HTTPS URL without credentials, query, or fragment.");
        if (!IsSafeHttpsEndpoint(options.Http.Endpoint))
            return ValidateOptionsResult.Fail("Delivery HTTP endpoint must be an absolute HTTPS URL without credentials or fragment.");
        if (string.IsNullOrWhiteSpace(options.Http.ApiKey) || string.IsNullOrWhiteSpace(options.Http.FromAddress))
            return ValidateOptionsResult.Fail("Delivery HTTP credentials and sender must be supplied externally when enabled.");
        return ValidateOptionsResult.Success;
    }

    private static bool IsSafeHttpsOrigin(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(uri.Host)
        && Uri.CheckHostName(uri.Host) != UriHostNameType.Unknown
        && string.IsNullOrEmpty(uri.UserInfo)
        && string.IsNullOrEmpty(uri.Query)
        && string.IsNullOrEmpty(uri.Fragment);

    private static bool IsSafeHttpsEndpoint(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(uri.Host)
        && Uri.CheckHostName(uri.Host) != UriHostNameType.Unknown
        && string.IsNullOrEmpty(uri.UserInfo)
        && string.IsNullOrEmpty(uri.Fragment);
}
