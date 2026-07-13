# Testing

## Testes atuais

Existe um test runner automatizado em `tests/AgendadorContas.Tests`.
Ele cobre regras de vencimento, pagamento, backup/restauracao, retencao de backups automaticos, lembrete por moeda e verificacoes basicas da protecao de acesso.

## Verificações mínimas

```powershell
dotnet build
dotnet run --project tests\AgendadorContas.Tests\AgendadorContas.Tests.csproj
```

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
