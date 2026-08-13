using AgendadorContas.Data.Entities;

namespace AgendadorContas.Tenancy;

public sealed record CurrentFamily(Guid FamilyId, Guid UserId, FamilyRole Role);

public interface ICurrentFamilyContext
{
    Task<CurrentFamily> RequireAsync(CancellationToken cancellationToken = default);
}
