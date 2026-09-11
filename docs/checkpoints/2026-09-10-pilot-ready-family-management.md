# Checkpoint - pilot-ready family management - 2026-09-10

## Reference

- Branch: `agent/pilot-ready-family-management`.
- Base: `master` at `6a3539b20ab2b4d42bafe90270ccbf554cd2f82c`.
- Production baseline remains the documented `ContaStore + JSON` runtime.

## State

Controlled self-registration, atomic Family creation, initial Owner assignment, Owner role administration and the existing secure multi-Family selector are integrated behind the development/testing feature gate. `Registration:Enabled=false` and `MultiFamily:Enabled=false` remain the defaults.

The model supports multiple Owners and enforces at least one active Owner in transactional membership operations. Joining an existing Family continues exclusively through secure invitations. No provider, credential, real identity, production database or operational metadata is part of this checkpoint.

## Validation completed

- local suite: 63/63 passed, 0 failed and 0 skipped;
- PostgreSQL 16 disposable suite: 73/73 passed, including signup concurrency, rollback, Owner safety and tenant isolation;
- real local host and browser flow with synthetic users: signup, login/logout, recovery, account creation, invitation acceptance, Family administration and switch between two Families;
- responsive UI reviewed at desktop and mobile breakpoints; security assertions remain automated and do not depend on pixel comparison;
- build: 0 errors and 0 warnings; format and diff checks passed;
- migration drift: zero; vulnerable NuGet packages: zero;
- sanitized changed-file scan found no real secret or private operational metadata. Test credentials and local disposable addresses are synthetic;
- no Telegram token evidence was found in the Git history available to this checkout; the existing operational confirmation/rotation TODO remains open until independently evidenced;
- disposable Docker cleanup: 0 containers, 0 volumes and 0 networks.

## Pilot readiness decision

`PILOT READINESS: READY` for a closed alpha with two Families and two to five synthetic users in an isolated environment. This is an application-readiness decision only: it does not authorize production activation, real-data import, provider delivery or deployment.

## Remaining work

- independent review and merge decision;
- pilot environment and rollback/cutover runbook;
- rehearsal of JSON import and backup/restore using approved copies only;
- distributed sessions, rate limits, Data Protection and workers before multiple replicas;
- external delivery provider homologation under a separate security gate.

## Next milestone

Prepare the isolated pilot environment and rehearsal plan. Do not deploy, import real JSON or activate production as part of this checkpoint.
