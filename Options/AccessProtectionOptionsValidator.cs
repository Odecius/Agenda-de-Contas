using Microsoft.Extensions.Options;

namespace AgendadorContas.Options;

public sealed class AccessProtectionOptionsValidator : IValidateOptions<AccessProtectionOptions>
{
    public ValidateOptionsResult Validate(string? name, AccessProtectionOptions options)
    {
        if (options.SessionHours is < 1 or > 168)
        {
            return ValidateOptionsResult.Fail("AccessProtection:SessionHours deve estar entre 1 e 168.");
        }

        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        if (string.IsNullOrWhiteSpace(options.Username))
        {
            return ValidateOptionsResult.Fail("AccessProtection:Username e obrigatorio quando a protecao esta ativa.");
        }

        if (string.IsNullOrWhiteSpace(options.Password))
        {
            return ValidateOptionsResult.Fail("AccessProtection:Password e obrigatorio quando a protecao esta ativa.");
        }

        return ValidateOptionsResult.Success;
    }
}
