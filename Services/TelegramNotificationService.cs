using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AgendadorContas.Options;
using Microsoft.Extensions.Options;

namespace AgendadorContas.Services;

public sealed class TelegramNotificationService : INotificationService
{
    private const string HttpClientName = "Telegram";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TelegramOptions _options;
    private readonly ILogger<TelegramNotificationService> _logger;

    public TelegramNotificationService(
        IHttpClientFactory httpClientFactory,
        IOptions<TelegramOptions> options,
        ILogger<TelegramNotificationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> SendAsync(string message, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Telegram esta desativado. A notificacao nao sera enviada.");
            return false;
        }

        var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        var endpoint = $"/bot{_options.BotToken}/sendMessage";
        var payload = new TelegramSendMessageRequest(_options.ChatId, message, "HTML");

        using var response = await httpClient.PostAsJsonAsync(endpoint, payload, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation("Mensagem enviada pelo Telegram com sucesso.");
            return true;
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogWarning(
            "Falha ao enviar mensagem pelo Telegram. Status: {StatusCode}. Resposta: {ResponseBody}",
            response.StatusCode,
            responseBody);

        throw new InvalidOperationException(
            $"Falha ao enviar mensagem pelo Telegram. Status: {(int)response.StatusCode} {response.ReasonPhrase}. Resposta: {responseBody}");
    }

    private sealed record TelegramSendMessageRequest(
        [property: JsonPropertyName("chat_id")] string ChatId,
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("parse_mode")] string ParseMode);
}
