using AgendadorContas.Data.Entities;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;

namespace AgendadorContas.Tenancy;

public sealed record IdentityLoginRequest(string Email, string Password);
public sealed record FamilySelectionRequest(Guid FamilyId);

public static class MultiFamilyEndpointExtensions
{
    public static IEndpointRouteBuilder MapMultiFamilyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/multi-family");

        group.MapGet("/antiforgery/token", (HttpContext context, IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(context);
            return Results.Ok(new { token = tokens.RequestToken });
        }).AllowAnonymous();

        group.MapPost("/auth/login", async (
            IdentityLoginRequest request,
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            LoginTimingProtector timingProtector) =>
        {
            var user = await userManager.FindByEmailAsync(request.Email);
            if (user is null)
            {
                timingProtector.Verify(request.Password);
                return Results.Unauthorized();
            }

            if (!user.IsActive)
            {
                return Results.Unauthorized();
            }

            var result = await signInManager.PasswordSignInAsync(user, request.Password, false, lockoutOnFailure: true);
            return result.Succeeded ? Results.Ok(new { sucesso = true }) : Results.Unauthorized();
        }).AllowAnonymous().RequireRateLimiting("multi-family-login").RequireAntiforgeryValidation();

        group.MapPost("/auth/logout", async (SignInManager<AppUser> signInManager, IFamilySelectionService selection) =>
        {
            selection.Clear();
            await signInManager.SignOutAsync();
            return Results.Ok(new { sucesso = true });
        }).RequireAuthorization().RequireAntiforgeryValidation();

        group.MapGet("/me", (ICurrentUserContext currentUser) =>
            Results.Ok(new { authenticated = currentUser.IsAuthenticated, userId = currentUser.UserId }))
            .RequireAuthorization();

        group.MapGet("/families", async (IFamilySelectionService selection, CancellationToken cancellationToken) =>
            Results.Ok(await selection.ListAsync(cancellationToken)))
            .RequireAuthorization();

        group.MapGet("/family/current", async (ICurrentFamilyContext currentFamily, CancellationToken cancellationToken) =>
        {
            try
            {
                return Results.Ok(await currentFamily.RequireAsync(cancellationToken));
            }
            catch (InvalidOperationException)
            {
                return Results.Conflict(new { erro = "Selecione uma familia autorizada." });
            }
        }).RequireAuthorization();

        group.MapPost("/family/select", async (
            FamilySelectionRequest request,
            IFamilySelectionService selection,
            CancellationToken cancellationToken) =>
            await selection.SelectAsync(request.FamilyId, cancellationToken)
                ? Results.NoContent()
                : Results.NotFound())
            .RequireAuthorization()
            .RequireAntiforgeryValidation();

        return endpoints;
    }

    private static RouteHandlerBuilder RequireAntiforgeryValidation(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
            try
            {
                await antiforgery.ValidateRequestAsync(context.HttpContext);
                return await next(context);
            }
            catch (AntiforgeryValidationException)
            {
                return Results.BadRequest(new { erro = "Token antiforgery ausente ou invalido." });
            }
        });
}
