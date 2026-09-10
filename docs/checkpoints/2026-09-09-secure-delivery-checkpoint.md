# Checkpoint - secure delivery - 2026-09-10

## Reference

- Branch: `agent/secure-notification-delivery`.
- Base synchronized with `master` at `380c46944a94703f2108cb5daa185efc1d790e6d` by merge commit.
- PRs #6, #7 and #9 are integrated in `master`; their hardened implementations were preserved as the conflict-resolution baseline.
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

- complete the independent review and merge decision for PR #8;
- homologate external delivery configuration separately;
- family/Owner administration;
- migration rehearsal and cutover/rollback runbook;
- distributed sessions, rate limits, Data Protection and workers;
- browser E2E, observability and two-family pilot.

## Next milestone

Review the synchronized delivery PR independently, without deployment or production activation.

## Historical PR integration review - 2026-09-09

The combined state of Draft PRs #6, #7 and #8 was reviewed and validated. PRs #6 and #7 are functionally independent, but both edit shared composition and documentation files. PR #6 alone owns the invitation schema migration. PR #8 contains the exact password-recovery commit, an equivalent conflict-resolved invitation commit and the secure-delivery commit.

Recommended integration order:

1. merge PR #7 after its independent review;
2. synchronize PR #6 with the resulting `master`, resolve only the known shared-file overlaps and rerun its gates;
3. merge PR #6;
4. synchronize PR #8 with the resulting `master`, retaining only the effective delivery changes, rerun the combined gates and then review it for merge.

This order follows the existing ancestry of PR #8 and avoids rewriting published history. No branch requires a speculative conflict-resolution commit before `master` changes.

Combined validation on the PR #8 branch:

- local test suite: 57/57 passed;
- PostgreSQL 16 disposable gate: 66/66 passed;
- build: 0 errors and 0 warnings;
- format, diff check and migration drift: passed;
- vulnerable-package and sanitized secret scans: clean;
- residual disposable containers, volumes and networks: zero.

All three PRs remain open and in Draft state. No merge, deployment, production access, real migration or real-data operation was performed.

## Post-synchronization state - 2026-09-10

PR #8 now contains only the Secure Notification Delivery delta over the current `master`. Delivery uses typed `FamilyInvitation` and `PasswordRecovery` messages behind `IUserNotificationDeliveryService`. External delivery remains disabled by default. The implemented adapter is HTTP email; Email, WhatsApp, Telegram and SMS are future adapters, not implemented features.

Enabled configuration requires safe HTTPS origins/endpoints and external credentials. Timeout and retry are bounded, permanent failures are not retried, and the same non-sensitive correlation ID is reused as the provider idempotency key. This reduces duplicate delivery but does not guarantee exactly-once behavior.

Invitation persistence commits before the external call. Recovery provider failures remain internally contained and do not change the generic public response. Logs exclude destination, action URL, raw token, credential and provider response body. No outbox is included; it becomes required before multiple replicas, asynchronous guaranteed delivery or stronger retry guarantees.

Final gate results after synchronization:

- local suite: 61/61 passed;
- PostgreSQL 16 disposable suite: 70/70 passed, including migrations, concurrency, rollback and tenant isolation;
- build: zero errors and zero warnings;
- format and diff check: passed;
- migration drift: zero;
- vulnerable packages: zero for application and tests;
- sanitized secret scan: clean;
- disposable Docker containers, volumes and networks remaining: zero.

The merge decision remains subject to the final GitHub review gate. No deployment, production access, real migration, real data or external message delivery is authorized by this checkpoint.
