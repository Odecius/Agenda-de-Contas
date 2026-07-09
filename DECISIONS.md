# DECISIONS

## 2026-07-09 - Usar .NET 8 com Minimal API

**DescriÃ§Ã£o:** O backend usa ASP.NET Core Minimal API em `Program.cs`.

**Motivo:** O escopo Ã© pequeno e as rotas sÃ£o diretas.

**Alternativas consideradas:** MVC, Razor Pages, SPA com backend separado.

**Impacto:** Manter rotas simples e extrair serviÃ§os quando a regra crescer.

## 2026-07-09 - Armazenar dados localmente em JSON

**DescriÃ§Ã£o:** `ContaStore` mantÃ©m os dados em arquivo JSON local.

**Motivo:** O projeto ainda Ã© de uso pessoal/local e nÃ£o exige banco externo.

**Alternativas consideradas:** SQLite, PostgreSQL, LiteDB.

**Impacto:** Simplicidade maior, mas exige atenÃ§Ã£o a backup, concorrÃªncia e migraÃ§Ã£o futura.

## 2026-07-09 - Enviar notificaÃ§Ãµes via Telegram

**DescriÃ§Ã£o:** `TelegramNotificationService` implementa `INotificationService`.

**Motivo:** Telegram Ã© simples para alertas pessoais e funciona bem em automaÃ§Ã£o.

**Alternativas consideradas:** Email, WhatsApp, push notification.

**Impacto:** Segredos devem ficar fora do cÃ³digo e a interface permite novos canais no futuro.

## 2026-07-09 - Usar User Secrets no desenvolvimento

**DescriÃ§Ã£o:** Tokens e chat id devem ser configurados por User Secrets em desenvolvimento.

**Motivo:** Evitar segredos no Git.

**Alternativas consideradas:** Gravar no `appsettings.json` ou `.env`.

**Impacto:** Desenvolvedores precisam configurar segredos localmente.

## 2026-07-09 - Restringir `/test-telegram` a Development

**DescriÃ§Ã£o:** A rota de teste existe somente em ambiente de desenvolvimento.

**Motivo:** Evitar endpoint operacional exposto em produÃ§Ã£o.

**Alternativas consideradas:** Remover rota ou proteger por autenticaÃ§Ã£o.

**Impacto:** Testes de produÃ§Ã£o devem usar logs e fluxo real.

## 2026-07-09 - Preparar suporte a pais e moeda por conta

**Descricao:** Cada conta passa a ter `Country` e `Currency`, usando enums para os paises e moedas inicialmente suportados.

**Motivo:** O projeto pode evoluir para uso em multiplos paises, dashboards por pais e conversao cambial futura.

**Alternativas consideradas:** Manter moeda fixa em EUR/GBP ou usar strings livres.

**Impacto:** Valores devem ser sempre exibidos com a moeda da conta. Totais com moedas diferentes devem ser agrupados por moeda enquanto nao houver servico de conversao cambial.
