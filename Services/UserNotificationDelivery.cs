using AgendadorContas.Options;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace AgendadorContas.Services;

public enum UserNotificationKind { FamilyInvitation, PasswordRecovery }
public enum DeliveryStatus { Sent, Disabled, TemporaryFailure, PermanentFailure, InvalidConfiguration }
public sealed record UserNotificationMessage(UserNotificationKind Kind, string Destination, string ActionUrl, Guid CorrelationId);
public sealed record DeliveryResult(DeliveryStatus Status, int Attempts);

public interface IUserNotificationDeliveryService
{
    Task<DeliveryResult> DeliverAsync(UserNotificationMessage message, CancellationToken cancellationToken = default);
}

public interface IUserNotificationProvider
{
    Task<DeliveryStatus> SendAsync(UserNotificationMessage message, CancellationToken cancellationToken);
}

public sealed class SecureActionLinkFactory(IOptions<DeliveryOptions> options)
{
    public string Create(string path, IReadOnlyDictionary<string, string> fragmentValues)
    {
        var fragment = QueryHelpers.AddQueryString(
            string.Empty,
            fragmentValues.Select(x => new KeyValuePair<string, string?>(x.Key, x.Value))).TrimStart('?');
        return $"{options.Value.PublicBaseUrl.TrimEnd('/')}/{path.TrimStart('/')}#{fragment}";
    }
}

public sealed class UserNotificationDeliveryService(
    IOptions<DeliveryOptions> options,
    IUserNotificationProvider provider,
    ILogger<UserNotificationDeliveryService> logger) : IUserNotificationDeliveryService
{
    public async Task<DeliveryResult> DeliverAsync(UserNotificationMessage message, CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled) return new(DeliveryStatus.Disabled, 0);
        for (var attempt = 1; attempt <= options.Value.MaxRetries + 1; attempt++)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
            DeliveryStatus status;
            try { status = await provider.SendAsync(message, timeout.Token); }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { status = DeliveryStatus.TemporaryFailure; }
            catch (HttpRequestException) { status = DeliveryStatus.TemporaryFailure; }
            catch (Exception exception)
            {
                logger.LogWarning("User notification provider failed. Kind={Kind}; CorrelationId={CorrelationId}; ExceptionType={ExceptionType}", message.Kind, message.CorrelationId, exception.GetType().Name);
                status = DeliveryStatus.PermanentFailure;
            }

            if (status != DeliveryStatus.TemporaryFailure || attempt > options.Value.MaxRetries)
            {
                logger.LogInformation("User notification delivery completed. Kind={Kind}; Status={Status}; Attempts={Attempts}; CorrelationId={CorrelationId}", message.Kind, status, attempt, message.CorrelationId);
                return new(status, attempt);
            }
            await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), cancellationToken);
        }
        throw new InvalidOperationException("Unreachable delivery state.");
    }
}

public sealed class HttpEmailNotificationProvider(IHttpClientFactory clients, IOptions<DeliveryOptions> options) : IUserNotificationProvider
{
    public async Task<DeliveryStatus> SendAsync(UserNotificationMessage message, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled) return DeliveryStatus.Disabled;
        var client = clients.CreateClient("UserNotificationDelivery");
        using var request = new HttpRequestMessage(HttpMethod.Post, options.Value.Http.Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.Http.ApiKey);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", message.CorrelationId.ToString("N"));
        request.Content = JsonContent.Create(new
        {
            from = options.Value.Http.FromAddress,
            to = message.Destination,
            template = message.Kind.ToString(),
            actionUrl = message.ActionUrl
        });
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.IsSuccessStatusCode) return DeliveryStatus.Sent;
        return response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
            || (int)response.StatusCode >= 500 ? DeliveryStatus.TemporaryFailure : DeliveryStatus.PermanentFailure;
    }
}
