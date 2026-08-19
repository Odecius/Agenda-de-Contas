using AgendadorContas.Data;
using AgendadorContas.Data.Entities;
using AgendadorContas.Data.Repositories;
using AgendadorContas.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Data;

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
public sealed record FamilyMemberCreateRequest(string Email, FamilyRole Role);
public sealed record FamilyMemberRoleRequest(FamilyRole Role);
public sealed record FamilySettingsRequest(AccountCurrency DefaultCurrency, string TimeZoneId, int ReminderHour, int ReminderMinute);
public sealed record TelegramSettingsRequest(bool IsEnabled, string? ChatId, string? BotTokenSecretReference);
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
        group.AddEndpointFilter(async (context, next) =>
        {
            try { return await next(context); }
            catch (InvalidOperationException ex) when (ex.Message == "An authorized active family must be selected.")
            {
                return Results.Conflict(new { erro = "Selecione uma familia autorizada." });
            }
        });

        group.MapGet("/mode", () => Results.Ok(new { enabled = true })).AllowAnonymous();

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
        MapMemberEndpoints(group);
        MapSettingsEndpoints(group);

        return endpoints;
    }

    private static void MapMemberEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/members", async (ICurrentFamilyContext current, AgendadorDbContext db, CancellationToken ct) =>
        {
            var tenant = await current.RequireAsync(ct);
            if (tenant.Role == FamilyRole.Member) return Results.Forbid();
            var members = await db.FamilyUsers.AsNoTracking()
                .Where(x => x.FamilyId == tenant.FamilyId)
                .OrderBy(x => x.User.Email)
                .Select(x => new { x.UserId, x.User.Email, x.Role, x.IsActive })
                .ToListAsync(ct);
            return Results.Ok(members);
        }).RequireAuthorization();

        group.MapPost("/members", async (FamilyMemberCreateRequest request, ICurrentFamilyContext current, UserManager<AppUser> users, AgendadorDbContext db, CancellationToken ct) =>
        {
            var tenant = await current.RequireAsync(ct);
            if (tenant.Role != FamilyRole.Owner) return Results.Forbid();
            if (request.Role is not (FamilyRole.Admin or FamilyRole.Member)) return Results.BadRequest(new { erro = "Role permitida: Admin ou Member." });
            var user = await users.FindByEmailAsync(request.Email.Trim());
            if (user is null || !user.IsActive) return Results.NotFound();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var membership = await db.FamilyUsers.FindAsync([tenant.FamilyId, user.Id], ct);
            if (membership is null)
            {
                db.FamilyUsers.Add(new FamilyUser { FamilyId = tenant.FamilyId, UserId = user.Id, Role = request.Role });
            }
            else
            {
                if (membership.Role == FamilyRole.Owner &&
                    await db.FamilyUsers.CountAsync(x => x.FamilyId == tenant.FamilyId && x.IsActive && x.Role == FamilyRole.Owner, ct) <= 1)
                    return Results.Conflict(new { erro = "A familia deve manter pelo menos um Owner ativo." });
                membership.Role = request.Role;
                membership.IsActive = true;
            }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Results.Ok(new { userId = user.Id, user.Email, role = request.Role, isActive = true });
        }).RequireAuthorization().RequireAntiforgeryValidation();

        group.MapPut("/members/{userId:guid}/role", async (Guid userId, FamilyMemberRoleRequest request, ICurrentFamilyContext current, AgendadorDbContext db, CancellationToken ct) =>
        {
            var tenant = await current.RequireAsync(ct);
            if (tenant.Role != FamilyRole.Owner) return Results.Forbid();
            if (request.Role is not (FamilyRole.Admin or FamilyRole.Member)) return Results.BadRequest(new { erro = "Role permitida: Admin ou Member." });
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var membership = await db.FamilyUsers.SingleOrDefaultAsync(x => x.FamilyId == tenant.FamilyId && x.UserId == userId, ct);
            if (membership is null) return Results.NotFound();
            if (membership.Role == FamilyRole.Owner && await db.FamilyUsers.CountAsync(x => x.FamilyId == tenant.FamilyId && x.IsActive && x.Role == FamilyRole.Owner, ct) <= 1)
                return Results.Conflict(new { erro = "A familia deve manter pelo menos um Owner ativo." });
            membership.Role = request.Role;
            membership.IsActive = true;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization().RequireAntiforgeryValidation();

        group.MapDelete("/members/{userId:guid}", async (Guid userId, ICurrentFamilyContext current, AgendadorDbContext db, CancellationToken ct) =>
        {
            var tenant = await current.RequireAsync(ct);
            if (tenant.Role != FamilyRole.Owner) return Results.Forbid();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var membership = await db.FamilyUsers.SingleOrDefaultAsync(x => x.FamilyId == tenant.FamilyId && x.UserId == userId, ct);
            if (membership is null) return Results.NotFound();
            if (membership.Role == FamilyRole.Owner && await db.FamilyUsers.CountAsync(x => x.FamilyId == tenant.FamilyId && x.IsActive && x.Role == FamilyRole.Owner, ct) <= 1)
                return Results.Conflict(new { erro = "A familia deve manter pelo menos um Owner ativo." });
            membership.IsActive = false;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization().RequireAntiforgeryValidation();
    }

    private static void MapSettingsEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/settings", async (ICurrentFamilyContext current, AgendadorDbContext db, CancellationToken ct) =>
        {
            var tenant = await current.RequireAsync(ct);
            var settings = await db.FamilySettings.AsNoTracking().SingleOrDefaultAsync(x => x.FamilyId == tenant.FamilyId, ct);
            return settings is null ? Results.NotFound() : Results.Ok(new { settings.DefaultCurrency, settings.TimeZoneId, settings.ReminderHour, settings.ReminderMinute });
        }).RequireAuthorization();

        group.MapPut("/settings", async (FamilySettingsRequest request, ICurrentFamilyContext current, AgendadorDbContext db, CancellationToken ct) =>
        {
            var tenant = await current.RequireAsync(ct);
            if (tenant.Role == FamilyRole.Member) return Results.Forbid();
            if (!Enum.IsDefined(request.DefaultCurrency) || request.ReminderHour is < 0 or > 23 || request.ReminderMinute is < 0 or > 59 || !IsValidTimeZone(request.TimeZoneId))
                return Results.BadRequest(new { erro = "Configuracao familiar invalida." });
            var settings = await db.FamilySettings.SingleOrDefaultAsync(x => x.FamilyId == tenant.FamilyId, ct);
            if (settings is null) { settings = new FamilySettings { FamilyId = tenant.FamilyId }; db.FamilySettings.Add(settings); }
            settings.DefaultCurrency = request.DefaultCurrency;
            settings.TimeZoneId = request.TimeZoneId.Trim();
            settings.ReminderHour = request.ReminderHour;
            settings.ReminderMinute = request.ReminderMinute;
            settings.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { settings.DefaultCurrency, settings.TimeZoneId, settings.ReminderHour, settings.ReminderMinute });
        }).RequireAuthorization().RequireAntiforgeryValidation();

        group.MapGet("/telegram-settings", async (ICurrentFamilyContext current, AgendadorDbContext db, CancellationToken ct) =>
        {
            var tenant = await current.RequireAsync(ct);
            var settings = await db.TelegramSettings.AsNoTracking().SingleOrDefaultAsync(x => x.FamilyId == tenant.FamilyId, ct);
            return Results.Ok(new { isEnabled = settings?.IsEnabled ?? false, chatIdMasked = Mask(settings?.ChatId), secretReferenceConfigured = !string.IsNullOrWhiteSpace(settings?.BotTokenSecretReference) });
        }).RequireAuthorization();

        group.MapPut("/telegram-settings", async (TelegramSettingsRequest request, ICurrentFamilyContext current, AgendadorDbContext db, CancellationToken ct) =>
        {
            var tenant = await current.RequireAsync(ct);
            if (tenant.Role != FamilyRole.Owner) return Results.Forbid();
            if (request.ChatId?.Length > 100 || request.BotTokenSecretReference?.Length > 200) return Results.BadRequest(new { erro = "Configuracao Telegram invalida." });
            var settings = await db.TelegramSettings.SingleOrDefaultAsync(x => x.FamilyId == tenant.FamilyId, ct);
            if (settings is null) { settings = new TelegramSettings { FamilyId = tenant.FamilyId }; db.TelegramSettings.Add(settings); }
            settings.IsEnabled = request.IsEnabled;
            settings.ChatId = string.IsNullOrWhiteSpace(request.ChatId) ? null : request.ChatId.Trim();
            settings.BotTokenSecretReference = string.IsNullOrWhiteSpace(request.BotTokenSecretReference) ? null : request.BotTokenSecretReference.Trim();
            settings.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { settings.IsEnabled, chatIdMasked = Mask(settings.ChatId), secretReferenceConfigured = !string.IsNullOrWhiteSpace(settings.BotTokenSecretReference) });
        }).RequireAuthorization().RequireAntiforgeryValidation();
    }

    private static bool IsValidTimeZone(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 100) return false;
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(value); return true; }
        catch (TimeZoneNotFoundException) { return false; }
        catch (InvalidTimeZoneException) { return false; }
    }

    private static string? Mask(string? value) => string.IsNullOrWhiteSpace(value) ? null : $"***{value[^Math.Min(4, value.Length)..]}";

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
