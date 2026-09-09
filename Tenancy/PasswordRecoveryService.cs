using AgendadorContas.Data.Entities;
using AgendadorContas.Options;
using AgendadorContas.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace AgendadorContas.Tenancy;

public sealed class PasswordRecoveryAttemptLimiter(TimeProvider timeProvider)
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    private readonly ConcurrentDictionary<string, AttemptWindow> attempts = new(StringComparer.Ordinal);
    public bool AllowEmail(string value) => Allow("email:" + Hash(value), 3);
    public bool AllowToken(string value) => Allow("token:" + Hash(value), 5);

    private bool Allow(string key, int limit)
    {
        var now = timeProvider.GetUtcNow();
        while (true)
        {
            var current = attempts.GetOrAdd(key, _ => new AttemptWindow(now, 0));
            var next = now - current.Start >= Window ? new AttemptWindow(now, 1) : current with { Count = current.Count + 1 };
            if (attempts.TryUpdate(key, next, current)) return next.Count <= limit;
        }
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private sealed record AttemptWindow(DateTimeOffset Start, int Count);
}

public sealed class PasswordRecoveryService(
    UserManager<AppUser> users,
    IUserNotificationDeliveryService delivery,
    SecureActionLinkFactory links,
    IOptions<PasswordRecoveryOptions> options,
    PasswordRecoveryAttemptLimiter limiter)
{
    public const string GenericResponse = "Se existir uma conta elegivel, enviaremos instrucoes para recuperacao.";

    public async Task RequestAsync(string? email, CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled) return;
        var canonical = email?.Trim() ?? string.Empty;
        var normalized = users.NormalizeEmail(canonical);
        if (string.IsNullOrWhiteSpace(normalized) || !limiter.AllowEmail(normalized)) return;
        var user = await users.FindByEmailAsync(canonical);
        if (user is null || !user.IsActive) return;
        var token = await users.GeneratePasswordResetTokenAsync(user);
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var url = links.Create("reset-password.html", new Dictionary<string, string>
        {
            ["token"] = encoded,
            ["email"] = user.Email!
        });
        _ = await delivery.DeliverAsync(new UserNotificationMessage(
            UserNotificationKind.PasswordRecovery, user.Email!, url, Guid.NewGuid()), cancellationToken);
    }

    public async Task<bool> ResetAsync(string? email, string? encodedToken, string? newPassword)
    {
        if (!options.Value.Enabled) return false;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(encodedToken) || encodedToken.Length > 4096
            || string.IsNullOrWhiteSpace(newPassword) || newPassword.Length > 200 || !limiter.AllowToken(encodedToken)) return false;
        var user = await users.FindByEmailAsync(email.Trim());
        if (user is null || !user.IsActive) return false;
        string token;
        try { token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encodedToken)); }
        catch (FormatException) { return false; }
        return (await users.ResetPasswordAsync(user, token, newPassword)).Succeeded;
    }
}
