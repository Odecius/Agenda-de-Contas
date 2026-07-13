using AgendadorContas.Models;
using AgendadorContas.Options;
using AgendadorContas.Services;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddFilter("System.Net.Http.HttpClient.Telegram", LogLevel.Warning);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services
    .AddOptions<AccessProtectionOptions>()
    .Bind(builder.Configuration.GetSection(AccessProtectionOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<TelegramOptions>()
    .Bind(builder.Configuration.GetSection(TelegramOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<BackupOptions>()
    .Bind(builder.Configuration.GetSection(BackupOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<IValidateOptions<AccessProtectionOptions>, AccessProtectionOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<TelegramOptions>, TelegramOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<BackupOptions>, BackupOptionsValidator>();
builder.Services.AddSingleton<ContaStore>();
builder.Services.AddSingleton<IMoneyFormatter, MoneyFormatter>();
builder.Services.AddSingleton<IReminderMessageBuilder, ReminderMessageBuilder>();
builder.Services.AddSingleton<INotificationService, TelegramNotificationService>();
builder.Services.AddHttpClient("Telegram", (serviceProvider, httpClient) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<TelegramOptions>>().Value;
    httpClient.BaseAddress = new Uri(options.ApiBaseUrl);
});
builder.Services.AddHostedService<DailyReminderService>();
builder.Services.AddHostedService<AutomaticBackupService>();
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "AgendadorContas.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.LoginPath = "/login.html";
        options.LogoutPath = "/api/auth/logout";
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseSecurityHeaders();
app.UseAuthentication();
app.UseAuthorization();
app.UseAccessProtection();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", (IHostEnvironment environment) =>
{
    return Results.Ok(new
    {
        status = "ok",
        application = "AgendadorContas",
        environment = environment.EnvironmentName,
        checkedAtUtc = DateTimeOffset.UtcNow
    });
});

app.MapGet("/api/auth/status", (HttpContext httpContext, IOptions<AccessProtectionOptions> options) =>
{
    return Results.Ok(new
    {
        enabled = options.Value.Enabled,
        authenticated = !options.Value.Enabled || httpContext.User.Identity?.IsAuthenticated == true,
        username = httpContext.User.Identity?.Name
    });
});

app.MapPost("/api/auth/login", async (LoginRequest request, HttpContext httpContext, IOptions<AccessProtectionOptions> options) =>
{
    var accessOptions = options.Value;
    if (!accessOptions.Enabled)
    {
        return Results.Ok(new { sucesso = true, mensagem = "Protecao de acesso desativada." });
    }

    var usernameMatches = string.Equals(request.Username, accessOptions.Username, StringComparison.Ordinal);
    var passwordMatches = SecureEquals(request.Password, accessOptions.Password);
    if (!usernameMatches || !passwordMatches)
    {
        return Results.Unauthorized();
    }

    var claims = new[]
    {
        new Claim(ClaimTypes.Name, accessOptions.Username)
    };
    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    var principal = new ClaimsPrincipal(identity);

    await httpContext.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        principal,
        new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(accessOptions.SessionHours)
        });

    return Results.Ok(new { sucesso = true });
});

app.MapPost("/api/auth/logout", async (HttpContext httpContext) =>
{
    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok(new { sucesso = true });
});

app.MapGet("/api/contas", async (ContaStore store) =>
{
    var contas = await store.ListarContasAsync();
    return Results.Ok(contas);
});

app.MapPost("/api/contas", async (ContaCreateRequest request, ContaStore store) =>
{
    try
    {
        var conta = await store.CriarContaAsync(request);
        return Results.Created($"/api/contas/{conta.Id}", conta);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { erro = ex.Message });
    }
});

app.MapPut("/api/contas/{id:guid}", async (Guid id, ContaCreateRequest request, ContaStore store) =>
{
    try
    {
        var conta = await store.AtualizarContaAsync(id, request);
        return conta is null ? Results.NotFound() : Results.Ok(conta);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { erro = ex.Message });
    }
});

app.MapPost("/api/contas/{id:guid}/alternar-ativa", async (Guid id, ContaStore store) =>
{
    return await store.AlternarAtivaAsync(id) ? Results.NoContent() : Results.NotFound();
});

app.MapDelete("/api/contas/{id:guid}", async (Guid id, bool confirm, ContaStore store) =>
{
    if (!confirm)
    {
        return Results.BadRequest(new { erro = "Confirme a exclusao usando confirm=true." });
    }

    return await store.ExcluirContaAsync(id) ? Results.NoContent() : Results.NotFound();
});

app.MapGet("/api/vencimentos", async (int? ano, int? mes, ContaStore store) =>
{
    var hoje = DateOnly.FromDateTime(DateTime.Today);
    var data = new DateOnly(ano ?? hoje.Year, mes ?? hoje.Month, 1);
    var vencimentos = await store.ListarVencimentosAsync(data);
    return Results.Ok(vencimentos);
});

app.MapGet("/api/vencimentos/hoje", async (ContaStore store) =>
{
    var hoje = DateOnly.FromDateTime(DateTime.Today);
    var vencimentos = await store.ListarVencimentosDoDiaAsync(hoje);
    return Results.Ok(vencimentos);
});

app.MapGet("/api/backups", async (ContaStore store) =>
{
    var backups = await store.ListarBackupsAsync();
    return Results.Ok(backups);
});

app.MapPost("/api/backups", async (ContaStore store) =>
{
    var backup = await store.CriarBackupAsync();
    return Results.Created($"/api/backups/{backup.FileName}", backup);
});

app.MapPost("/api/backups/{fileName}/restaurar", async (string fileName, bool confirm, ContaStore store) =>
{
    if (!confirm)
    {
        return Results.BadRequest(new { erro = "Confirme a restauracao usando confirm=true." });
    }

    return await store.RestaurarBackupAsync(fileName)
        ? Results.Ok(new { sucesso = true })
        : Results.NotFound(new { erro = "Backup nao encontrado ou invalido." });
});

if (app.Environment.IsDevelopment())
{
    app.MapGet("/test-telegram", async (INotificationService notificationService, CancellationToken cancellationToken) =>
    {
        try
        {
            var testNumber = DateTimeOffset.Now.ToString("HH:mm - dd/MM/yy");
            var sent = await notificationService.SendAsync($"Teste {testNumber} do Agendador de Contas", cancellationToken);
            return Results.Ok(new
            {
                sucesso = sent,
                numeroTeste = testNumber,
                mensagem = sent
                    ? $"Teste {testNumber} enviado. Esta rota so existe em Development."
                    : $"Teste {testNumber} nao foi enviado porque o canal de notificacao esta desativado."
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Erro ao testar notificacao Telegram",
                detail: ex.Message,
                statusCode: StatusCodes.Status502BadGateway);
        }
    });
}

app.MapPost("/api/contas/{id:guid}/pagamentos/{ano:int}/{mes:int}", async (Guid id, int ano, int mes, ContaStore store) =>
{
    return await store.MarcarPagamentoAsync(id, ano, mes) ? Results.NoContent() : Results.NotFound();
});

app.MapDelete("/api/contas/{id:guid}/pagamentos/{ano:int}/{mes:int}", async (Guid id, int ano, int mes, ContaStore store) =>
{
    return await store.DesmarcarPagamentoAsync(id, ano, mes) ? Results.NoContent() : Results.NotFound();
});

app.Run();

static bool SecureEquals(string left, string right)
{
    var leftHash = SHA256.HashData(Encoding.UTF8.GetBytes(left));
    var rightHash = SHA256.HashData(Encoding.UTF8.GetBytes(right));
    return CryptographicOperations.FixedTimeEquals(leftHash, rightHash);
}
