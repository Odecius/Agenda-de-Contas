using AgendadorContas.Data;
using Microsoft.EntityFrameworkCore;

namespace AgendadorContas.Tenancy;

public sealed class FamilySelectionService(
    AgendadorDbContext db,
    ICurrentUserContext currentUser,
    IHttpContextAccessor httpContextAccessor) : IFamilySelectionService
{
    private const string SessionKey = "MultiFamily.ActiveFamilyId";
    private const string SessionUserKey = "MultiFamily.ActiveFamilyUserId";

    public async Task<IReadOnlyList<AuthorizedFamily>> ListAsync(CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequireUserId();
        return await db.FamilyUsers
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.IsActive && x.User.IsActive && x.Family.IsActive)
            .OrderBy(x => x.Family.Name)
            .Select(x => new AuthorizedFamily(x.FamilyId, x.Family.Name, x.Role))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> SelectAsync(Guid familyId, CancellationToken cancellationToken = default)
    {
        var authorized = await FindAuthorizedAsync(familyId, cancellationToken);
        if (authorized is null)
        {
            return false;
        }

        Session.SetString(SessionKey, familyId.ToString("D"));
        Session.SetString(SessionUserKey, currentUser.RequireUserId().ToString("D"));
        return true;
    }

    public async Task<AuthorizedFamily?> ResolveAsync(CancellationToken cancellationToken = default)
    {
        var memberships = await ListAsync(cancellationToken);
        if (memberships.Count == 0)
        {
            Clear();
            return null;
        }

        var selectedValue = Session.GetString(SessionKey);
        var selectedUserValue = Session.GetString(SessionUserKey);
        var currentUserId = currentUser.RequireUserId();
        if (selectedValue is not null || selectedUserValue is not null)
        {
            if (Guid.TryParse(selectedValue, out var selectedFamilyId)
                && Guid.TryParse(selectedUserValue, out var selectedUserId)
                && selectedUserId == currentUserId)
            {
                var selected = memberships.SingleOrDefault(x => x.FamilyId == selectedFamilyId);
                if (selected is not null)
                {
                    return selected;
                }
            }

            Clear();
            return null;
        }

        if (memberships.Count == 1)
        {
            var onlyFamily = memberships[0];
            Session.SetString(SessionKey, onlyFamily.FamilyId.ToString("D"));
            Session.SetString(SessionUserKey, currentUserId.ToString("D"));
            return onlyFamily;
        }

        return null;
    }

    public void Clear()
    {
        Session.Remove(SessionKey);
        Session.Remove(SessionUserKey);
    }

    private async Task<AuthorizedFamily?> FindAuthorizedAsync(Guid familyId, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        return await db.FamilyUsers
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.FamilyId == familyId && x.IsActive && x.User.IsActive && x.Family.IsActive)
            .Select(x => new AuthorizedFamily(x.FamilyId, x.Family.Name, x.Role))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private ISession Session => httpContextAccessor.HttpContext?.Session
        ?? throw new InvalidOperationException("An active server session is required.");
}
