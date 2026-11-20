# Pilot engineering story - editorial notes

This is a source outline, not a finished or published article.

## Problem

A useful personal bill scheduler can succeed with local JSON, a single trust boundary and simple operations. A Multi-Family Pilot changes the engineering problem: identities become individual, every query needs tenant context, administration must avoid public back doors, database change becomes operational risk, and recovery must be demonstrated rather than assumed.

The story is therefore not “replace JSON with PostgreSQL.” It is how to preserve a working legacy path while building a separately gated relational path with explicit evidence at each boundary.

## Architecture evolution

### JSON and local-state origin

The original application used `ContaStore` and JSON persistence. That simple design remains useful as the legacy runtime and rollback reference. The relational path was introduced behind a feature boundary rather than silently replacing the working system.

### PostgreSQL transition

The relational model added Families, users, memberships, settings, accounts, payments and reminder records. Migration tooling treats JSON as controlled input, uses deterministic identifiers and supports dry-run, reconciliation, idempotency and transactional rollback. A real cutover remains an explicit operational decision.

### Tenant isolation

Tenant scope is established from authenticated membership and the active server-side Family context. Repositories do not accept an arbitrary `FamilyId` as authority. Authorization combines tenant context with Owner, Admin and Member roles, while database constraints protect cross-Family relationships.

### Least privilege and migration safety

The Pilot database separates bootstrap administration, migration ownership and runtime data access. Migrations are explicit; runtime cannot perform schema changes; startup does not mutate the schema. Disposable PostgreSQL testing validates the design before controlled operational verification.

### Controlled bootstrap and authentication recovery

The first Owner cannot depend on public registration. Bootstrap is therefore an explicit CLI command behind a temporary private gate. Password reset follows the same pattern and uses Identity token/reset APIs rather than direct hash editing. Both commands fail closed outside their intended environments and have no HTTP administrative endpoint.

### Backup, restore and readiness

Backup design includes native database dumps, checksums, guarded publication and off-host verification. Restore is treated as the proof that backup evidence is usable. Liveness and readiness are separate: readiness checks database access and migration currency while returning a generic response.

### External ingress boundary and post-smoke recovery

The operator reported a dedicated tunnel terminating into an ingress-only Docker network, with the application bridging that ingress boundary to a separate database network and PostgreSQL remaining off the ingress network. This reduces unnecessary lateral reach without claiming complete network isolation. The same external-validation sequence reported public readiness, browser authentication, a synthetic transactional smoke test and a successful disposable restore of a new post-smoke backup, including all three migrations. These remain operator-confirmed until independently reviewable sanitized artifacts are approved.

## Engineering principles

- **Fail closed:** ambiguous environment, target or administrative state stops the operation.
- **Least privilege:** runtime receives data access, not schema ownership.
- **Explicit administrative gates:** bootstrap and reset are temporary CLI operations, not public endpoints.
- **No secret coupling:** credentials and connection details remain outside Git and outside publication evidence.
- **Disposable validation:** destructive migration, restore and failure scenarios run against isolated generated resources.
- **Migration integrity:** schema changes are reviewed and explicitly applied by the migration identity.
- **Recoverability before expansion:** backup plus restore proof precedes participant growth.
- **Authentication before exposure:** individual identity and tenant authorization precede external access.
- **Operational evidence over assumptions:** implemented, tested and operationally verified claims stay distinct.

## Lessons supported by the history

1. Feature flags help preserve a working legacy path, but environment allowlists and fail-closed configuration are also required.
2. Adding `FamilyId` is insufficient; tenant authority must be derived server-side and reinforced by relationships and constraints.
3. Administrative convenience becomes attack surface unless it is explicit, temporary, non-HTTP and disabled by default.
4. Database ownership is an application security decision, not only an infrastructure detail.
5. A successful dump is not recovery evidence; restoration into an isolated target is the meaningful gate.
6. Publication-quality evidence benefits from recording what was implemented, tested in disposable infrastructure and verified operationally as separate layers.

## Possible article themes

1. Tenant isolation beyond adding a tenant column.
2. Evolving a JSON-backed .NET application without a forced cutover.
3. A fail-closed Pilot environment as an engineering deliverable.
4. Least-privilege PostgreSQL roles for a small application.
5. Why backup completion is weaker evidence than a tested restore.
6. CLI-only bootstrap and password recovery without public administrative endpoints.
7. Operational readiness as a deliberate engineering deliverable.
8. Network boundaries between ingress and database services.

## Editorial cautions

- Do not publish private topology, identities, paths, screenshots or operational commands copied from audits.
- Do not describe disposable testing as real-environment proof.
- Do not claim scale, adoption or security certification that the evidence does not establish.
- Preserve the distinction between a sanitized checkpoint that records operator confirmation and independently publication-evidenced operational proof.
- Obtain author confirmation before publishing operational outcomes, timelines, personal contribution attribution or impact statements.
