# Testing

## Testes atuais

NÃ£o hÃ¡ projeto de testes automatizados.

## VerificaÃ§Ãµes mÃ­nimas

```powershell
dotnet build
```

## Teste manual

- Criar conta com duraÃ§Ã£o definida.
- Criar conta sem fim definido usando duraÃ§Ã£o `0`.
- Editar conta.
- Pausar e reativar.
- Marcar/desmarcar pagamento.
- Consultar vencimentos do mÃªs.
- Verificar vencimentos de hoje.
- Testar `/test-telegram` em `Development`.
- Confirmar que `/test-telegram` nÃ£o existe em `Production`.

## Testes recomendados

- Criar testes unitÃ¡rios para `ContaStore`.
- Testar cÃ¡lculo de vencimentos.
- Testar `ReminderMessageBuilder`.
- Testar validaÃ§Ã£o de `TelegramOptions`.
