using AgendadorContas.Data;
using AgendadorContas.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace AgendadorContas.Tenancy;

public sealed record BootstrapResult(Guid UserId, Guid FamilyId, bool UserCreated, bool FamilyCreated, bool MembershipCreated);

public sealed class MultiFamilyBootstrapService(
    AgendadorDbContext db,
    UserManager<AppUser> userManager,
    ILogger<MultiFamilyBootstrapService> logger)
{
    public async Task<BootstrapResult> BootstrapAsync(
        string email,
        string password,
        string familyName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentException.ThrowIfNullOrWhiteSpace(familyName);
        var normalizedEmail = email.Trim();
        var normalizedFamilyName = familyName.Trim();
        var familyNameKey = normalizedFamilyName.ToUpperInvariant();
        if (normalizedFamilyName.Length > 120) throw new ArgumentException("Family name is too long.", nameof(familyName));

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var user = await userManager.FindByEmailAsync(normalizedEmail);
        var matchingFamilies = await db.Families.Where(x => x.Name.ToUpper() == familyNameKey).Take(2).ToListAsync(cancellationToken);
        if (matchingFamilies.Count > 1)
        {
            throw new InvalidOperationException("Bootstrap identity conflicts with existing state; explicit administrative intervention is required.");
        }

        var family = matchingFamilies.SingleOrDefault();
        if (user is null && family is null)
        {
            user = new AppUser { Id = Guid.NewGuid(), Email = normalizedEmail, UserName = normalizedEmail, IsActive = true };
            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Code)));
            }
            family = new Family { Id = Guid.NewGuid(), Name = normalizedFamilyName };
            db.Families.Add(family);
            db.FamilyUsers.Add(new FamilyUser { FamilyId = family.Id, UserId = user.Id, Role = FamilyRole.Owner });
            db.FamilySettings.Add(new FamilySettings { FamilyId = family.Id });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            logger.LogInformation("Multi-family bootstrap created user {UserId} and family {FamilyId}.", user.Id, family.Id);
            return new BootstrapResult(user.Id, family.Id, true, true, true);
        }

        if (user is null || family is null || !user.IsActive || !family.IsActive)
        {
            throw new InvalidOperationException("Bootstrap identity conflicts with existing state; explicit administrative intervention is required.");
        }

        var membership = await db.FamilyUsers.SingleOrDefaultAsync(
            x => x.FamilyId == family.Id && x.UserId == user.Id,
            cancellationToken);
        var hasSettings = await db.FamilySettings.AnyAsync(x => x.FamilyId == family.Id, cancellationToken);
        if (membership is null || membership.Role != FamilyRole.Owner || !hasSettings)
        {
            throw new InvalidOperationException("Bootstrap identity conflicts with existing state; explicit administrative intervention is required.");
        }

        if (!membership.IsActive)
        {
            var anotherActiveOwnerExists = await db.FamilyUsers.AnyAsync(
                x => x.FamilyId == family.Id && x.UserId != user.Id && x.IsActive && x.Role == FamilyRole.Owner,
                cancellationToken);
            if (anotherActiveOwnerExists)
            {
                throw new InvalidOperationException("Bootstrap identity conflicts with existing state; explicit administrative intervention is required.");
            }

            membership.IsActive = true;
            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Multi-family bootstrap confirmed user {UserId} and family {FamilyId}.", user.Id, family.Id);
        return new BootstrapResult(user.Id, family.Id, false, false, false);
    }
}
