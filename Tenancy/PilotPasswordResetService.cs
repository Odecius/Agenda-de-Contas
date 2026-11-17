using AgendadorContas.Data.Entities;
using Microsoft.AspNetCore.Identity;

namespace AgendadorContas.Tenancy;

public sealed class PilotPasswordResetService(
    UserManager<AppUser> userManager,
    ILogger<PilotPasswordResetService> logger)
{
    private const string IneligibleUserMessage =
        "Pilot password reset cannot continue for the requested identity.";

    public async Task ResetAsync(string email, string newPassword)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(newPassword);

        var user = await userManager.FindByEmailAsync(email.Trim());
        if (user is null || !user.IsActive)
        {
            throw new InvalidOperationException(IneligibleUserMessage);
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Code)));
        }

        logger.LogInformation("Pilot password reset completed successfully.");
    }
}
