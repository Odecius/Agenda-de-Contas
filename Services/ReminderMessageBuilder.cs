using System.Text;
using System.Text.Encodings.Web;
using AgendadorContas.Models;

namespace AgendadorContas.Services;

public sealed class ReminderMessageBuilder : IReminderMessageBuilder
{
    private readonly IMoneyFormatter _moneyFormatter;

    public ReminderMessageBuilder(IMoneyFormatter moneyFormatter)
    {
        _moneyFormatter = moneyFormatter;
    }

    public string BuildDailyMessage(IReadOnlyList<ContaVencimento> vencimentos, DateOnly data)
    {
        if (vencimentos.Count == 0)
        {
            return $"<b>Bom dia!</b>{Environment.NewLine}Nao existem contas para pagar hoje ({data:dd/MM/yyyy}).";
        }

        var message = new StringBuilder();

        message.AppendLine("<b>Bom dia!</b>");
        message.AppendLine($"Existem <b>{vencimentos.Count}</b> conta(s) para pagar hoje ({data:dd/MM/yyyy}):");
        message.AppendLine();

        foreach (var vencimento in vencimentos)
        {
            var nome = HtmlEncoder.Default.Encode(vencimento.Conta.Nome);
            var valor = _moneyFormatter.Format(vencimento.Conta.Valor, vencimento.Conta.Currency);
            message.AppendLine($"- <b>{nome}</b>: {valor}");
        }

        message.AppendLine();
        message.AppendLine("Total do dia:");

        foreach (var totalPorMoeda in vencimentos.GroupBy(v => v.Conta.Currency))
        {
            var total = totalPorMoeda.Sum(v => v.Conta.Valor);
            message.AppendLine($"- <b>{_moneyFormatter.Format(total, totalPorMoeda.Key)}</b>");
        }

        return message.ToString();
    }
}
