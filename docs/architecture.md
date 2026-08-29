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

- A protecao atual usa credencial compartilhada e cookie; nao e autenticacao individual.
- Não há banco externo.
- JSON e `SemaphoreSlim` suportam somente uma instancia coordenada no processo.
- Nao existe isolamento por usuario ou familia.
- Deploy Docker em Linux esta em producao; deploy Raspberry continua planejado.

## Estado de producao

A aplicacao roda via Docker Compose em um servidor Linux. Os dados JSON, configuracao do lembrete, backups e chaves ASP.NET Data Protection ficam em armazenamento persistente fora do container. AccessProtection, backups automaticos e `/health` estao ativos. O timezone usa configuracao IANA externa.

O baseline pos-producao e `v1.0.4`, commit `e06d30e`.

## Evolucao planejada

O desenho relacional esta documentado em `multi-family-postgresql-plan.md` e implementado atras de uma feature flag restrita a Development/Testing. Ele inclui PostgreSQL, Identity, contexto familiar server-side, repositories tenant-aware, UI operacional, worker por familia, importador controlado e onboarding por convite. O runtime publicado continua sendo o baseline JSON descrito acima; nenhuma migration relacional roda automaticamente.

Convites pertencem obrigatoriamente a uma familia e ao Owner criador. Apenas o hash do token e persistido; aceite e revogacao usam filtros tenant-aware e constraints relacionais. Consulte `multi-family-invitations.md`.
