using AgendadorContas.Data.Entities;

namespace AgendadorContas.Tenancy;

public enum FamilyPermission
{
    ViewContas,
    CreateConta,
    EditConta,
    DeleteConta,
    ViewPagamentos,
    CreatePagamento,
    DeletePagamento
}

public interface IFamilyAuthorizationService
{
    Task<bool> IsAllowedAsync(FamilyPermission permission, CancellationToken cancellationToken = default);
}

public sealed class FamilyAuthorizationService(ICurrentFamilyContext currentFamily) : IFamilyAuthorizationService
{
    public async Task<bool> IsAllowedAsync(FamilyPermission permission, CancellationToken cancellationToken = default)
    {
        var family = await currentFamily.RequireAsync(cancellationToken);
        return family.Role switch
        {
            FamilyRole.Owner => true,
            FamilyRole.Admin => permission is FamilyPermission.ViewContas
                or FamilyPermission.CreateConta
                or FamilyPermission.EditConta
                or FamilyPermission.ViewPagamentos
                or FamilyPermission.CreatePagamento,
            FamilyRole.Member => permission is FamilyPermission.ViewContas
                or FamilyPermission.ViewPagamentos
                or FamilyPermission.CreatePagamento,
            _ => false
        };
    }
}
