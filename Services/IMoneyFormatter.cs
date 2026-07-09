using AgendadorContas.Models;

namespace AgendadorContas.Services;

public interface IMoneyFormatter
{
    string Format(decimal amount, AccountCurrency currency);
}
