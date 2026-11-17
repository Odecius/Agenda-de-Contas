using Microsoft.Extensions.Configuration;

namespace AgendadorContas.Tenancy;

public static class PilotPasswordResetPolicy
{
    public static void EnsureAllowed(
        string environmentName,
        bool multiFamilyEnabled,
        bool pilotPasswordResetEnabled)
    {
        if (!multiFamilyEnabled)
        {
            throw new InvalidOperationException("Pilot password reset requires MultiFamily:Enabled=true.");
        }

        if (environmentName.Equals("Development", StringComparison.OrdinalIgnoreCase)
            || environmentName.Equals("Testing", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (environmentName.Equals("Pilot", StringComparison.OrdinalIgnoreCase))
        {
            if (pilotPasswordResetEnabled)
            {
                return;
            }

            throw new InvalidOperationException("Pilot password reset requires PilotPasswordReset:Enabled=true.");
        }

        throw new InvalidOperationException("Pilot password reset is not allowed in this environment.");
    }

    public static string RequireConfigurationValue(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{key} is required.");
        }

        return value;
    }
}
