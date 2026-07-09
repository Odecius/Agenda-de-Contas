namespace AgendadorContas.Services;

public interface INotificationService
{
    Task<bool> SendAsync(string message, CancellationToken cancellationToken = default);
}
