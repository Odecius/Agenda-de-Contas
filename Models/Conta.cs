namespace AgendadorContas.Models;

public enum AccountCountry
{
    UnitedKingdom = 0,
    Portugal = 1,
    Brazil = 2
}

public enum AccountCurrency
{
    GBP = 0,
    EUR = 1,
    BRL = 2
}

public sealed class Conta
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nome { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public AccountCountry Country { get; set; } = AccountCountry.UnitedKingdom;
    public AccountCurrency Currency { get; set; } = AccountCurrency.GBP;
    public int DiaVencimento { get; set; }
    public DateOnly DataInicio { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public int DuracaoMeses { get; set; }
    public bool Ativa { get; set; } = true;
    public string? Observacoes { get; set; }
}

public sealed class Pagamento
{
    public Guid ContaId { get; set; }
    public int Ano { get; set; }
    public int Mes { get; set; }
    public DateTime PagoEm { get; set; } = DateTime.UtcNow;
}

public sealed class LembreteEnviado
{
    public DateOnly Data { get; set; }
    public DateTime EnviadoEm { get; set; } = DateTime.UtcNow;
}

public sealed class ContaVencimento
{
    public required Conta Conta { get; init; }
    public required DateOnly DataVencimento { get; init; }
    public required bool Pago { get; init; }
    public DateTime? PagoEm { get; init; }
}

public sealed class ContaCreateRequest
{
    public string Nome { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public AccountCountry Country { get; set; } = AccountCountry.UnitedKingdom;
    public AccountCurrency Currency { get; set; } = AccountCurrency.GBP;
    public int DiaVencimento { get; set; }
    public DateOnly DataInicio { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public int DuracaoMeses { get; set; }
    public string? Observacoes { get; set; }
}
