using AgendadorContas.Data;
using AgendadorContas.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace AgendadorContas.Tenancy;

public sealed record FamilyRegistrationResult(bool Succeeded, Guid? UserId = null, Guid? FamilyId = null);

public sealed class FamilyRegistrationService(
    AgendadorDbContext db,
    UserManager<AppUser> users,
    ILogger<FamilyRegistrationService> logger)
{
    internal Func<CancellationToken, Task>? BeforeFamilyPersistence { get; set; }

    public async Task<FamilyRegistrationResult> RegisterAsync(
        string email,
        string password,
        string familyName,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email?.Trim() ?? string.Empty;
        var normalizedFamilyName = familyName?.Trim() ?? string.Empty;
        if (normalizedEmail.Length is < 3 or > 256
            || normalizedFamilyName.Length is < 2 or > 120
            || string.IsNullOrWhiteSpace(password))
        {
            return new(false);
        }

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var user = new AppUser
            {
                Id = Guid.NewGuid(),
                Email = normalizedEmail,
                UserName = normalizedEmail,
                IsActive = true
            };

            var identityResult = await users.CreateAsync(user, password);
            if (!identityResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new FamilyRegistrationResult(false);
            }

            try
            {
                if (BeforeFamilyPersistence is not null)
                {
                    await BeforeFamilyPersistence(cancellationToken);
                }

                var family = new Family { Id = Guid.NewGuid(), Name = normalizedFamilyName };
                db.Families.Add(family);
                db.FamilyUsers.Add(new FamilyUser
                {
                    FamilyId = family.Id,
                    UserId = user.Id,
                    Role = FamilyRole.Owner,
                    IsActive = true
                });
                db.FamilySettings.Add(new FamilySettings { FamilyId = family.Id });
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                logger.LogInformation("Registration created user {UserId} and family {FamilyId} with an initial Owner.", user.Id, family.Id);
                return new FamilyRegistrationResult(true, user.Id, family.Id);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }
}
