using AgendadorContas.Data.Entities;

namespace AgendadorContas.Data.Repositories;

public interface IPagamentoRepository
{
    Task<IReadOnlyList<PagamentoEntity>> ListAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PagamentoEntity>?> ListForContaAsync(Guid contaId, CancellationToken cancellationToken = default);
    Task<PagamentoEntity?> GetByIdAsync(Guid pagamentoId, CancellationToken cancellationToken = default);
    Task<PagamentoEntity?> CreateAsync(Guid contaId, int ano, int mes, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid pagamentoId, CancellationToken cancellationToken = default);
}
