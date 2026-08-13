using AgendadorContas.Models;

namespace AgendadorContas.Data.Entities;

public sealed class ContaEntity
{
    public Guid Id { get; set; }
    public Guid FamilyId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public AccountCountry Country { get; set; }
    public AccountCurrency Currency { get; set; }
    public int DiaVencimento { get; set; }
    public DateOnly DataInicio { get; set; }
    public int DuracaoMeses { get; set; }
    public bool Ativa { get; set; } = true;
    public string? Observacoes { get; set; }
    public Family Family { get; set; } = null!;
    public ICollection<PagamentoEntity> Pagamentos { get; set; } = [];
}
