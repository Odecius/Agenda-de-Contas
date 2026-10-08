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

The verified existing HP-to-Lenovo architecture uses timestamp-named bundles, SSH/SCP, `SHA256SUMS` and `BACKUP_OK`. The hardened reusable implementation is [`deploy/sync-backup-bundles-offhost.sh`](../deploy/sync-backup-bundles-offhost.sh), with destination verification in [`deploy/verify-backup-bundle.ps1`](../deploy/verify-backup-bundle.ps1). Operational values are mandatory external configuration and are not committed.

The Bash transport validates the flat GNU manifest locally, rejects unsafe names and symlinks, verifies local hashes, copies only manifested payloads, invokes the remote verifier, transfers `BACKUP_OK` under a temporary name and atomically renames it after verification. A complete existing bundle is reverified instead of blindly skipped. A corrupt complete bundle fails without overwrite; an incomplete bundle may be recopied and verified.

The transport fails closed when no source backup bundles are discovered. An empty source can indicate a configuration, mount, storage or upstream backup-generation failure and is never reported as a successful no-op. Complete remote bundles must remain flat: every direct entry must be the manifest, completion marker or a manifested payload; unexpected directories, junctions, links and unlisted entries are rejected without deleting evidence.

`IMPLEMENTATION HARDENED` means the candidate and synthetic evidence exist in Git. It does not mean the real HP script, timer, service, Lenovo destination or monitoring was changed.

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

The transport-specific harness is `tests/run-existing-offhost-sync-hardening.ps1 -ConfirmDisposable`. It uses temporary directories, a network-disabled local container and mocked SSH/SCP actions. It exercises new, incomplete, partial, existing, corrupted and idempotent bundles plus malformed and malicious manifests. The PowerShell verifier itself runs locally against synthetic Windows directories.

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

## Controlled deployment and rollback plan

This plan is documentation only and must not be executed without explicit authorization:

1. stop before mutation and record the current service/timer state;
2. copy the current transport script to a root-only timestamped rollback file on the same host;
3. record SHA-256 and ownership/mode of the current script without publishing paths or values;
4. install the reviewed Bash candidate and Windows verifier while preserving all external private configuration;
5. restore privileged ownership and mode, then run Bash and PowerShell syntax validation;
6. use a new synthetic timestamp bundle to prove copy, remote SHA-256 and marker-last publication;
7. corrupt a separate synthetic copy and prove non-zero exit with no final marker;
8. restore the synthetic verified bundle into an isolated disposable database and validate it;
9. run one manually authorized one-shot service execution, without invoking the timer or touching unrelated bundles;
10. confirm sanitized status and monitoring, then observe the next scheduled execution;
11. if any gate fails, stop the timer if necessary, restore the saved script with its original ownership/mode, validate syntax, run one controlled verification and preserve all backup evidence.

Do not delete bundles during rollback. Do not integrate a pilot source until its isolated PostgreSQL database exists and is explicitly identified.

### Configuration migration requirement

The observed real unit did not expose an `EnvironmentFile`, while the hardened candidate requires external `SYNC_*` configuration. Direct script replacement is therefore unsafe.

`CONFIG MIGRATION REQUIRED: YES`.

A controlled deployment must extract the existing private values locally without logging them, migrate them into a root-owned/root-readable configuration mechanism, update the unit to load that configuration, validate permissions and run `systemctl daemon-reload` only in the authorized maintenance window. Rollback must restore the previous script, unit and configuration together, followed by another daemon reload and a controlled one-shot validation.

The PowerShell verifier must also be installed separately on the Windows destination in a restricted directory. Record its checksum and ACL, and make it non-modifiable by the backup identity where practical. The configured verifier path remains private. Rollback must preserve or remove that verifier according to the approved change record without touching backup bundles.

`REMOTE VERIFIER: CODE READY / NOT DEPLOYED`.
