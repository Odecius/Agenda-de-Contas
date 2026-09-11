# Testing

## Testes atuais

Existe um test runner automatizado em `tests/AgendadorContas.Tests`.
Ele cobre regras de vencimento, pagamento, backup/restauracao, retencao de backups automaticos, lembrete por moeda e verificacoes basicas da protecao de acesso.

## Verificações mínimas

```powershell
dotnet build
dotnet run --project tests\AgendadorContas.Tests\AgendadorContas.Tests.csproj
```

## Gate PostgreSQL 16 descartável

Com Docker Desktop saudável, executar:

```powershell
.\tests\run-postgresql16-gate.ps1
```

O script cria um container `postgres:16-alpine` com nome, porta e senha temporários, usa `tmpfs` em vez de volume persistente, define `AGENDADOR_TEST_POSTGRES` somente durante a execução e remove o container em `finally`. Os oito testes condicionais PostgreSQL não são adicionados ao runner quando essa variável está ausente e nunca devem ser contados como aprovados nessa situação.

O gate cobre migrations em banco vazio, Identity/runtime HTTP, isolamento A/B, importação JSON sintética, lifecycle de convites, cadastro/familia/Owner, concorrência de email e Owner, rollback forçado e constraints PostgreSQL. Nenhum dado ou credential real deve ser usado.

O milestone de pilot readiness inclui ainda um fluxo de navegador em ambiente local descartavel: cadastro, login/logout, recovery, aceite de convite, administracao e troca entre duas Families. Os assertions de seguranca e isolamento permanecem na suite HTTP/PostgreSQL; o navegador valida a integracao e acessibilidade basica sem snapshots de pixels.

Em 2026-08-29, o gate foi executado em PostgreSQL 16 real descartável e concluiu com 59/59 testes. Concorrência, rollback, constraints, isolamento e cleanup passaram; nenhum recurso Docker residual permaneceu e nenhum ambiente ou dado de produção foi acessado.

## Teste manual

- Criar conta com duração definida.
- Criar conta sem fim definido usando duração `0`.
- Editar conta.
- Pausar e reativar.
- Marcar/desmarcar pagamento.
- Criar backup manual.
- Restaurar backup com confirmação.
- Conferir backup automatico em ambiente configurado com `Backup__AutomaticEnabled=true`.
- Confirmar que retencao remove apenas backups automaticos antigos.
- Consultar vencimentos do mês.
- Conferir o resumo por pais e moeda no mes selecionado.
- Filtrar contas por pais e moeda.
- Exportar CSV do mes selecionado e conferir colunas de pais, moeda, valor e status.
- Verificar `/health` e confirmar que nao retorna dados sensiveis.
- Verificar vencimentos de hoje.
- Testar `/test-telegram` em `Development`.
- Confirmar que `/test-telegram` não existe em `Production`.

## Testes recomendados

- Expandir testes unitários para `ContaStore`.
- Cobrir mais cenários de cálculo de vencimentos.
- Testar validação de `TelegramOptions`.
- Testar validação de `AccessProtectionOptions`.
- Testar endpoints de autenticação com servidor em memória futuramente.
