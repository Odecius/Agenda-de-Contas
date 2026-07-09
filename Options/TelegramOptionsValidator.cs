using Microsoft.Extensions.Options;

namespace AgendadorContas.Options;

public sealed class TelegramOptionsValidator : IValidateOptions<TelegramOptions>
{
    public ValidateOptionsResult Validate(string? name, TelegramOptions options)
    {
        var failures = new List<string>();

        if (!Uri.TryCreate(options.ApiBaseUrl, UriKind.Absolute, out var apiBaseUri)
            || apiBaseUri.Scheme is not ("http" or "https"))
        {
            failures.Add("Telegram:ApiBaseUrl deve ser uma URL absoluta http ou https.");
        }

        if (options.Enabled)
        {
            if (string.IsNullOrWhiteSpace(options.BotToken))
            {
                failures.Add("Telegram:BotToken e obrigatorio quando Telegram:Enabled for true.");
            }

            if (string.IsNullOrWhiteSpace(options.ChatId))
            {
                failures.Add("Telegram:ChatId e obrigatorio quando Telegram:Enabled for true.");
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
