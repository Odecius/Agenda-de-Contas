# Arquitetura

## VisÃ£o geral

AplicaÃ§Ã£o ASP.NET Core .NET 8 com Minimal API, frontend estÃ¡tico servido por `UseStaticFiles`, persistÃªncia local em JSON e serviÃ§o em segundo plano para lembretes Telegram.

## Camadas

- `Program.cs`: composiÃ§Ã£o da aplicaÃ§Ã£o, rotas HTTP, DI e middleware.
- `Models/`: modelos de domÃ­nio e requests.
- `Options/`: configuraÃ§Ã£o e validaÃ§Ã£o do Telegram.
- `Services/`: persistÃªncia, lembrete diÃ¡rio, montagem de mensagens e notificaÃ§Ãµes.
- `wwwroot/`: interface web.

## Fluxo de dados

1. Frontend chama API.
2. API valida request e delega para `ContaStore`.
3. `ContaStore` lÃª/grava o JSON local.
4. Rotas de vencimento calculam dados por mÃªs/dia.
5. Hosted Service consulta vencimentos e envia notificaÃ§Ã£o.

## IntegraÃ§Ãµes

- Telegram Bot API.
- Arquivo JSON local em pasta de dados.

## RestriÃ§Ãµes

- NÃ£o hÃ¡ autenticaÃ§Ã£o.
- NÃ£o hÃ¡ banco externo.
- Deploy Raspberry Ã© planejado, mas ainda precisa validaÃ§Ã£o real.
