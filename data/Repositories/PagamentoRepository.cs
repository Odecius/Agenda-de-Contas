using AgendadorContas.Data.Entities;
using AgendadorContas.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace AgendadorContas.Data.Repositories;

public sealed class PagamentoRepository(
    AgendadorDbContext db,
    ICurrentFamilyContext currentFamily) : IPagamentoRepository
{
    public async Task<IReadOnlyList<PagamentoEntity>> ListAsync(CancellationToken cancellationToken = default)
    {
        var tenant = await currentFamily.RequireAsync(cancellationToken);
        return await db.Pagamentos
            .AsNoTracking()
            .Where(x => x.FamilyId == tenant.FamilyId)
            .OrderByDescending(x => x.Ano)
            .ThenByDescending(x => x.Mes)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PagamentoEntity>?> ListForContaAsync(Guid contaId, CancellationToken cancellationToken = default)
    {
        var tenant = await currentFamily.RequireAsync(cancellationToken);
        if (!await db.Contas.AsNoTracking().AnyAsync(
                x => x.FamilyId == tenant.FamilyId && x.Id == contaId,
                cancellationToken))
        {
            return null;
        }

        return await db.Pagamentos
            .AsNoTracking()
            .Where(x => x.FamilyId == tenant.FamilyId && x.ContaId == contaId)
            .OrderByDescending(x => x.Ano)
            .ThenByDescending(x => x.Mes)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagamentoEntity?> GetByIdAsync(Guid pagamentoId, CancellationToken cancellationToken = default)
    {
        var tenant = await currentFamily.RequireAsync(cancellationToken);
        return await db.Pagamentos
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.FamilyId == tenant.FamilyId && x.Id == pagamentoId, cancellationToken);
    }

    public async Task<PagamentoEntity?> CreateAsync(
        Guid contaId,
        int ano,
        int mes,
        CancellationToken cancellationToken = default)
    {
        var tenant = await currentFamily.RequireAsync(cancellationToken);
        if (!await db.Contas.AsNoTracking().AnyAsync(
                x => x.FamilyId == tenant.FamilyId && x.Id == contaId,
                cancellationToken))
        {
            return null;
        }

        var entity = new PagamentoEntity
        {
            Id = Guid.NewGuid(),
            FamilyId = tenant.FamilyId,
            ContaId = contaId,
            Ano = ano,
            Mes = mes,
            PagoEmUtc = DateTime.UtcNow
        };
        db.Pagamentos.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<bool> DeleteAsync(Guid pagamentoId, CancellationToken cancellationToken = default)
    {
        var tenant = await currentFamily.RequireAsync(cancellationToken);
        var entity = await db.Pagamentos
            .SingleOrDefaultAsync(x => x.FamilyId == tenant.FamilyId && x.Id == pagamentoId, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        db.Pagamentos.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
