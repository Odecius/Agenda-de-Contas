namespace AgendadorContas.Tenancy;

public sealed class CurrentFamilyContext(
    ICurrentUserContext currentUser,
    IFamilySelectionService familySelection) : ICurrentFamilyContext
{
    public async Task<CurrentFamily> RequireAsync(CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequireUserId();
        var family = await familySelection.ResolveAsync(cancellationToken)
            ?? throw new InvalidOperationException("An authorized active family must be selected.");
        return new CurrentFamily(family.FamilyId, userId, family.Role);
    }
}
