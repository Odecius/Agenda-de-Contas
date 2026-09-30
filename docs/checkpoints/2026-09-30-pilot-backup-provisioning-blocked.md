# Checkpoint - Pilot backup provisioning blocked - 2026-09-30

## Scope

A read-only inventory was performed on the authorized HP host and Lenovo off-host candidate. No application, database, backup, firewall, account, key, mount, scheduler or production configuration was changed. No secret value, private address, username, hostname, personal path or backup content was collected or committed.

## Corrected sanitized findings

- the HP runs a supported Ubuntu release with healthy free capacity, active Docker and existing PostgreSQL containers;
- the published legacy Agenda-de-Contas application still uses JSON and no database was identified unequivocally as its isolated pilot PostgreSQL database;
- the ABC Prospect database has an active daily local-backup timer with dump, checksum, retention and restore capabilities; its most recent observed execution succeeded;
- `postgres-lenovo-sync.timer` exists, is enabled and active, starts after boot and runs approximately hourly;
- the previous HP-to-Lenovo TCP/22 failure was a false negative caused by testing `/dev/tcp` under `sh`; a corrected `bash` test confirmed TCP/22 is reachable;
- the Lenovo has sufficient capacity, active OpenSSH and private-overlay connectivity;
- the HP SSH identity available to this session does not have non-interactive administrative elevation;
- the sync script is protected, so its destination, source scope, transport, checksum and retention remain `UNKNOWN` pending privileged read-only inspection;
- no dedicated pilot backup marker, isolated pilot database or proven end-to-end pilot backup and restore was established.

## Existing pipeline map

```text
ABC Prospect
  -> abc-prospect-backup.timer
  -> backup-postgres.sh
  -> local dumps
  -> UNKNOWN
  -> postgres-lenovo-sync.timer
  -> sync-postgres-backups-to-lenovo.sh
  -> UNKNOWN
  -> Lenovo
```

The diagram records observed components only. It does not prove that the sync timer consumes ABC Prospect dumps, that Lenovo is its actual destination, or that it provides the pilot backup. The existing pipeline must be inspected for safe reuse before any second pipeline is designed or provisioned.

## Sync failure-pattern analysis

The seven-day journal was analyzed locally on the host and only aggregate statistics were returned:

- journal entries inspected: `669`;
- success markers: `8`;
- failure markers: `318`;
- event windows grouped by minute: `167`;
- success-only windows: `8`;
- failure-only windows: `159`;
- mixed success/failure windows: `0`;
- authentication, connection, missing-path and checksum-specific markers recognized by the sanitized classifier: `0` each;
- failures occurred on every day inspected and across all hours, consistent with repeated hourly failure windows rather than a single isolated burst;
- last failure marker: `2026-09-30 07:35 BST`;
- last success marker: `2026-09-30 12:37 BST`;
- the latest observed service result was `success` with exit status `0`, and five success markers occurred after the last observed failure.

The failure text and protected script were not exposed. Therefore the historical failures cannot yet be correlated with Lenovo availability or assigned to authentication, transport, source, destination or another cause. The recent successful sequence suggests improvement, but it does not outweigh 159 failure-only windows in seven days.

Provisional sync health: `DEGRADED`.

## Telegram checkpoint

- configured: `YES`;
- enabled: `YES`;
- runtime: `ACTIVE`;
- recent successful-send markers: `YES`;
- credential defined: `YES`;
- credential exposed by this audit: `NO`;
- status: `ROTATION REQUIRED`.

Do not rotate the token without simultaneously updating the runtime secret because Telegram notifications are currently active. Rotation was not performed and no Telegram API call or message was made.

## Database map

- ABC Prospect: `abc_prospect`, owned by the ABC Prospect workload;
- shared/other platform: `abcserver`;
- Agenda legacy runtime: `JSON`;
- isolated pilot PostgreSQL database: `NOT YET IDENTIFIED`.

Neither `abc_prospect` nor `abcserver` may be treated as the pilot target. No timer may be configured for the pilot until an isolated target is identified and authorized.

## Stop conditions applied

Provisioning remains stopped before mutation because:

1. the protected sync implementation requires privileged read-only inspection before reuse can be evaluated;
2. the seven-day history contains repeated failure windows whose cause is still unknown;
3. existing PostgreSQL backup timers must be reconciled before another pipeline can be installed;
4. there is no clearly isolated real pilot database, so a daily pilot timer cannot be safely targeted;
5. no backup/restore rehearsal has established that the existing sync satisfies the pilot RPO, integrity, retention and recovery requirements.

No attempt was made to bypass privileges, expose credentials, reuse a personal credential as a backup identity, dump an unidentified database or create a duplicate timer.

## State

- backup design: `READY`;
- backup infrastructure: `NOT PROVISIONED` for the pilot;
- HP-to-Lenovo TCP/22: `REACHABLE`;
- existing sync timer: `ACTIVE`, approximately hourly, provisional health `DEGRADED`;
- existing sync destination/scope/checksum/retention: `UNKNOWN`;
- pilot systemd service/timer: not installed;
- synthetic hardware rehearsal: not started;
- Telegram runtime: `ACTIVE`;
- Telegram credential status: `ROTATION REQUIRED`;
- pilot environment readiness: `NO-GO`.

## Required privileged read-only facts

Before a reuse decision, confirm without printing secret values:

1. exact service identity and executable path;
2. environment-file paths only, not their contents;
3. script ownership and permissions;
4. source backup directory and workload/database scope;
5. destination class and transport method;
6. checksum verification and atomic-copy behavior;
7. local and remote retention behavior;
8. the cause of historical failures and whether the successful sequence is sustained.

Until those facts are established, do not install a service, enable a new timer, create keys/accounts, copy backups, rotate Telegram credentials or run `pg_dump` against any existing database.
