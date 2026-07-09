using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using AgendadorContas.Models;

namespace AgendadorContas.Services;

public sealed class ReminderMessageBuilder : IReminderMessageBuilder
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("pt-PT");

    public string BuildDailyMessage(IReadOnlyList<ContaVencimento> vencimentos, DateOnly data)
    {
        if (vencimentos.Count == 0)
        {
            return $"<b>Bom dia!</b>{Environment.NewLine}Nao existem contas para pagar hoje ({data:dd/MM/yyyy}).";
        }

        var total = vencimentos.Sum(v => v.Conta.Valor);
        var message = new StringBuilder();

        message.AppendLine("<b>Bom dia!</b>");
        message.AppendLine($"Existem <b>{vencimentos.Count}</b> conta(s) para pagar hoje ({data:dd/MM/yyyy}):");
        message.AppendLine();

        foreach (var vencimento in vencimentos)
        {
            var nome = HtmlEncoder.Default.Encode(vencimento.Conta.Nome);
            var valor = vencimento.Conta.Valor.ToString("C", Culture);
            message.AppendLine($"- <b>{nome}</b>: {valor}");
        }

        message.AppendLine();
        message.Append($"Total do dia: <b>{total.ToString("C", Culture)}</b>");

        return message.ToString();
    }
}
