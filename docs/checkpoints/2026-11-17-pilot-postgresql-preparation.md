# Pilot PostgreSQL preparation checkpoint

Date: 2026-11-17

## Discovery

- Production default remains `ContaStore + JSON` with `MultiFamily:Enabled=false`.
- Pilot composition already fails closed without an external PostgreSQL connection string.
- Three reviewed EF Core migrations exist; the application does not migrate at startup.
- Disposable PostgreSQL 16, backup and restore harnesses already use synthetic data and isolated resources.
- No code or schema change is required to introduce an isolated Pilot database.

## Repository preparation

- Added a PostgreSQL 16 Compose definition with no host port, a dedicated internal network and a dedicated labelled volume.
- Added distinct external file-based bootstrap-administrator, migrator and runtime credentials.
- Added fail-closed initialisation for a non-elevated migrator role, a non-owning runtime role and an environment identity marker.
- Assigned database/schema ownership and explicit migrations only to the migrator; runtime receives DML and sequence privileges through migrator default privileges.
- Added a mode-aware verifier for database identity, ownership, schema creation, runtime DML grants, role attributes and unexpected databases.
- Documented dedicated backup namespace and disposable restore requirements.

## Real-environment boundary

No real target database, container, volume, network, secret path or backup path was identified or modified during repository preparation. Real provisioning must stop until an operator proves unique target identities and supplies legitimate elevation where required.

No existing PostgreSQL database or container was accessed, restarted, migrated or modified. No Telegram work or real-user onboarding occurred.

## Disposable validation

- Compose configuration and shell syntax: PASS.
- Dedicated volume, internal network and no host port: PASS.
- Database identity marker, segregated credentials, ownership and least-privilege role attributes: PASS.
- Existing three migrations applied only by the migrator role to a loopback-only disposable target: PASS.
- Runtime migration, CREATE TABLE, ALTER TABLE and DROP TABLE attempts rejected: PASS.
- Runtime CRUD, backup, restored-data validation and tenant-isolation paths: PASS.
- Local application tests: 63/63 PASS.
- PostgreSQL 16 gate: 73/73 PASS.
- Pilot seed, backup, restore, application validation and rollback rehearsal: PASS.
- Off-host backup, catalogue, checksum, retention, corruption and restore rehearsal: PASS.
- Hardened transport/verifier synthetic scenarios: PASS.
- Migration drift: zero.

The validation resources used generated credentials, synthetic data, random names and automatic cleanup. These results prove the repository design in disposable infrastructure; they do not prove real Pilot provisioning.
