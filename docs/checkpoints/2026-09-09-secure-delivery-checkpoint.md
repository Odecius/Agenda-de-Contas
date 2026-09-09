# Checkpoint - secure delivery - 2026-09-09

## Reference

- Branch: `agent/secure-notification-delivery`.
- Base composition: password recovery Draft PR plus invitation onboarding commit.
- Dependencies: Draft PRs #6 and #7; neither was merged or modified.
- Production baseline remains the documented JSON runtime.

## State

Delivery architecture is shared by invitation and password recovery messages. External delivery is disabled by default. The HTTP email adapter requires HTTPS and externally supplied configuration, applies bounded timeout/retry and uses an idempotency key. No real provider, destination, credential or production topology was used.

Database commits complete before external calls. Password recovery remains non-enumerating on provider failure. Invitation delivery failure leaves the valid invitation available for manual secure sharing. No outbox was introduced; its operational trigger is documented.

## Security and validation

Tests use only synthetic identities, fake delivery providers and disposable databases. Raw tokens are not persisted or logged.

Final validation:

- local test suite: 57/57 passed;
- PostgreSQL 16 disposable gate: 66/66 passed, including migrations, concurrency, rollback, tenant isolation and legacy-runtime checks;
- build: 0 errors and 0 warnings;
- format and diff check: passed;
- migration drift: zero;
- vulnerable-package and secret scans: clean;
- disposable Docker resources: removed after the gate.

## Remaining work

- review and merge dependencies in the correct order;
- homologate external delivery configuration separately;
- family/Owner administration;
- migration rehearsal and cutover/rollback runbook;
- distributed sessions, rate limits, Data Protection and workers;
- browser E2E, observability and two-family pilot.

## Next milestone

Review the stacked delivery Draft PR after PRs #6 and #7, without deployment or production activation.
