# Isolated Pilot PostgreSQL

## Status

Repository configuration only. This directory does not prove that a real pilot database, role, volume, backup or restore target has been provisioned.

## Architecture

The Pilot database runs in its own PostgreSQL 16 container, internal Docker network and named volume. It exposes no host port. Bootstrap, migration and runtime credentials come from separate files outside the repository.

The migration role owns the dedicated database and `public` schema and is used only for explicit EF Core migrations. It has no global elevated PostgreSQL attributes. Default privileges grant runtime DML on tables and required sequence access to the application role.

The runtime application role owns neither the database nor schema, cannot create schema objects and has no superuser, database-creation, role-creation, replication or row-level-security bypass attribute.

The committed identifiers are generic safety controls:

- database: `agenda_pilot`;
- migration role: `agenda_pilot_migrator`;
- application role: `agenda_pilot_app`;
- environment marker: `agenda-pilot-v1`.

Existing PostgreSQL containers, databases, volumes, credentials and networks are never valid inputs to this Compose project.

## Required private inputs

The operator supplies these values outside the repository:

- `PILOT_DB_ENV_FILE`: root-readable environment file derived from `pilot-db.env.template`;
- `PILOT_DB_ADMIN_PASSWORD_FILE`: generated administrator password file;
- `PILOT_DB_MIGRATOR_PASSWORD_FILE`: generated migration-role password file;
- `PILOT_DB_APP_PASSWORD_FILE`: generated application password file;
- optionally unique `PILOT_DB_VOLUME_NAME` and `PILOT_DB_NETWORK_NAME` values approved during deployment.

Password files must contain at least 32 URL-safe random characters and must never be printed, committed or reused. The application connection string is assembled in the private deployment environment and is not stored here.

## Fail-closed provisioning gates

Before creating resources, the operator must prove that the selected Compose project, volume and network do not already exist and that no existing PostgreSQL resource is being reused. If any name or target is ambiguous, stop.

After initialisation, run `verify-pilot-database.sh` once as the migration role with `PILOT_VERIFY_MODE=migrator`, then after migrations as the application role with `PILOT_VERIFY_MODE=runtime`. It verifies identities, ownership, global attributes, schema creation denial, runtime DML grants and absence of unexpected non-template databases without printing credentials or topology.

Migrations are applied explicitly with `agenda_pilot_migrator` only after the migration identity verifier passes. The runtime connection uses `agenda_pilot_app`; it must fail to initialise an empty schema or perform DDL. The application never runs migrations at startup. No migration is required beyond the three existing reviewed migrations.

## Backup and restore boundary

Pilot backups use a dedicated marked local directory and a dedicated bundle namespace for the existing hardened off-host transport. They must not share retention scope with another database. `deploy/backup-postgresql.sh` continues to require standard external PostgreSQL authentication settings and does not log them.

A restore proof must use a fresh disposable PostgreSQL target with independent storage and generated credentials. Passing `pg_restore` is insufficient: run the existing Pilot validation mode to compare synthetic Families, users, accounts, payments and relationships. Never restore over the active Pilot database or any pre-existing database.

## Application boundary

`appsettings.Pilot.json` already provides safe defaults: Multi-Family enabled, registration disabled, delivery disabled, Telegram disabled and legacy automatic backup disabled. A real application deployment, TLS exposure or pilot-user onboarding requires a separate authorisation after database, backup and restore evidence is reviewed.
