using System.Security.Claims;

namespace AgendadorContas.Tenancy;

public sealed class CurrentUserContext(IHttpContextAccessor httpContextAccessor) : ICurrentUserContext
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true && UserId.HasValue;

    public Guid? UserId
    {
        get
        {
            var value = Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var userId) ? userId : null;
        }
    }

    public Guid RequireUserId() => UserId is { } userId && Principal?.Identity?.IsAuthenticated == true
        ? userId
        : throw new UnauthorizedAccessException("Authenticated user is required.");
}
