using System.ComponentModel.DataAnnotations;

namespace AgendadorContas.Options;

public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    public bool Enabled { get; init; }

    public string BotToken { get; init; } = string.Empty;

    public string ChatId { get; init; } = string.Empty;

    [Required]
    public string ApiBaseUrl { get; init; } = "https://api.telegram.org";
}
