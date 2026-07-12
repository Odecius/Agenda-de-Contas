# Testing

## Testes atuais

Não há projeto de testes automatizados.

## Verificações mínimas

```powershell
dotnet build
```

## Teste manual

- Criar conta com duração definida.
- Criar conta sem fim definido usando duração `0`.
- Editar conta.
- Pausar e reativar.
- Marcar/desmarcar pagamento.
- Consultar vencimentos do mês.
- Verificar vencimentos de hoje.
- Testar `/test-telegram` em `Development`.
- Confirmar que `/test-telegram` não existe em `Production`.

## Testes recomendados

- Criar testes unitários para `ContaStore`.
- Testar cálculo de vencimentos.
- Testar `ReminderMessageBuilder`.
- Testar validação de `TelegramOptions`.
