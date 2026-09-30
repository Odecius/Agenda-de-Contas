# Checkpoint - Pilot backup provisioning blocked - 2026-09-30

## Scope

A read-only inventory was performed on the authorized HP host and Lenovo off-host candidate. No application, database, backup, firewall, account, key, mount, scheduler or production configuration was changed. No secret value, private address, username, hostname, personal path or backup content was collected or committed.

## Sanitized findings

- the HP runs a supported Ubuntu release with healthy free capacity, active Docker and existing PostgreSQL containers;
- the published legacy application remains active and its Telegram integration is configured and enabled; no value was read or exposed;
- multiple existing backup timers are active, including PostgreSQL-related jobs, but no existing off-host transport was detected;
- no database was identified unequivocally as the isolated Agenda-de-Contas pilot database;
- the Lenovo has sufficient NTFS capacity, an existing writable backup root, active SSH and private-overlay services, and no Agenda-de-Contas backup task;
- private-overlay connectivity from HP to Lenovo succeeds, but the Lenovo SSH port is not reachable from HP;
- the HP SSH identity available to this session does not have non-interactive administrative elevation;
- no dedicated pilot backup marker, restricted backup identity or end-to-end HP-to-Lenovo transport was provisioned.

## Stop conditions applied

Provisioning stopped before mutation because:

1. opening or changing the Lenovo firewall/overlay policy was not unambiguously authorized;
2. privileged HP changes would require interactive elevation or credential handling;
3. existing PostgreSQL backup timers require target/schedule review before another pipeline can be installed;
4. there is no clearly isolated real pilot database, so a daily timer cannot be safely targeted.

No attempt was made to bypass privileges, expose credentials, reuse a personal credential as a backup identity, dump an unidentified database or create a duplicate timer.

## State

- backup design: `READY`;
- backup infrastructure: `NOT PROVISIONED`;
- transport HP to Lenovo: network reachable, backup transport blocked;
- systemd service/timer: not installed;
- synthetic hardware rehearsal: not started because the secure transport and dedicated identity gates were not satisfied;
- Telegram credential status: `ROTATION REQUIRED`;
- pilot environment readiness: `NO-GO`.

## Required follow-up authorization and prerequisites

Before retrying, the operator must:

1. provide an approved method for privileged HP changes without sharing a password in chat or logs;
2. explicitly authorize a narrowly scoped Lenovo SSH/firewall or overlay policy change, or provide an already approved transport endpoint;
3. approve creation of a dedicated least-privilege Lenovo backup identity rather than a personal account;
4. identify the isolated pilot PostgreSQL target, or confirm that the next run remains synthetic-only with the real timer disabled;
5. review the existing PostgreSQL backup timers and approve reuse or a non-conflicting schedule.

Until those prerequisites are met, do not install the service, enable a timer, create keys/accounts, copy backups or run `pg_dump` against any existing database.
