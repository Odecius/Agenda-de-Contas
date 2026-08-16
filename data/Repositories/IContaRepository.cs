using AgendadorContas.Data.Entities;
using AgendadorContas.Models;

namespace AgendadorContas.Data.Repositories;

public sealed record ContaWriteModel(
    string Nome,
    decimal Valor,
    AccountCountry Country,
    AccountCurrency Currency,
    int DiaVencimento,
    DateOnly DataInicio,
    int DuracaoMeses,
    bool Ativa,
    string? Observacoes);

public interface IContaRepository
{
    Task<IReadOnlyList<ContaEntity>> ListAsync(CancellationToken cancellationToken = default);
    Task<ContaEntity?> GetByIdAsync(Guid contaId, CancellationToken cancellationToken = default);
    Task<ContaEntity> CreateAsync(ContaWriteModel model, CancellationToken cancellationToken = default);
    Task<ContaEntity?> UpdateAsync(Guid contaId, ContaWriteModel model, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid contaId, CancellationToken cancellationToken = default);
}
