# DECISIONS

## 2026-07-09 - Usar .NET 8 com Minimal API

**Descrição:** O backend usa ASP.NET Core Minimal API em `Program.cs`.

**Motivo:** O escopo é pequeno e as rotas são diretas.

**Alternativas consideradas:** MVC, Razor Pages, SPA com backend separado.

**Impacto:** Manter rotas simples e extrair serviços quando a regra crescer.

## 2026-07-09 - Armazenar dados localmente em JSON

**Descrição:** `ContaStore` mantém os dados em arquivo JSON local.

**Motivo:** O projeto ainda é de uso pessoal/local e não exige banco externo.

**Alternativas consideradas:** SQLite, PostgreSQL, LiteDB.

**Impacto:** Simplicidade maior, mas exige atenção a backup, concorrência e migração futura.

## 2026-07-09 - Enviar notificações via Telegram

**Descrição:** `TelegramNotificationService` implementa `INotificationService`.

**Motivo:** Telegram é simples para alertas pessoais e funciona bem em automação.

**Alternativas consideradas:** Email, WhatsApp, push notification.

**Impacto:** Segredos devem ficar fora do código e a interface permite novos canais no futuro.

## 2026-07-09 - Usar User Secrets no desenvolvimento

**Descrição:** Tokens e chat id devem ser configurados por User Secrets em desenvolvimento.

**Motivo:** Evitar segredos no Git.

**Alternativas consideradas:** Gravar no `appsettings.json` ou `.env`.

**Impacto:** Desenvolvedores precisam configurar segredos localmente.

## 2026-07-09 - Restringir `/test-telegram` a Development

**Descrição:** A rota de teste existe somente em ambiente de desenvolvimento.

**Motivo:** Evitar endpoint operacional exposto em produção.

**Alternativas consideradas:** Remover rota ou proteger por autenticação.

**Impacto:** Testes de produção devem usar logs e fluxo real.

## 2026-07-09 - Preparar suporte a pais e moeda por conta

**Descricao:** Cada conta passa a ter `Country` e `Currency`, usando enums para os paises e moedas inicialmente suportados.

**Motivo:** O projeto pode evoluir para uso em multiplos paises, dashboards por pais e conversao cambial futura.

**Alternativas consideradas:** Manter moeda fixa em EUR/GBP ou usar strings livres.

**Impacto:** Valores devem ser sempre exibidos com a moeda da conta. Totais com moedas diferentes devem ser agrupados por moeda enquanto nao houver servico de conversao cambial.

## 2026-07-09 - Protecao simples por cookie

**Descricao:** A aplicacao usa uma protecao opcional por cookie, ativada por configuracao `AccessProtection`.

**Motivo:** Antes de expor o sistema na rede local, e necessario impedir acesso direto a interface e APIs.

**Alternativas consideradas:** Sem autenticacao, Basic Auth, Identity completo.

**Impacto:** Credenciais devem ser configuradas por User Secrets ou variaveis de ambiente. Para exposicao fora da rede local, ainda sera necessario avaliar HTTPS, reverse proxy e autenticacao mais robusta.

## 2026-07-09 - Dashboard por pais e moeda sem conversao

**Descricao:** A interface agrupa os vencimentos do mes selecionado por pais e moeda.

**Motivo:** O usuario precisa enxergar a distribuicao das contas por pais sem misturar moedas diferentes.

**Alternativas consideradas:** Somar todos os valores em uma moeda principal ou criar conversao cambial nesta sprint.

**Impacto:** O dashboard melhora a leitura operacional agora e preserva um ponto claro para futura integracao com servico de cambio.
