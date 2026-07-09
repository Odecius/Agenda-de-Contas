using System.Globalization;
using AgendadorContas.Models;

namespace AgendadorContas.Services;

public sealed class MoneyFormatter : IMoneyFormatter
{
    public string Format(decimal amount, AccountCurrency currency)
    {
        var culture = currency switch
        {
            AccountCurrency.GBP => CultureInfo.GetCultureInfo("en-GB"),
            AccountCurrency.EUR => CultureInfo.GetCultureInfo("pt-PT"),
            AccountCurrency.BRL => CultureInfo.GetCultureInfo("pt-BR"),
            _ => CultureInfo.InvariantCulture
        };

        return amount.ToString("C", culture);
    }
}
