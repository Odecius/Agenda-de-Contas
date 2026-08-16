using AgendadorContas.Data.Entities;
using AgendadorContas.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace AgendadorContas.Data.Repositories;

public sealed class ContaRepository(
    AgendadorDbContext db,
    ICurrentFamilyContext currentFamily) : IContaRepository
{
    public async Task<IReadOnlyList<ContaEntity>> ListAsync(CancellationToken cancellationToken = default)
    {
        var tenant = await currentFamily.RequireAsync(cancellationToken);
        return await db.Contas
            .AsNoTracking()
            .Where(x => x.FamilyId == tenant.FamilyId)
            .OrderBy(x => x.Nome)
            .ToListAsync(cancellationToken);
    }

    public async Task<ContaEntity?> GetByIdAsync(Guid contaId, CancellationToken cancellationToken = default)
    {
        var tenant = await currentFamily.RequireAsync(cancellationToken);
        return await db.Contas
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.FamilyId == tenant.FamilyId && x.Id == contaId, cancellationToken);
    }

    public async Task<ContaEntity> CreateAsync(ContaWriteModel model, CancellationToken cancellationToken = default)
    {
        var tenant = await currentFamily.RequireAsync(cancellationToken);
        var entity = new ContaEntity { Id = Guid.NewGuid(), FamilyId = tenant.FamilyId };
        Apply(entity, model);
        db.Contas.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<ContaEntity?> UpdateAsync(Guid contaId, ContaWriteModel model, CancellationToken cancellationToken = default)
    {
        var tenant = await currentFamily.RequireAsync(cancellationToken);
        var entity = await db.Contas
            .SingleOrDefaultAsync(x => x.FamilyId == tenant.FamilyId && x.Id == contaId, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        Apply(entity, model);
        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<bool> DeleteAsync(Guid contaId, CancellationToken cancellationToken = default)
    {
        var tenant = await currentFamily.RequireAsync(cancellationToken);
        var entity = await db.Contas
            .SingleOrDefaultAsync(x => x.FamilyId == tenant.FamilyId && x.Id == contaId, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        db.Contas.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static void Apply(ContaEntity entity, ContaWriteModel model)
    {
        entity.Nome = model.Nome.Trim();
        entity.Valor = model.Valor;
        entity.Country = model.Country;
        entity.Currency = model.Currency;
        entity.DiaVencimento = model.DiaVencimento;
        entity.DataInicio = model.DataInicio;
        entity.DuracaoMeses = model.DuracaoMeses;
        entity.Ativa = model.Ativa;
        entity.Observacoes = string.IsNullOrWhiteSpace(model.Observacoes) ? null : model.Observacoes.Trim();
    }
}
