using AgendadorContas.Models;

namespace AgendadorContas.Services;

public interface IReminderMessageBuilder
{
    string BuildDailyMessage(IReadOnlyList<ContaVencimento> vencimentos, DateOnly data);
}
