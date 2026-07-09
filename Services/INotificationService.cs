namespace AgendadorContas.Services;

public interface INotificationService
{
    Task SendAsync(string message, CancellationToken cancellationToken = default);
}
