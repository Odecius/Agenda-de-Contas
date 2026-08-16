using AgendadorContas.Data.Entities;
using AgendadorContas.Data.Repositories;
using AgendadorContas.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AgendadorContas.Tenancy;

public sealed record IdentityLoginRequest(string Email, string Password);
public sealed record FamilySelectionRequest(Guid FamilyId);
public sealed record MultiFamilyContaRequest(
    string Nome,
    decimal Valor,
    AccountCountry Country,
    AccountCurrency Currency,
    int DiaVencimento,
    DateOnly DataInicio,
    int DuracaoMeses,
    bool Ativa = true,
    string? Observacoes = null);
public sealed record MultiFamilyPagamentoRequest(int Ano, int Mes);
public sealed record MultiFamilyContaResponse(
    Guid Id,
    string Nome,
    decimal Valor,
    AccountCountry Country,
    AccountCurrency Currency,
    int DiaVencimento,
    DateOnly DataInicio,
    int DuracaoMeses,
    bool Ativa,
    string? Observacoes);
public sealed record MultiFamilyPagamentoResponse(Guid Id, Guid ContaId, int Ano, int Mes, DateTime PagoEmUtc);

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

        MapContaEndpoints(group);
        MapPagamentoEndpoints(group);

        return endpoints;
    }

    private static void MapContaEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/contas", async (
            IContaRepository repository,
            IFamilyAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            if (!await authorization.IsAllowedAsync(FamilyPermission.ViewContas, cancellationToken))
            {
                return Results.Forbid();
            }

            return Results.Ok((await repository.ListAsync(cancellationToken)).Select(ToResponse));
        }).RequireAuthorization();

        group.MapGet("/contas/{id:guid}", async (
            Guid id,
            IContaRepository repository,
            IFamilyAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            var conta = await repository.GetByIdAsync(id, cancellationToken);
            if (conta is null)
            {
                return Results.NotFound();
            }

            return await authorization.IsAllowedAsync(FamilyPermission.ViewContas, cancellationToken)
                ? Results.Ok(ToResponse(conta))
                : Results.Forbid();
        }).RequireAuthorization();

        group.MapPost("/contas", async (
            MultiFamilyContaRequest request,
            IContaRepository repository,
            IFamilyAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            if (!await authorization.IsAllowedAsync(FamilyPermission.CreateConta, cancellationToken))
            {
                return Results.Forbid();
            }

            var validationError = Validate(request);
            if (validationError is not null)
            {
                return validationError;
            }

            var conta = await repository.CreateAsync(ToWriteModel(request), cancellationToken);
            return Results.Created($"/api/multi-family/contas/{conta.Id}", ToResponse(conta));
        }).RequireAuthorization().RequireAntiforgeryValidation();

        group.MapPut("/contas/{id:guid}", async (
            Guid id,
            MultiFamilyContaRequest request,
            IContaRepository repository,
            IFamilyAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            if (await repository.GetByIdAsync(id, cancellationToken) is null)
            {
                return Results.NotFound();
            }

            if (!await authorization.IsAllowedAsync(FamilyPermission.EditConta, cancellationToken))
            {
                return Results.Forbid();
            }

            var validationError = Validate(request);
            if (validationError is not null)
            {
                return validationError;
            }

            var conta = await repository.UpdateAsync(id, ToWriteModel(request), cancellationToken);
            return conta is null ? Results.NotFound() : Results.Ok(ToResponse(conta));
        }).RequireAuthorization().RequireAntiforgeryValidation();

        group.MapDelete("/contas/{id:guid}", async (
            Guid id,
            IContaRepository repository,
            IFamilyAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            if (await repository.GetByIdAsync(id, cancellationToken) is null)
            {
                return Results.NotFound();
            }

            if (!await authorization.IsAllowedAsync(FamilyPermission.DeleteConta, cancellationToken))
            {
                return Results.Forbid();
            }

            return await repository.DeleteAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization().RequireAntiforgeryValidation();
    }

    private static void MapPagamentoEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/pagamentos", async (
            IPagamentoRepository repository,
            IFamilyAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            if (!await authorization.IsAllowedAsync(FamilyPermission.ViewPagamentos, cancellationToken))
            {
                return Results.Forbid();
            }

            return Results.Ok((await repository.ListAsync(cancellationToken)).Select(ToResponse));
        }).RequireAuthorization();

        group.MapGet("/contas/{contaId:guid}/pagamentos", async (
            Guid contaId,
            IPagamentoRepository repository,
            IFamilyAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            var pagamentos = await repository.ListForContaAsync(contaId, cancellationToken);
            if (pagamentos is null)
            {
                return Results.NotFound();
            }

            return await authorization.IsAllowedAsync(FamilyPermission.ViewPagamentos, cancellationToken)
                ? Results.Ok(pagamentos.Select(ToResponse))
                : Results.Forbid();
        }).RequireAuthorization();

        group.MapPost("/contas/{contaId:guid}/pagamentos", async (
            Guid contaId,
            MultiFamilyPagamentoRequest request,
            IContaRepository contaRepository,
            IPagamentoRepository pagamentoRepository,
            IFamilyAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            if (await contaRepository.GetByIdAsync(contaId, cancellationToken) is null)
            {
                return Results.NotFound();
            }

            if (!await authorization.IsAllowedAsync(FamilyPermission.CreatePagamento, cancellationToken))
            {
                return Results.Forbid();
            }

            if (request.Ano is < 1 or > 9999 || request.Mes is < 1 or > 12)
            {
                return Results.BadRequest(new { erro = "Ano ou mes invalido." });
            }

            try
            {
                var pagamento = await pagamentoRepository.CreateAsync(contaId, request.Ano, request.Mes, cancellationToken);
                return pagamento is null
                    ? Results.NotFound()
                    : Results.Created($"/api/multi-family/pagamentos/{pagamento.Id}", ToResponse(pagamento));
            }
            catch (DbUpdateException)
            {
                return Results.Conflict(new { erro = "Pagamento ja registrado para o periodo." });
            }
        }).RequireAuthorization().RequireAntiforgeryValidation();

        group.MapDelete("/pagamentos/{id:guid}", async (
            Guid id,
            IPagamentoRepository repository,
            IFamilyAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            if (await repository.GetByIdAsync(id, cancellationToken) is null)
            {
                return Results.NotFound();
            }

            if (!await authorization.IsAllowedAsync(FamilyPermission.DeletePagamento, cancellationToken))
            {
                return Results.Forbid();
            }

            return await repository.DeleteAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization().RequireAntiforgeryValidation();
    }

    private static IResult? Validate(MultiFamilyContaRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nome) || request.Nome.Trim().Length > 80)
        {
            return Results.BadRequest(new { erro = "Nome e obrigatorio e deve ter no maximo 80 caracteres." });
        }

        if (request.Valor <= 0 || request.DiaVencimento is < 1 or > 31 || request.DuracaoMeses < 0)
        {
            return Results.BadRequest(new { erro = "Valores da conta sao invalidos." });
        }

        if (!Enum.IsDefined(request.Country) || !Enum.IsDefined(request.Currency) || request.Observacoes?.Length > 300)
        {
            return Results.BadRequest(new { erro = "Pais, moeda ou observacoes invalidos." });
        }

        return null;
    }

    private static ContaWriteModel ToWriteModel(MultiFamilyContaRequest request) => new(
        request.Nome,
        request.Valor,
        request.Country,
        request.Currency,
        request.DiaVencimento,
        request.DataInicio,
        request.DuracaoMeses,
        request.Ativa,
        request.Observacoes);

    private static MultiFamilyContaResponse ToResponse(ContaEntity entity) => new(
        entity.Id,
        entity.Nome,
        entity.Valor,
        entity.Country,
        entity.Currency,
        entity.DiaVencimento,
        entity.DataInicio,
        entity.DuracaoMeses,
        entity.Ativa,
        entity.Observacoes);

    private static MultiFamilyPagamentoResponse ToResponse(PagamentoEntity entity) =>
        new(entity.Id, entity.ContaId, entity.Ano, entity.Mes, entity.PagoEmUtc);

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
