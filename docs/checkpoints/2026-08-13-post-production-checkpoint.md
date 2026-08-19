# Checkpoint pos-producao - 2026-08-13

## Referencia

- Repositorio: checkout local do projeto.
- Branch: `master`.
- Commit: `e06d30e`.
- Tag: `v1.0.4`.
- Estado Git no inicio: limpo e sincronizado com `origin/master`.

## Estado consolidado

A aplicacao esta funcional em producao em um servidor Linux via Docker Compose. O arquivo Compose e os dados persistentes ficam fora do repositorio e do filesystem efemero do container.

A persistencia continua em JSON, com `contas.json` como arquivo principal. `settings.json`, backups e chaves ASP.NET Data Protection permanecem no mesmo volume persistente. AccessProtection, sessao, backups automaticos e health check estao ativos. O timezone e configurado externamente com um identificador IANA.

## Funcionalidade e seguranca

O sistema cobre contas, vencimentos, pagamentos, pais/moeda, resumo mensal, CSV, Telegram, horario configuravel, backup/restauracao e interface responsiva. A protecao atual usa credencial compartilhada e cookie, com rate limiting, CSP estrita, headers de seguranca e HSTS. Ela nao representa identidade individual nem autorizacao familiar.

Nenhum secret e registrado neste checkpoint. A rotacao de qualquer token Telegram historicamente exposto ainda deve ser confirmada documentalmente.

## Testes

O test runner possui 12 cenarios de dominio, backup, configuracao e seguranca. Ainda nao existem testes de PostgreSQL, importacao, identidade individual ou isolamento multi-tenant.

## Limite arquitetural

JSON, credencial compartilhada e armazenamento singleton nao devem ser ampliados diretamente para tres familias. O primeiro schema PostgreSQL deve nascer com `Family/Tenant`. O JSON original deve ser preservado durante migracao e rollback.

## Riscos e pendencias

- manter correspondencia verificavel entre imagem implantada e commit/tag;
- definir RPO/RTO e ensaiar restore antes da migracao;
- confirmar rotacao de secrets historicamente expostos;
- projetar autenticacao, convites, recuperacao de senha e roles;
- comprovar isolamento IDOR/BOLA antes dos pilotos.

## Proximo passo autorizado

Revisar `docs/multi-family-postgresql-plan.md`. PostgreSQL, autenticacao, importacao e multi-tenancy nao estao autorizados por este checkpoint.
