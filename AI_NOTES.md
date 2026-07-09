# AI_NOTES

## Resumo tÃ©cnico

Agendador de Contas Ã© uma aplicaÃ§Ã£o ASP.NET Core .NET 8 com frontend estÃ¡tico em `wwwroot`, APIs mÃ­nimas em `Program.cs`, armazenamento local em JSON e lembretes diÃ¡rios via Hosted Service.

O modelo de contas possui suporte inicial a pais e moeda por conta. Os paises suportados sao `UnitedKingdom`, `Portugal` e `Brazil`; as moedas suportadas sao `GBP`, `EUR` e `BRL`. O sistema nao faz conversao cambial. Totais com moedas diferentes devem ser agrupados por moeda.

## Fluxo do sistema

1. UsuÃ¡rio acessa a interface em `wwwroot/index.html`.
2. `wwwroot/app.js` chama rotas `/api/contas` e `/api/vencimentos`.
3. `ContaStore` valida e persiste contas.
4. `DailyReminderService` verifica vencimentos e chama `INotificationService`.
5. `TelegramNotificationService` envia mensagem pela Telegram Bot API.

## Pontos crÃ­ticos

- Segredos Telegram nunca devem ficar em cÃ³digo, Git ou documentaÃ§Ã£o pÃºblica.
- `notas.txt` contÃ©m segredo histÃ³rico e precisa ser tratado.
- JSON local precisa de backup.
- NÃ£o hÃ¡ autenticaÃ§Ã£o.
- Deploy Raspberry esta preparado em documentacao e modelos, mas ainda nao foi validado em hardware real.
- Contas antigas sem `country` e `currency` assumem `UnitedKingdom` e `GBP`.

## Arquivos importantes

- `Program.cs`
- `Models/Conta.cs`
- `Services/ContaStore.cs`
- `Services/DailyReminderService.cs`
- `Services/TelegramNotificationService.cs`
- `Options/TelegramOptions.cs`
- `Options/TelegramOptionsValidator.cs`
- `wwwroot/app.js`
- `wwwroot/styles.css`

## Boas prÃ¡ticas especÃ­ficas

- Rodar `dotnet build` antes de finalizar.
- Manter rota `/test-telegram` somente em `Development`.
- Atualizar docs apÃ³s alterar rotas, configuraÃ§Ã£o, deploy ou regras de vencimento.
- Preferir serviÃ§os pequenos e testÃ¡veis para regras de negÃ³cio.
- Para deploy Raspberry, consultar `docs/deployment.md` e os modelos em `deploy/`.

## Onde continuar

PrÃ³ximo foco sugerido: deploy Raspberry Pi ou protecao de acesso antes de expor o sistema em rede.
