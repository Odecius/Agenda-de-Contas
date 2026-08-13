# Fundacao PostgreSQL multi-tenant

## Escopo

Somente fundacao local. Producao continua em JSON. Nenhuma migration foi aplicada no servidor, nenhum dado real foi importado, nenhum deploy foi feito e Docker/Compose nao foi alterado.

## Componentes

- EF Core/Identity 8.0.29;
- Npgsql EF Core 8.0.0;
- SQLite 8.0.29 somente para testes;
- `dotnet-ef` 8.0.29 fixado no manifest local.

A migration `InitialMultiTenantSchema` cria Identity, `families`, `family_users`, `family_settings`, `telegram_settings`, `contas`, `pagamentos` e `lembretes_enviados`.

## Integridade

- role textual limitada a Owner/Admin/Member;
- settings e Telegram 1:1 com familia;
- contas com `FamilyId` e chave alternativa `(FamilyId, Id)`;
- pagamentos com FK composta cross-family e unicidade familia/conta/ano/mes;
- lembretes unicos por familia/data/canal;
- checks de valor, vencimento, duracao, mes, ano e horario.

## Validacao local

```powershell
dotnet tool restore
dotnet build
dotnet run --project tests\AgendadorContas.Tests\AgendadorContas.Tests.csproj
dotnet tool run dotnet-ef migrations script --context AgendadorDbContext
```

O factory usa `AGENDADOR_DESIGN_CONNECTION`; o fallback possui somente host/database/username locais de exemplo. Nao usar producao.

## Pendente

`CurrentFamilyContext`, repositorios, importador JSON, autenticacao individual completa, Telegram/worker por familia, backup PostgreSQL e deploy.
