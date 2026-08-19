using AgendadorContas.Data;
using AgendadorContas.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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
        var normalizedFamilyName = familyName.Trim();
        if (normalizedFamilyName.Length > 120) throw new ArgumentException("Family name is too long.", nameof(familyName));

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var user = await userManager.FindByEmailAsync(email.Trim());
        var userCreated = false;
        if (user is null)
        {
            user = new AppUser { Id = Guid.NewGuid(), Email = email.Trim(), UserName = email.Trim(), IsActive = true };
            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Code)));
            }
            userCreated = true;
        }

        var family = await db.Families.SingleOrDefaultAsync(x => x.Name == normalizedFamilyName, cancellationToken);
        var familyCreated = false;
        if (family is null)
        {
            family = new Family { Id = Guid.NewGuid(), Name = normalizedFamilyName };
            db.Families.Add(family);
            familyCreated = true;
        }

        var membership = await db.FamilyUsers.FindAsync([family.Id, user.Id], cancellationToken);
        var membershipCreated = false;
        if (membership is null)
        {
            membership = new FamilyUser { FamilyId = family.Id, UserId = user.Id, Role = FamilyRole.Owner };
            db.FamilyUsers.Add(membership);
            membershipCreated = true;
        }
        else if (membership.Role != FamilyRole.Owner || !membership.IsActive)
        {
            membership.Role = FamilyRole.Owner;
            membership.IsActive = true;
        }

        if (!await db.FamilySettings.AnyAsync(x => x.FamilyId == family.Id, cancellationToken))
        {
            db.FamilySettings.Add(new FamilySettings { FamilyId = family.Id });
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Multi-family bootstrap completed for user {UserId} and family {FamilyId}.", user.Id, family.Id);
        return new BootstrapResult(user.Id, family.Id, userCreated, familyCreated, membershipCreated);
    }
}
