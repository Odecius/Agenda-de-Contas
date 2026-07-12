# Arquitetura

## Visão geral

Aplicação ASP.NET Core .NET 8 com Minimal API, frontend estático servido por `UseStaticFiles`, persistência local em JSON e serviço em segundo plano para lembretes Telegram.

## Camadas

- `Program.cs`: composição da aplicação, rotas HTTP, DI e middleware.
- `Models/`: modelos de domínio e requests.
- `Options/`: configuração e validação do Telegram.
- `Services/`: persistência, lembrete diário, montagem de mensagens e notificações.
- `wwwroot/`: interface web.

## Fluxo de dados

1. Frontend chama API.
2. API valida request e delega para `ContaStore`.
3. `ContaStore` lê/grava o JSON local.
4. Rotas de vencimento calculam dados por mês/dia.
5. Hosted Service consulta vencimentos e envia notificação.

## Integrações

- Telegram Bot API.
- Arquivo JSON local em pasta de dados.

## Restrições

- Não há autenticação.
- Não há banco externo.
- Deploy Raspberry é planejado, mas ainda precisa validação real.
