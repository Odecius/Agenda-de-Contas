using Microsoft.Extensions.Configuration;

namespace AgendadorContas.Tenancy;

public static class PilotBootstrapPolicy
{
    public static void EnsureAllowed(
        string environmentName,
        bool multiFamilyEnabled,
        bool pilotBootstrapEnabled)
    {
        if (!multiFamilyEnabled)
        {
            throw new InvalidOperationException("Multi-family bootstrap requires MultiFamily:Enabled=true.");
        }

        if (environmentName.Equals("Development", StringComparison.OrdinalIgnoreCase)
            || environmentName.Equals("Testing", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (environmentName.Equals("Pilot", StringComparison.OrdinalIgnoreCase))
        {
            if (pilotBootstrapEnabled)
            {
                return;
            }

            throw new InvalidOperationException("Pilot bootstrap requires PilotBootstrap:Enabled=true.");
        }

        throw new InvalidOperationException("Multi-family bootstrap is not allowed in this environment.");
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
