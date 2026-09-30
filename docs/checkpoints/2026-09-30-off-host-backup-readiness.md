# Checkpoint - Off-host backup readiness design - 2026-09-30

## Scope

Prepared the PostgreSQL daily/off-host backup design without accessing the HP server, Lenovo storage, a real database, real backup or production configuration.

## Delivered

- POSIX operational script using custom-format `pg_dump`, catalogue validation and SHA-256;
- explicit pre-marked local/off-host targets with no production defaults;
- atomic publication, conflict detection, pending-copy retry and bounded 30-day retention with seven-point minimum;
- sanitized status/log contract and external-encryption assertion;
- disposable PostgreSQL 16 harness covering success, failure, destroy, restore and application validation;
- scheduling, monitoring, recovery and real-provisioning plan.

## Disposable evidence

- synthetic migration/reconciliation/idempotency baseline: PASS;
- local backup, off-host copy, catalogue and checksum: PASS;
- duplicate handling and retention: PASS;
- database, unavailable destination, permission and invalid-target failures: PASS;
- corrupted copy and checksum mismatch: PASS;
- source destruction and fresh PostgreSQL restore: PASS;
- restored application readiness, login, Families, accounts, invitations, recovery and Family switch: PASS;
- measured laboratory RTO: 5 seconds;
- residual Docker resources: zero.

## State

`BACKUP DESIGN READY`

`OFF-HOST BACKUP NOT PROVISIONED`

RPO `<= 24h` and RTO `<= 30 min` remain conditional targets. They become operational claims only after separately authorized provisioning, monitoring and a restore drill in the isolated pilot environment.

`PILOT ENVIRONMENT READINESS: NO-GO` because the Telegram credential status remains `ROTATION REQUIRED` and real off-host backup remains unprovisioned.

## Next authorized gate

With explicit authorization naming both machines, provision the restricted encrypted destination/transport, local directory, markers, external PostgreSQL credential reference, daily `systemd` service/timer and monitoring. Execute the first backup and restore only in the isolated pilot environment, preserve all backups on rollback and do not restart the application/database unless a separately reviewed need appears.
