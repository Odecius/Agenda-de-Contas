using AgendadorContas.Models;
using AgendadorContas.Options;
using AgendadorContas.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>(optional: true, reloadOnChange: true);
}

builder.Services
    .AddOptions<TelegramOptions>()
    .Bind(builder.Configuration.GetSection(TelegramOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<IValidateOptions<TelegramOptions>, TelegramOptionsValidator>();
builder.Services.AddSingleton<ContaStore>();
builder.Services.AddSingleton<INotificationService, TelegramNotificationService>();
builder.Services.AddHttpClient("Telegram", (serviceProvider, httpClient) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<TelegramOptions>>().Value;
    httpClient.BaseAddress = new Uri(options.ApiBaseUrl);
});
builder.Services.AddHostedService<DailyReminderService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

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

app.MapDelete("/api/contas/{id:guid}", async (Guid id, ContaStore store) =>
{
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

app.MapGet("/test-telegram", async (INotificationService notificationService, CancellationToken cancellationToken) =>
{
    try
    {
        await notificationService.SendAsync("Teste do Agendador de Contas", cancellationToken);
        return Results.Ok(new
        {
            sucesso = true,
            mensagem = "Teste de notificacao processado. Verifique o Telegram ou os logs se Telegram:Enabled estiver false."
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

app.MapPost("/api/contas/{id:guid}/pagamentos/{ano:int}/{mes:int}", async (Guid id, int ano, int mes, ContaStore store) =>
{
    return await store.MarcarPagamentoAsync(id, ano, mes) ? Results.NoContent() : Results.NotFound();
});

app.MapDelete("/api/contas/{id:guid}/pagamentos/{ano:int}/{mes:int}", async (Guid id, int ano, int mes, ContaStore store) =>
{
    return await store.DesmarcarPagamentoAsync(id, ano, mes) ? Results.NoContent() : Results.NotFound();
});

app.Run();
