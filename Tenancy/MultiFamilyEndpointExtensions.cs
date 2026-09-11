using AgendadorContas.Data;
using AgendadorContas.Data.Entities;
using AgendadorContas.Data.Repositories;
using AgendadorContas.Models;
using AgendadorContas.Options;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Data;

namespace AgendadorContas.Tenancy;

public sealed record IdentityLoginRequest(string Email, string Password);
public sealed record FamilyRegistrationRequest(string Email, string Password, string FamilyName);
public sealed record ForgotPasswordRequest(string Email);
public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);
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
public sealed record FamilyMemberRoleRequest(FamilyRole Role);
public sealed record FamilyInvitationCreateRequest(string Email, FamilyRole Role);
public sealed record FamilyInvitationAcceptRequest(string Token, string Email, string Password);
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

        group.MapGet("/mode", (IOptions<PasswordRecoveryOptions> recovery, IOptions<RegistrationOptions> registration) =>
            Results.Ok(new
            {
                enabled = true,
                passwordRecoveryEnabled = recovery.Value.Enabled,
                registrationEnabled = registration.Value.Enabled
            })).AllowAnonymous();

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

        group.MapPost("/auth/register", async (
            FamilyRegistrationRequest request,
            IOptions<RegistrationOptions> options,
            FamilyRegistrationService registration,
            LoginTimingProtector timingProtector,
            UserManager<AppUser> users,
            SignInManager<AppUser> signInManager,
            IFamilySelectionService selection,
            CancellationToken ct) =>
        {
            if (!options.Value.Enabled) return Results.NotFound();

            try
            {
                var result = await registration.RegisterAsync(request.Email, request.Password, request.FamilyName, ct);
                if (!result.Succeeded || result.UserId is null || result.FamilyId is null)
                {
                    timingProtector.Verify(request.Password);
                    return Results.BadRequest(new { erro = "Nao foi possivel concluir o cadastro." });
                }

                var user = await users.FindByIdAsync(result.UserId.Value.ToString());
                if (user is null) return Results.BadRequest(new { erro = "Nao foi possivel concluir o cadastro." });
                selection.Clear();
                await signInManager.SignInAsync(user, isPersistent: false);
                await selection.SelectAsync(result.FamilyId.Value, ct);
                return Results.Created("/api/multi-family/me", new { sucesso = true });
            }
            catch (Exception exception) when (exception is DbUpdateException or InvalidOperationException)
            {
                return Results.BadRequest(new { erro = "Nao foi possivel concluir o cadastro." });
            }
        }).AllowAnonymous().RequireRateLimiting("multi-family-registration").RequireAntiforgeryValidation();

        group.MapPost("/auth/forgot-password", async (ForgotPasswordRequest request, PasswordRecoveryService recovery, CancellationToken ct) =>
        {
            await recovery.RequestAsync(request.Email, ct);
            return Results.Ok(new { mensagem = PasswordRecoveryService.GenericResponse });
        }).AllowAnonymous().RequireRateLimiting("password-recovery-request").RequireAntiforgeryValidation();

        group.MapPost("/auth/reset-password", async (ResetPasswordRequest request, PasswordRecoveryService recovery) =>
            await recovery.ResetAsync(request.Email, request.Token, request.NewPassword)
                ? Results.Ok(new { sucesso = true })
                : Results.BadRequest(new { erro = "Nao foi possivel redefinir a senha." }))
            .AllowAnonymous().RequireRateLimiting("password-recovery-reset").RequireAntiforgeryValidation();

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
        MapInvitationEndpoints(group);
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

        group.MapPut("/members/{userId:guid}/role", async (Guid userId, FamilyMemberRoleRequest request, FamilyMembershipService memberships, CancellationToken ct) =>
        {
            try
            {
                var result = await memberships.ChangeRoleAsync(userId, request.Role, ct);
                return MembershipResult(result);
            }
            catch (Exception exception) when (IsPostgresConcurrencyConflict(exception))
            {
                return Results.Conflict(new { erro = "A membership mudou durante a operacao. Atualize e tente novamente." });
            }
        }).RequireAuthorization().RequireAntiforgeryValidation();

        group.MapDelete("/members/{userId:guid}", async (Guid userId, FamilyMembershipService memberships, CancellationToken ct) =>
        {
            try
            {
                var result = await memberships.RemoveAsync(userId, ct);
                return MembershipResult(result);
            }
            catch (Exception exception) when (IsPostgresConcurrencyConflict(exception))
            {
                return Results.Conflict(new { erro = "A membership mudou durante a operacao. Atualize e tente novamente." });
            }
        }).RequireAuthorization().RequireAntiforgeryValidation();
    }

    private static IResult MembershipResult(MembershipChangeResult result) => result switch
    {
        MembershipChangeResult.Success => Results.NoContent(),
        MembershipChangeResult.NotFound => Results.NotFound(),
        MembershipChangeResult.Forbidden => Results.Forbid(),
        MembershipChangeResult.LastOwner => Results.Conflict(new { erro = "A familia deve manter pelo menos um Owner ativo." }),
        _ => Results.BadRequest()
    };

    private static bool IsPostgresConcurrencyConflict(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres && postgres.SqlState is "40001" or "40P01") return true;
        }
        return false;
    }

    private static void MapInvitationEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/invitations", async (IFamilyInvitationService invitations, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await invitations.ListAsync(ct));
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        }).RequireAuthorization();

        group.MapPost("/invitations", async (
            FamilyInvitationCreateRequest request,
            IFamilyInvitationService invitations,
            CancellationToken ct) =>
        {
            try
            {
                var invitation = await invitations.CreateAsync(request.Email, request.Role, ct);
                return Results.Created($"/api/multi-family/invitations/{invitation.Id}", invitation);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
            catch (ArgumentException)
            {
                return Results.BadRequest(new { erro = "Email ou role de convite invalido." });
            }
            catch (FamilyInvitationConflictException)
            {
                return Results.Conflict(new { erro = "O usuario ja pertence a esta familia." });
            }
        }).RequireAuthorization().RequireAntiforgeryValidation();

        group.MapDelete("/invitations/{invitationId:guid}", async (
            Guid invitationId,
            IFamilyInvitationService invitations,
            CancellationToken ct) =>
        {
            try
            {
                return await invitations.RevokeAsync(invitationId, ct)
                    ? Results.NoContent()
                    : Results.NotFound();
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        }).RequireAuthorization().RequireAntiforgeryValidation();

        group.MapPost("/invitations/accept", async (
            FamilyInvitationAcceptRequest request,
            IFamilyInvitationService invitations,
            UserManager<AppUser> users,
            SignInManager<AppUser> signInManager,
            IFamilySelectionService selection,
            CancellationToken ct) =>
        {
            var accepted = await invitations.AcceptAsync(request.Token, request.Email, request.Password, ct);
            if (accepted is null)
            {
                return Results.BadRequest(new { erro = "Convite invalido, expirado ou ja utilizado." });
            }

            var user = await users.FindByIdAsync(accepted.UserId.ToString());
            if (user is null || !user.IsActive)
            {
                return Results.BadRequest(new { erro = "Convite invalido, expirado ou ja utilizado." });
            }

            selection.Clear();
            await signInManager.SignInAsync(user, isPersistent: false);
            return Results.Ok(new { accepted.UserId, accepted.FamilyId, accepted.UserCreated });
        }).AllowAnonymous().RequireRateLimiting("multi-family-invitation").RequireAntiforgeryValidation();
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
