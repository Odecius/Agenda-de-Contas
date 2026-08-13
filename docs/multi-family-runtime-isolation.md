# Multi-family runtime isolation - fase 2.1

## Escopo

A fase 2.1 prepara Identity, usuario atual e familia ativa sem trocar o runtime JSON. `ContaStore`, AccessProtection e os endpoints existentes continuam inalterados quando `MultiFamily:Enabled=false`, que e o default.

## Ativacao controlada

`MultiFamily__Enabled=true` so e aceito em `Development` ou `Testing` e exige `MultiFamily__ConnectionString`. A aplicacao falha no startup se a flag for ligada em outro ambiente. Nenhuma connection string real e mantida no repositorio.

Quando a flag esta ligada, os endpoints legados `/api/*` ficam indisponiveis e somente `/api/multi-family/*` e exposto para os testes. Isso impede que uma identidade individual acesse o JSON compartilhado antes de existirem repositories tenant-aware.

Os workers legados de lembretes e backup tambem nao sao registrados nesse modo, evitando operacoes de background sobre JSON ou Telegram fora do contexto familiar.

## Identidade e trust boundary

Identity usa `AppUser`, `IdentityRole<Guid>`, senha com o hasher padrao, email normalizado unico, lockout apos cinco falhas e cookie `HttpOnly`, `Secure` e `SameSite=Strict`. Login tem rate limiting por IP. Mutacoes usam token antiforgery.

`CurrentUserContext` aceita somente o claim `NameIdentifier` do principal autenticado. Query, body, headers e route nao sao fontes de `UserId` ou tenant.

`FamilySelectionService` consulta memberships ativas e familias/usuarios ativos no banco. A selecao fica em sessao protegida e e revalidada server-side. Uma unica familia valida e selecionada automaticamente; duas ou mais exigem escolha explicita autorizada.

`CurrentFamilyContext` retorna `FamilyId`, `UserId` e `FamilyRole`. Os roles validos continuam `Owner`, `Admin` e `Member`.

## Politica 403/404

- `404 Not Found`: ID pertence a outra familia ou a membership nao autoriza revelar o recurso.
- `403 Forbidden`: recurso pertence ao tenant atual, mas a role nao permite a operacao.

Repositories completos e a matriz de permissoes serao implementados na proxima subfase.

## Endpoints controlados

- `GET /api/multi-family/antiforgery/token`
- `POST /api/multi-family/auth/login`
- `POST /api/multi-family/auth/logout`
- `GET /api/multi-family/me`
- `GET /api/multi-family/families`
- `GET /api/multi-family/family/current`
- `POST /api/multi-family/family/select`

Nao existe UI nova, seed de runtime, importador ou migration automatica no startup.

## Limitacoes

- PostgreSQL ainda nao e o runtime das contas.
- Os endpoints JSON nao sao tenant-aware e por isso sao bloqueados no modo experimental.
- Recovery por email, convites e repositories de negocio ficam fora desta fase.
- A sessao em memoria serve somente ao ambiente local/controlado; uma estrategia distribuida sera necessaria antes de multiplas replicas.
