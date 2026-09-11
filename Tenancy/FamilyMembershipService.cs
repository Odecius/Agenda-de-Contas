using AgendadorContas.Data;
using AgendadorContas.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace AgendadorContas.Tenancy;

public enum MembershipChangeResult { Success, NotFound, Forbidden, LastOwner }

public sealed class FamilyMembershipService(
    AgendadorDbContext db,
    ICurrentFamilyContext current,
    ILogger<FamilyMembershipService> logger)
{
    public async Task<MembershipChangeResult> ChangeRoleAsync(Guid userId, FamilyRole role, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(role)) return MembershipChangeResult.Forbidden;
        return await MutateAsync(userId, role, deactivate: false, cancellationToken);
    }

    public Task<MembershipChangeResult> RemoveAsync(Guid userId, CancellationToken cancellationToken = default) =>
        MutateAsync(userId, null, deactivate: true, cancellationToken);

    private async Task<MembershipChangeResult> MutateAsync(Guid userId, FamilyRole? role, bool deactivate, CancellationToken cancellationToken)
    {
        var tenant = await current.RequireAsync(cancellationToken);

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            if (db.Database.IsNpgsql())
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT 1 FROM family_users WHERE \"FamilyId\" = {tenant.FamilyId} AND \"IsActive\" AND \"Role\" = 'Owner' FOR UPDATE",
                    cancellationToken);
            }

            var actorIsOwner = await db.FamilyUsers.AnyAsync(
                x => x.FamilyId == tenant.FamilyId && x.UserId == tenant.UserId && x.IsActive && x.Role == FamilyRole.Owner,
                cancellationToken);
            if (!actorIsOwner) return MembershipChangeResult.Forbidden;

            var membership = await db.FamilyUsers
                .SingleOrDefaultAsync(x => x.FamilyId == tenant.FamilyId && x.UserId == userId, cancellationToken);
            if (membership is null) return MembershipChangeResult.NotFound;

            if (membership.IsActive && membership.Role == FamilyRole.Owner && (deactivate || role != FamilyRole.Owner))
            {
                var ownerCount = await db.FamilyUsers.CountAsync(
                    x => x.FamilyId == tenant.FamilyId && x.IsActive && x.Role == FamilyRole.Owner,
                    cancellationToken);
                if (ownerCount <= 1) return MembershipChangeResult.LastOwner;
            }

            membership.IsActive = !deactivate;
            if (role.HasValue) membership.Role = role.Value;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            logger.LogInformation("Owner {ActorUserId} changed membership {TargetUserId} in family {FamilyId}; action {Action}.",
                tenant.UserId, userId, tenant.FamilyId, deactivate ? "removed" : "role-changed");
            return MembershipChangeResult.Success;
        });
    }
}
