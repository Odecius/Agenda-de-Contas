# Checkpoint - pilot environment readiness - 2026-09-11

## Reference

- Branch: `agent/pilot-environment-readiness`.
- Base: `master` at `20bd8cf2d77d1847590828d8b394a63bd0f053df`.
- Production baseline remains `ContaStore + JSON`; production and the HP server were not accessed.

## Implemented safeguards

- explicit `Pilot` environment support while `MultiFamily:Enabled=false` remains the global default;
- fail-closed pilot profile: connection string absent, registration disabled, delivery disabled, Telegram disabled and legacy automatic backup disabled;
- generic `/health/ready` check for database reachability and migration currency;
- disposable rehearsal automation with explicit confirmation, generated credentials, random loopback ports, tmpfs and label-scoped cleanup;
- sanitized runbook for architecture, cutover, rollback, RPO/RTO, retention, observability, alerting and incidents;
- tester guide and feedback template that prohibit credentials and tokens.

## Rehearsal evidence

- source: generated JSON with two accounts and three payment rows, including one explainable logical duplicate;
- dry-run: passed without database writes;
- import: two accounts and two unique payments inserted;
- reconciliation: two source accounts, two imported accounts, three payment rows, two imported payments, one classified duplicate/skipped payment, zero unexplained difference;
- monetary reconciliation: GBP 45.50 and EUR 30.00, preserved separately; no currency conversion;
- idempotency: repeated import inserted zero accounts and zero payments;
- invalid-input failure: rejected without partial writes;
- backup: PostgreSQL custom-format dump created, non-empty, SHA-256 calculated and catalogue validated;
- restore: source database destroyed; dump restored into a fresh PostgreSQL 16 database;
- restored application: readiness, login, Family resolution, account list/create, invitation, recovery and Family switch passed;
- tenant isolation after switch: passed;
- measured laboratory RTO: 5 seconds from fresh database provisioning through restore and application validation;
- cleanup: zero labeled containers, volumes and networks.

## Objectives and decision

- proposed RPO: `<= 24h`, contingent on daily backup plus monitored off-host copy;
- proposed pilot RTO: `<= 30 minutes`; the laboratory result demonstrates feasibility but does not replace an operational drill;
- delivery remains disabled pending provider homologation;
- no real data, credential, migration, import, message, deploy or pilot user was used.

The repository/history scan available in this checkout did not find a concrete Telegram token value. It also cannot prove that an external historical credential was never exposed or already rotated. Per the pilot gate, the unresolved status is therefore `ROTATION REQUIRED` before real onboarding.

`PILOT ENVIRONMENT READINESS: NO-GO` until the Telegram credential status is recorded as `CONFIRMED SAFE` or `ROTATED`, and the proposed backup schedule/off-host copy are provisioned in the future isolated environment. The technical rehearsal itself passed.

## Next authorized step

Obtain operational confirmation or rotate the historical Telegram credential without exposing it, then provision the isolated pilot environment under explicit authorization and repeat the runbook gates. Do not deploy or onboard testers from this checkpoint.
