namespace AgendadorContas.Data.Entities;

public sealed class PagamentoEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FamilyId { get; set; }
    public Guid ContaId { get; set; }
    public int Ano { get; set; }
    public int Mes { get; set; }
    public DateTime PagoEmUtc { get; set; } = DateTime.UtcNow;
    public Family Family { get; set; } = null!;
    public ContaEntity Conta { get; set; } = null!;
}
