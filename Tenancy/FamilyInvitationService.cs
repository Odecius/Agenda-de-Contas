using AgendadorContas.Data;
using AgendadorContas.Data.Entities;
using AgendadorContas.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Data;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;

namespace AgendadorContas.Tenancy;

public sealed record CreatedFamilyInvitation(Guid Id, string Email, FamilyRole Role, DateTime ExpiresAtUtc, string Token)
{
    public override string ToString() => $"CreatedFamilyInvitation {{ Id = {Id}, Email = [REDACTED], Role = {Role}, ExpiresAtUtc = {ExpiresAtUtc:O}, Token = [REDACTED] }}";
}
public sealed record FamilyInvitationSummary(Guid Id, string Email, FamilyRole Role, DateTime ExpiresAtUtc, DateTime? AcceptedAtUtc, DateTime? RevokedAtUtc);
public sealed record AcceptedFamilyInvitation(Guid UserId, Guid FamilyId, bool UserCreated);
public sealed class FamilyInvitationConflictException(string message) : InvalidOperationException(message);

public interface IFamilyInvitationService
{
    Task<CreatedFamilyInvitation> CreateAsync(string email, FamilyRole role, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FamilyInvitationSummary>> ListAsync(CancellationToken cancellationToken = default);
    Task<bool> RevokeAsync(Guid invitationId, CancellationToken cancellationToken = default);
    Task<AcceptedFamilyInvitation?> AcceptAsync(string token, string email, string password, CancellationToken cancellationToken = default);
}

public sealed class FamilyInvitationService(
    AgendadorDbContext db,
    ICurrentFamilyContext currentFamily,
    UserManager<AppUser> userManager,
    IOptions<MultiFamilyOptions> options,
    TimeProvider timeProvider) : IFamilyInvitationService
{
    public async Task<CreatedFamilyInvitation> CreateAsync(
        string email,
        FamilyRole role,
        CancellationToken cancellationToken = default)
    {
        var tenant = await currentFamily.RequireAsync(cancellationToken);
        if (tenant.Role != FamilyRole.Owner)
        {
            throw new UnauthorizedAccessException("Only an Owner may create invitations.");
        }

        if (role is not (FamilyRole.Admin or FamilyRole.Member))
        {
            throw new ArgumentException("Invitation role must be Admin or Member.", nameof(role));
        }

        var normalized = NormalizeEmail(email);
        var canonicalEmail = email.Trim();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var existingUser = await userManager.FindByEmailAsync(canonicalEmail);
        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var invitation = new FamilyInvitation
        {
            Id = Guid.NewGuid(),
            FamilyId = tenant.FamilyId,
            Email = canonicalEmail,
            NormalizedEmail = normalized,
            Role = role,
            TokenHash = HashToken(token),
            CreatedByUserId = tenant.UserId,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddHours(options.Value.InvitationHours)
        };

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (existingUser is not null && await db.FamilyUsers.AsNoTracking().AnyAsync(
                x => x.FamilyId == tenant.FamilyId && x.UserId == existingUser.Id,
                cancellationToken))
        {
            throw new FamilyInvitationConflictException("This user already has a membership in the current family.");
        }

        var previousInvitations = await db.FamilyInvitations
            .Where(x => x.FamilyId == tenant.FamilyId
                && x.NormalizedEmail == normalized
                && x.AcceptedAtUtc == null
                && x.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var previous in previousInvitations)
        {
            previous.RevokedAtUtc = now;
        }

        db.FamilyInvitations.Add(invitation);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new CreatedFamilyInvitation(invitation.Id, invitation.Email, invitation.Role, invitation.ExpiresAtUtc, token);
    }

    public async Task<IReadOnlyList<FamilyInvitationSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        var tenant = await currentFamily.RequireAsync(cancellationToken);
        if (tenant.Role != FamilyRole.Owner)
        {
            throw new UnauthorizedAccessException("Only an Owner may list invitations.");
        }

        return await db.FamilyInvitations.AsNoTracking()
            .Where(x => x.FamilyId == tenant.FamilyId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new FamilyInvitationSummary(x.Id, x.Email, x.Role, x.ExpiresAtUtc, x.AcceptedAtUtc, x.RevokedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> RevokeAsync(Guid invitationId, CancellationToken cancellationToken = default)
    {
        var tenant = await currentFamily.RequireAsync(cancellationToken);
        if (tenant.Role != FamilyRole.Owner)
        {
            throw new UnauthorizedAccessException("Only an Owner may revoke invitations.");
        }

        var invitation = await db.FamilyInvitations.SingleOrDefaultAsync(
            x => x.FamilyId == tenant.FamilyId && x.Id == invitationId,
            cancellationToken);
        if (invitation is null)
        {
            return false;
        }

        if (invitation.AcceptedAtUtc is null && invitation.RevokedAtUtc is null)
        {
            invitation.RevokedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task<AcceptedFamilyInvitation?> AcceptAsync(
        string token,
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 200
            || string.IsNullOrWhiteSpace(password) || password.Length > 200)
        {
            return null;
        }

        string normalized;
        try
        {
            normalized = NormalizeEmail(email);
        }
        catch (ArgumentException)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var tokenHash = HashToken(token);
        var preview = await db.FamilyInvitations
            .AsNoTracking()
            .Include(x => x.Family)
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
        if (!IsUsable(preview, normalized, now))
        {
            return null;
        }

        var user = await userManager.FindByEmailAsync(preview!.Email);
        if (user is not null && (!user.IsActive || !await ValidateExistingPasswordAsync(user, password)))
        {
            return null;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var invitation = await LoadForAcceptanceAsync(tokenHash, cancellationToken);
        if (!IsUsable(invitation, normalized, now))
        {
            return null;
        }
        var validInvitation = invitation!;

        var userCreated = false;
        if (user is null)
        {
            user = new AppUser
            {
                Id = Guid.NewGuid(),
                Email = validInvitation.Email,
                UserName = validInvitation.Email,
                IsActive = true
            };
            var created = await userManager.CreateAsync(user, password);
            if (!created.Succeeded)
            {
                return null;
            }

            userCreated = true;
        }

        if (await db.FamilyUsers.AnyAsync(
                x => x.FamilyId == validInvitation.FamilyId && x.UserId == user.Id,
                cancellationToken))
        {
            return null;
        }

        var consumed = await db.FamilyInvitations
            .Where(x => x.Id == validInvitation.Id
                && x.AcceptedAtUtc == null
                && x.RevokedAtUtc == null
                && x.ExpiresAtUtc > now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.AcceptedAtUtc, now)
                .SetProperty(x => x.AcceptedByUserId, user.Id), cancellationToken);
        if (consumed != 1)
        {
            return null;
        }

        db.FamilyUsers.Add(new FamilyUser
        {
            FamilyId = validInvitation.FamilyId,
            UserId = user.Id,
            Role = validInvitation.Role,
            IsActive = true,
            JoinedAtUtc = now
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new AcceptedFamilyInvitation(user.Id, validInvitation.FamilyId, userCreated);
    }

    private async Task<FamilyInvitation?> LoadForAcceptanceAsync(
        string tokenHash,
        CancellationToken cancellationToken)
    {
        IQueryable<FamilyInvitation> query = db.FamilyInvitations;
        if (db.Database.IsNpgsql())
        {
            query = db.FamilyInvitations.FromSqlInterpolated(
                $"""SELECT * FROM "family_invitations" WHERE "TokenHash" = {tokenHash} FOR UPDATE""");
        }
        else
        {
            query = query.Where(x => x.TokenHash == tokenHash);
        }

        return await query
            .AsNoTracking()
            .Include(x => x.Family)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Trim().Length > 256)
        {
            throw new ArgumentException("A valid email is required.", nameof(email));
        }

        var canonicalEmail = email.Trim();
        if (!MailAddress.TryCreate(canonicalEmail, out var parsedEmail)
            || !string.Equals(parsedEmail.Address, canonicalEmail, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("A valid email is required.", nameof(email));
        }

        var normalized = userManager.NormalizeEmail(canonicalEmail);
        return string.IsNullOrWhiteSpace(normalized)
            ? throw new ArgumentException("A valid email is required.", nameof(email))
            : normalized;
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static bool IsUsable(FamilyInvitation? invitation, string normalizedEmail, DateTime now) =>
        invitation is not null
        && invitation.NormalizedEmail == normalizedEmail
        && invitation.AcceptedAtUtc is null
        && invitation.RevokedAtUtc is null
        && invitation.ExpiresAtUtc > now
        && invitation.Family.IsActive;

    private async Task<bool> ValidateExistingPasswordAsync(AppUser user, string password)
    {
        if (userManager.SupportsUserLockout && await userManager.IsLockedOutAsync(user))
        {
            return false;
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            if (userManager.SupportsUserLockout)
            {
                await userManager.AccessFailedAsync(user);
            }

            return false;
        }

        if (userManager.SupportsUserLockout && await userManager.GetAccessFailedCountAsync(user) > 0)
        {
            await userManager.ResetAccessFailedCountAsync(user);
        }

        return true;
    }
}
