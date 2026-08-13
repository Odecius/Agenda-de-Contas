using AgendadorContas.Data.Entities;

namespace AgendadorContas.Tenancy;

public sealed record AuthorizedFamily(Guid FamilyId, string Name, FamilyRole Role);

public interface IFamilySelectionService
{
    Task<IReadOnlyList<AuthorizedFamily>> ListAsync(CancellationToken cancellationToken = default);
    Task<bool> SelectAsync(Guid familyId, CancellationToken cancellationToken = default);
    Task<AuthorizedFamily?> ResolveAsync(CancellationToken cancellationToken = default);
    void Clear();
}
