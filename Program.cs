using AgendadorContas.Data;
using AgendadorContas.Data.Entities;
using AgendadorContas.Data.Repositories;
using AgendadorContas.DataMigration;
using AgendadorContas.Models;
using AgendadorContas.Options;
using AgendadorContas.Services;
using AgendadorContas.Tenancy;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
var multiFamilyOptions = builder.Configuration
    .GetSection(MultiFamilyOptions.SectionName)
    .Get<MultiFamilyOptions>() ?? new MultiFamilyOptions();

if (multiFamilyOptions.Enabled && !builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"))
{
    throw new InvalidOperationException("MultiFamily may only be enabled in Development or Testing during phase 2.1.");
}

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
builder.Services.AddSingleton<ReminderSettingsStore>();
builder.Services.AddSingleton<IMoneyFormatter, MoneyFormatter>();
builder.Services.AddSingleton<IReminderMessageBuilder, ReminderMessageBuilder>();
builder.Services.AddSingleton<INotificationService, TelegramNotificationService>();
builder.Services.AddHttpClient("Telegram", (serviceProvider, httpClient) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<TelegramOptions>>().Value;
    httpClient.BaseAddress = new Uri(options.ApiBaseUrl);
});
if (!multiFamilyOptions.Enabled)
{
    builder.Services.AddHostedService<DailyReminderService>();
    builder.Services.AddHostedService<AutomaticBackupService>();
}

var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    Directory.CreateDirectory(dataProtectionKeysPath);
    builder.Services
        .AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
        .SetApplicationName("AgendadorContas");
}

if (multiFamilyOptions.Enabled)
{
    if (string.IsNullOrWhiteSpace(multiFamilyOptions.ConnectionString))
    {
        throw new InvalidOperationException("MultiFamily:ConnectionString is required when MultiFamily is enabled.");
    }

    builder.Services.Configure<MultiFamilyOptions>(builder.Configuration.GetSection(MultiFamilyOptions.SectionName));
    builder.Services.AddDbContext<AgendadorDbContext>(options => options.UseNpgsql(multiFamilyOptions.ConnectionString));
    builder.Services
        .AddIdentityCore<AppUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<AgendadorDbContext>()
        .AddSignInManager()
        .AddDefaultTokenProviders();
    builder.Services
        .AddAuthentication(IdentityConstants.ApplicationScheme)
        .AddCookie(IdentityConstants.ApplicationScheme, options =>
        {
            options.Cookie.Name = "AgendadorContas.MultiFamily.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.ExpireTimeSpan = TimeSpan.FromHours(multiFamilyOptions.SessionHours);
            options.SlidingExpiration = true;
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUserContext, CurrentUserContext>();
    builder.Services.AddScoped<IFamilySelectionService, FamilySelectionService>();
    builder.Services.AddScoped<ICurrentFamilyContext, CurrentFamilyContext>();
    builder.Services.AddScoped<IFamilyAuthorizationService, FamilyAuthorizationService>();
    builder.Services.AddScoped<IContaRepository, ContaRepository>();
    builder.Services.AddScoped<IPagamentoRepository, PagamentoRepository>();
    builder.Services.AddScoped<IJsonToPostgresqlMigrator, JsonToPostgresqlMigrator>();
    builder.Services.AddSingleton<LoginTimingProtector>();
    builder.Services.AddDistributedMemoryCache();
    builder.Services.AddSession(options =>
    {
        options.Cookie.Name = "AgendadorContas.MultiFamily.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.IdleTimeout = TimeSpan.FromHours(multiFamilyOptions.SessionHours);
    });
    builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
}
else
{
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
}
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("login", limiterOptions =>
    {
        limiterOptions.PermitLimit = 5;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
    options.AddPolicy("multi-family-login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));
});

var app = builder.Build();

app.UseSecurityHeaders(includeHsts: app.Environment.IsProduction());
if (multiFamilyOptions.Enabled)
{
    app.Use(async (context, next) =>
    {
        if ((context.Request.Path.StartsWithSegments("/api")
                && !context.Request.Path.StartsWithSegments("/api/multi-family"))
            || context.Request.Path.Equals("/test-telegram"))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await next();
    });
}
if (multiFamilyOptions.Enabled)
{
    app.UseSession();
}
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
if (multiFamilyOptions.Enabled)
{
    app.UseAntiforgery();
}
else
{
    app.UseAccessProtection();
}
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () =>
{
    return Results.Ok(new
    {
        status = "ok"
    });
});

if (multiFamilyOptions.Enabled)
{
    app.MapMultiFamilyEndpoints();
}

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
}).RequireRateLimiting("login");

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

app.MapGet("/api/settings/reminder", async (ReminderSettingsStore settingsStore) =>
{
    var settings = await settingsStore.GetAsync();
    return Results.Ok(settings);
});

app.MapPut("/api/settings/reminder", async (ReminderSettingsUpdateRequest request, ReminderSettingsStore settingsStore) =>
{
    try
    {
        var settings = await settingsStore.UpdateAsync(request);
        return Results.Ok(settings);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { erro = ex.Message });
    }
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

public partial class Program;
