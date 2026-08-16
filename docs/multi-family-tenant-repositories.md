# Multi-family tenant-aware repositories - fase 2.2

## Boundary de tenant

`ContaRepository` e `PagamentoRepository` sao scoped e resolvem a familia atual por `ICurrentFamilyContext` em cada operacao. Seus metodos publicos recebem apenas IDs de recursos e dados de negocio; nenhum recebe `FamilyId` arbitrario.

Criacoes atribuem `FamilyId` server-side. Leituras, updates e deletes consultam por `FamilyId atual + ID`. `FindAsync(id)` sem tenant nao e usado. Consultas read-only usam `AsNoTracking`.

DTOs HTTP nao possuem `FamilyId`, `UserId`, `FamilyRole` ou `TenantId`. Propriedades JSON extras sao ignoradas pelo serializer, mas nunca consultadas nem usadas como autoridade. Query strings e headers como `X-Family-Id` tambem nao participam da resolucao.

## Matriz de roles

| Operacao | Owner | Admin | Member |
| --- | --- | --- | --- |
| Visualizar contas e pagamentos | Sim | Sim | Sim |
| Criar conta | Sim | Sim | Nao |
| Editar/ativar/desativar conta | Sim | Sim | Nao |
| Excluir conta | Sim | Nao | Nao |
| Registrar pagamento | Sim | Sim | Sim |
| Remover pagamento | Sim | Nao | Nao |

A exclusao continua fisica, como no runtime legado, mas fica restrita a Owner no modo experimental. Cascades e FKs do schema continuam como protecao adicional.

## Semantica HTTP

- `404 Not Found`: recurso nao existe ou pertence a outra familia, inclusive conta referenciada por pagamento.
- `403 Forbidden`: recurso pertence ao tenant atual, mas a role nao permite a operacao.
- `409 Conflict`: pagamento ja existe para a mesma conta/ano/mes.
- `400 Bad Request`: DTO invalido ou mutacao sem antiforgery valido.

O endpoint resolve o recurso tenant-aware antes de avaliar permissoes destrutivas, impedindo enumeracao cross-family.

## Endpoints experimentais

- `GET /api/multi-family/contas`
- `GET /api/multi-family/contas/{id}`
- `POST /api/multi-family/contas`
- `PUT /api/multi-family/contas/{id}`
- `DELETE /api/multi-family/contas/{id}`
- `GET /api/multi-family/pagamentos`
- `GET /api/multi-family/contas/{contaId}/pagamentos`
- `POST /api/multi-family/contas/{contaId}/pagamentos`
- `DELETE /api/multi-family/pagamentos/{id}`

Todos exigem Identity e familia ativa. POST, PUT e DELETE exigem antiforgery.

## Evidencia de isolamento

O harness PostgreSQL 16 cria somente usuarios, familias, contas e pagamentos ficticios. Ele prova listagem isolada A/B, 404 bidirecional, tenant server-side em criacoes, matriz Owner/Admin/Member, antiforgery, adulteracao em body/query/headers e troca A -> B -> A sem estado antigo no repository/DbContext.

## Limitacoes e proxima etapa

- Nao existe UI multi-family completa.
- PostgreSQL nao e runtime de producao.
- Nao existe importacao de JSON real, Telegram por familia ou worker relacional.
- A sessao continua em memoria e requer estrategia distribuida antes de multiplas replicas.
- A proxima fase deve ser planejada separadamente, preservando `ContaStore + JSON` ate uma migracao explicitamente aprovada.
