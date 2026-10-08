# Checkpoint - Verified existing off-host backup pipeline - 2026-09-30

## Scope

A read-only inventory and authorized privileged inspection were performed on the existing HP-to-Lenovo backup transport. No application, database, backup, firewall, account, key, scheduler, Telegram configuration or production data was changed. No secret value, private address, username, hostname, personal path or remote destination was collected or committed.

## Verified timer and service

- `postgres-lenovo-sync.timer` is active and enabled;
- it starts five minutes after boot and runs approximately every hour;
- persistent scheduling is enabled;
- `postgres-lenovo-sync.service` is a one-shot system service;
- the service uses the default privileged system identity;
- the transport script is owned by the privileged system account and has mode `750`;
- the latest inspected execution completed successfully with exit status `0`.

The real paths, remote host, remote user, SSH identity and destination remain intentionally omitted.

## Verified transport behavior

The script fails closed, applies a restrictive file-creation mask and defines separate source-root, source-directory, remote-host, remote-user, remote-directory, SSH-key and known-hosts settings.

Transport capabilities:

- SSH: `YES`;
- SCP: `YES`;
- SHA-256: `YES`;
- rsync: `NO`;
- SFTP: `NO`;
- `pg_dump`: `NO`;
- `pg_restore`: `NO`.

The script does not create PostgreSQL backups. It transports previously generated backup bundles.

## Source discovery and local validation

The transport discovers timestamp-named backup directories directly below its configured source root. It is not hardcoded to a specific database in the inspected transport logic.

For each candidate bundle, the script:

1. requires a `BACKUP_OK` completion marker;
2. requires `SHA256SUMS`;
3. ignores incomplete bundles;
4. validates the local bundle with `sha256sum -c` before transfer.

Local checksum validation: `CONFIRMED`.

## Remote publication safeguard

Before copying, the script checks over SSH whether the remote bundle already has `BACKUP_OK`. An already published bundle is skipped. Otherwise it:

1. creates the remote bundle directory;
2. copies all bundle files except `BACKUP_OK` using SCP;
3. copies `BACKUP_OK` last.

Consequently, a partial copy does not receive the completion marker. This is a valid publication safeguard, but it is not equivalent to remote checksum verification.

## Seven-day execution health

The systemd result fields, rather than keyword matching of message text, show:

- successful completions: `167`;
- failed service results: `1`;
- non-zero process exits: `1`.

The single observed failure was termination by `SIGTERM` during an authorized server reboot. Normal executions preceded the reboot and resumed successfully after boot. The current service result is successful with exit status `0`.

The previously reported `318` failure markers came from broad keyword matching and did not represent 318 failed service executions. That metric is superseded by the verified systemd results.

Existing sync health: `HEALTHY BASELINE / HARDENING REQUIRED`.

## Remaining integrity gap

The script verifies SHA-256 locally before SCP, but remote checksum recomputation after transfer was not evidenced. The current publication sequence is therefore:

```text
local checksum PASS
  -> SCP data
  -> SCP BACKUP_OK
  -> published
```

The required hardened sequence is:

```text
local checksum PASS
  -> SCP data
  -> remote checksum verification
  -> PASS
  -> BACKUP_OK publication
```

Remote checksum hardening: `REQUIRED`.

## Architecture decision

Do not create a second HP-to-Lenovo sync pipeline.

Decision: `REUSE EXISTING SYNC - APPROVED WITH HARDENING`.

Minimum future hardening:

1. recompute and validate SHA-256 at the remote destination before publication;
2. publish `BACKUP_OK` only after remote validation passes;
3. improve status reporting and monitoring;
4. run synthetic transfer, failure and restore tests;
5. integrate the future isolated pilot backup source without disturbing existing workloads.

No hardening was implemented in this documentation-only change.

## Telegram checkpoint

- configured: `YES`;
- enabled: `YES`;
- runtime: `ACTIVE`;
- recent successful sends: `YES`;
- credential defined: `YES`;
- credential exposed: `NO`;
- status: `ROTATION REQUIRED`.

Do not revoke or rotate the token without simultaneously updating the active runtime secret. No Telegram configuration, API call or message was performed.

## Database map

- ABC Prospect: `abc_prospect`, owned by the ABC Prospect workload;
- shared/other platform: `abcserver`;
- Agenda legacy runtime: `JSON`;
- isolated pilot PostgreSQL database: `NOT YET CREATED / IDENTIFIED`.

Neither `abc_prospect` nor `abcserver` may be used as the pilot target.

## State and remaining blockers

- existing off-host transport: `VERIFIED`;
- existing sync health: `HEALTHY BASELINE / HARDENING REQUIRED`;
- remote checksum: `NOT EVIDENCED`;
- pilot backup target: `NOT YET CREATED / IDENTIFIED`;
- synthetic transfer/restore rehearsal: `NOT YET COMPLETED`;
- Telegram credential: `ROTATION REQUIRED`;
- pilot environment readiness: `NO-GO`.

Until separately authorized, do not alter the real script or timer, create a pilot database, use another workload's database, execute a real backup or restore, rotate Telegram credentials, activate MultiFamily or deploy.
