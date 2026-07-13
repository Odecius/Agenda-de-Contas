# AI_NOTES

## Resumo técnico

Agendador de Contas é uma aplicação ASP.NET Core .NET 8 com frontend estático em `wwwroot`, APIs mínimas em `Program.cs`, armazenamento local em JSON e lembretes diários via Hosted Service.

O modelo de contas possui suporte inicial a pais e moeda por conta. Os paises suportados sao `UnitedKingdom`, `Portugal` e `Brazil`; as moedas suportadas sao `GBP`, `EUR` e `BRL`. O sistema nao faz conversao cambial. Totais com moedas diferentes devem ser agrupados por moeda.

A interface possui resumo por pais e moeda baseado nos vencimentos do mes selecionado. Esse resumo e apenas demonstrativo/operacional: ele separa os valores por moeda e nao calcula conversao.

A exportacao CSV mensal e feita no navegador a partir de `state.vencimentos`. Ela nao chama endpoint proprio e nao faz conversao cambial.

## Fluxo do sistema

1. Usuário acessa a interface em `wwwroot/index.html`.
2. `wwwroot/app.js` chama rotas `/api/contas` e `/api/vencimentos`.
3. `ContaStore` valida e persiste contas.
4. `DailyReminderService` verifica vencimentos e chama `INotificationService`.
5. `TelegramNotificationService` envia mensagem pela Telegram Bot API.

## Pontos críticos

- Segredos Telegram nunca devem ficar em código, Git ou documentação pública.
- `notas.txt` contém segredo histórico e precisa ser tratado.
- JSON local precisa de backup.
- Ha protecao opcional de acesso por cookie, ativada por configuracao `AccessProtection`.
- Deploy Raspberry esta preparado em documentacao e modelos, mas ainda nao foi validado em hardware real.
- Contas antigas sem `country` e `currency` assumem `UnitedKingdom` e `GBP`.
- Protecao de acesso por cookie existe, mas deve ser ativada por configuracao `AccessProtection` em producao.
- Backups manuais, automaticos e `pre-restore` ficam em uma pasta `backups` ao lado do arquivo `Data:FilePath`.
- Retencao automatica remove apenas `contas.auto.*.json`; nao remover backups manuais nem `pre-restore`.
- `/health` e anonimo e deve continuar sem dados sensiveis.
- Testes automatizados ficam em `tests/AgendadorContas.Tests` e rodam com `dotnet run --project tests\AgendadorContas.Tests\AgendadorContas.Tests.csproj`.

## Arquivos importantes

- `Program.cs`
- `Models/Conta.cs`
- `Services/ContaStore.cs`
- `Services/DailyReminderService.cs`
- `Services/AutomaticBackupService.cs`
- `Services/TelegramNotificationService.cs`
- `Options/TelegramOptions.cs`
- `Options/TelegramOptionsValidator.cs`
- `wwwroot/app.js`
- `wwwroot/styles.css`

## Boas práticas específicas

- Rodar `dotnet build` antes de finalizar.
- Rodar o test runner automatizado antes de finalizar mudanças de regra de negocio.
- Manter rota `/test-telegram` somente em `Development`.
- Atualizar docs após alterar rotas, configuração, deploy ou regras de vencimento.
- Preferir serviços pequenos e testáveis para regras de negócio.
- Para deploy Raspberry, consultar `docs/deployment.md` e os modelos em `deploy/`.
- Para proteger acesso, configurar `AccessProtection__Enabled=true`, usuario e senha por User Secrets ou variaveis de ambiente.

## Onde continuar

Próximo foco sugerido: validar acesso em rede local, revisar checklist final de producao ou melhorar relatorios por moeda/pais.
