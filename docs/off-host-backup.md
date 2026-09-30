# PostgreSQL backup and off-host copy

## Status and scope

`BACKUP DESIGN READY` means the repository contains a reviewed backup mechanism and a disposable proof. It does not mean that a real scheduler, storage target, key, alert or restore drill exists. Until those items are provisioned and evidenced, the operational status remains `OFF-HOST BACKUP NOT PROVISIONED`.

The design is for the isolated Pilot PostgreSQL database only. It must not target the current JSON production deployment, a shared PostgreSQL database or an unapproved directory.

## Flow

`Pilot PostgreSQL -> local custom-format dump -> catalogue/hash validation -> verified mounted off-host copy -> retention -> status/alert consumer -> controlled restore drill`

The implementation is [`deploy/backup-postgresql.sh`](../deploy/backup-postgresql.sh). It uses `pg_dump --format=custom`, validates the catalogue with `pg_restore --list`, writes SHA-256 manifests and publishes files through temporary names followed by an atomic rename. Existing files are never silently replaced: an existing destination must match its manifest and the source checksum.

The script accepts a simple database name only. PostgreSQL network and authentication settings use the standard external `PGHOST`, `PGPORT`, `PGUSER` and `PGPASSFILE`/secret environment contract. Do not pass a connection string or password as a script argument. The script never logs those values.

## Explicit targets

Both `BACKUP_LOCAL_DIR` and `BACKUP_OFFHOST_DIR` are mandatory absolute directories. They must be different, cannot be the filesystem root or contain whitespace, and must already contain `.agendador-backup-target` with the exact content `agendador-postgresql-backup-v1`.

The script never creates or guesses a destination. Retention deletes only files matching `agendador-postgresql-YYYYMMDDTHHMMSSZ.dump` and their checksum manifests inside a validated marked directory.

## Frequency, retention and capacity

- run once every 24 hours; this supports an RPO target of `<= 24h` only after scheduling and monitoring are proven;
- retain 30 daily restore points locally and 30 daily restore points off-host;
- always keep at least seven restore points even if they are older than the retention window;
- verify free capacity during provisioning and alert before space exhaustion.

Thirty days provides a monthly investigation window without introducing weekly/monthly retention complexity before actual backup size and change rate are measured. Reassess capacity and retention after the first seven real backups. A smaller RPO requires WAL archiving/PITR or more frequent dumps and is outside this milestone.

## Off-host transport and retry

The script writes to a mounted destination. The mount may be provided by a restricted SSH/SFTP/rsync-backed mechanism or equivalent managed storage, but transport setup remains an infrastructure responsibility. Use a dedicated key and account restricted to the approved backup target; never embed credentials in the repository, unit file or command line.

If the destination is unavailable, unmarked, read-only or corrupt, the run fails non-zero and retains the verified local dump. At the next run, every valid local dump missing from the destination is copied again. There is no infinite retry. A matching destination is accepted idempotently; a conflicting or corrupted file stops the run rather than being overwritten.

## Verification and monitoring contract

A successful run requires a non-empty dump, readable catalogue, valid local SHA-256, matching destination hash and completed retention. The process exit code is authoritative. Sanitized events and `backup-status.env` contain only timestamp, operation, result, duration, generated filename and checksum status.

A scheduler or monitoring agent must alert on a non-zero exit, missing/old success status, `result != success`, backup age over 24 hours, copy/checksum failure, retention failure or overdue restore test. No real alert channel is configured here.

## Encryption

The pilot requires encryption at rest on off-host storage and encrypted transport. The script deliberately does not implement custom cryptography. Provisioning must use a consolidated storage/filesystem encryption facility and set `BACKUP_OFFHOST_ENCRYPTION_ASSERTION=external-managed-encryption` only after an operator verifies it. This variable is an operational assertion, not cryptographic proof.

## Disposable proof

Run only against generated Docker resources:

```powershell
.\tests\run-off-host-backup-rehearsal.ps1 -ConfirmDisposable
```

The harness seeds synthetic multi-family data, creates and verifies local/off-host backups, tests idempotency and retention, simulates database failure, unavailable/invalid/read-only destinations and corruption/checksum mismatch, destroys the source database, restores into fresh PostgreSQL 16 and runs application validation. It uses tmpfs, random credentials, loopback-only ports, labels and `finally` cleanup.

## Scheduling template

On the future Linux pilot host, use a dedicated unprivileged service account and a `systemd` oneshot service plus daily timer. The service should load an external root-readable environment/credential file, require the off-host mount, execute the reviewed script by absolute path and use `OnFailure=` or the host monitoring agent to consume the non-zero exit/status file. Use `Persistent=true` so a missed run is attempted after the host returns. Apply a bounded start timeout and never configure automatic application or database restart.

Do not copy repository examples directly into a real host without replacing and reviewing every target. Actual unit names, paths, mount and secret locations belong in private infrastructure documentation.

## Restore procedure

1. Stop or isolate application writes; preserve logs and the original database.
2. Select a verified restore point and re-check its SHA-256 and catalogue.
3. Create a fresh PostgreSQL database with approved ownership.
4. Restore with `pg_restore --no-owner --no-privileges` in an explicit maintenance window.
5. Confirm migrations, readiness, login, Families, memberships, settings, accounts, constraints and tenant isolation.
6. Reopen access only after an operator records the result and approval.

The target RTO is `<= 30 minutes`. The disposable rehearsal demonstrates feasibility but cannot establish operational RTO until the real isolated environment is measured.

## Real provisioning gate

Real provisioning requires separate explicit authorization for both machines. It will create or change backup directories/markers, a dedicated account and key, encrypted off-host storage, a restricted mount/transport, an external database credential reference, a `systemd` service/timer and monitoring. The first run must prove backup, copy, checksum and a restore into a separate database.

No application or database restart should be required. Expected risk is temporary database/disk/network I/O; schedule the first run in a maintenance window. Rollback is to disable the timer, unmount/disable the transport and remove new unit/configuration files while preserving every verified backup and leaving the application/database unchanged.
