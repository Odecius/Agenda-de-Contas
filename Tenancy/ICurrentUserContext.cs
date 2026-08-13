namespace AgendadorContas.Tenancy;

public interface ICurrentUserContext
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    Guid RequireUserId();
}
