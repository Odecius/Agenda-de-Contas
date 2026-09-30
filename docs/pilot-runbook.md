# Pilot environment runbook

## Scope and architecture

This runbook prepares a closed alpha for two Families and two to five invited testers. It is not a production deployment authorization.

The target is one isolated application instance running with `ASPNETCORE_ENVIRONMENT=Pilot`, one dedicated PostgreSQL database/user, persistent database storage, a separate encrypted backup destination, structured application logs and external liveness/readiness probes. Runtime and backup storage must not be shared with the current JSON production deployment.

Required state:

- `MultiFamily:Enabled=true`, with its connection string supplied externally;
- `Registration:Enabled=false` initially; enable only for the controlled signup window and disable immediately afterwards;
- `Delivery:Enabled=false` until a provider passes its separate security gate;
- legacy JSON is read-only migration input and rollback evidence, never a concurrent writer;
- `/health` is liveness; `/health/ready` verifies database connectivity and migration currency without exposing topology or versions.

The committed `appsettings.Pilot.json` is deliberately incomplete and fails closed without an external connection string. Secrets, hostnames and private paths never belong in the repository.

## Preconditions

- approved image digest mapped to a reviewed Git commit;
- isolated network, database and persistent storage;
- TLS termination and secure cookies verified;
- database credentials and Data Protection keys supplied through approved secret storage;
- enough free disk for database, one local backup and restore workspace;
- current schema migrations reviewed; migrations are applied by an explicit operator step, never application startup;
- Telegram historical-secret status is `CONFIRMED SAFE` or `ROTATED`;
- named operator, incident contact and maintenance window.

## Rehearsal

Run only against generated disposable resources:

```powershell
.\tests\run-pilot-environment-rehearsal.ps1 -ConfirmDisposable
```

The script generates credentials, starts PostgreSQL A on a random loopback port and tmpfs, performs validation/dry-run/import/reconciliation/idempotency/failure tests, creates a custom-format `pg_dump`, validates its catalogue, destroys A, restores into fresh PostgreSQL B, exercises the application, and removes the backup and all labeled Docker resources in `finally`.

The committed script has no production default, hostname or credential. Do not adapt it to real data. A real cutover requires a separately reviewed command sheet with explicit targets.

## Backup, retention and integrity

Proposed pilot policy, pending environment provisioning:

- automated PostgreSQL backup at least every 24 hours: proposed RPO `<= 24h`;
- keep 14 daily restore points on the pilot host;
- copy at least one verified backup off-host after each successful run and retain eight weekly restore points;
- encrypt off-host copies, restrict access and record a checksum and completion status;
- perform a disposable restore test before onboarding and at least monthly during the pilot;
- alert on backup failure, missing off-host copy, checksum failure or restore failure.

Retention values are targets, not claims about any current server. Provisioning must verify available capacity and the actual scheduler.

## Recovery objectives

- RPO proposal: `<= 24h`, supported only after daily backups and off-host copy monitoring are operational.
- laboratory restore measurement: recorded in the latest checkpoint.
- RTO proposal: `<= 30 minutes` for the closed alpha. This includes declaring the incident, identifying the restore point, provisioning the database, restore, application validation and reopening access. The short laboratory measurement is evidence for feasibility, not an operational guarantee.

## Cutover

### Pre-cutover

1. confirm approval, image/commit, health, free disk and database connectivity;
2. validate configuration names only; never print values;
3. disable registration and delivery;
4. notify testers of the maintenance window;
5. freeze legacy writes and record the freeze time;
6. create and checksum the final JSON backup and a PostgreSQL pre-change backup;
7. execute dry-run and reconcile counts, duplicate classifications and monetary totals by currency.

### Cutover

1. keep writes frozen;
2. apply reviewed migrations explicitly;
3. import into the approved target Family;
4. require unexplained reconciliation difference of zero;
5. start the Pilot runtime with registration and delivery disabled;
6. require liveness and readiness success;
7. run login, Family, account, invitation, recovery and isolation smoke tests;
8. open access only after the go/no-go owner signs off.

### Point of no return

The practical point of no return is the first accepted PostgreSQL write after pilot access reopens. Returning to the frozen JSON after that point loses or requires reconciliation of new writes. Before that point, rollback is a direct runtime/configuration reversal; after it, prefer restoring PostgreSQL or perform a separately designed reverse reconciliation.

### Rollback triggers

Rollback or stop immediately for failed reconciliation, readiness failure, authentication bypass, cross-tenant visibility, data loss/corruption, failed required backup, migration uncertainty or any CRITICAL/HIGH security finding.

### Rollback

1. stop access and preserve logs/evidence;
2. disable registration and delivery;
3. before the point of no return, disable the Pilot runtime and reopen the preserved JSON baseline only after validation;
4. after the point of no return, restore the last verified PostgreSQL backup into a fresh database;
5. validate schema, migrations, login, Families, accounts, invitations, recovery and tenant isolation;
6. document the incident and obtain approval before reopening.

## Observability and alerts

Collect structured logs with timestamp, severity, request/correlation ID, route template, status and duration. Never log passwords, tokens, destinations, authenticated URLs, connection strings or private topology. Retain pilot application logs for 30 days unless capacity/privacy review chooses a shorter period.

Minimum signals:

- liveness and readiness availability;
- HTTP 4xx/5xx counts and latency;
- login failures and lockouts without email addresses;
- invitation/recovery/delivery outcomes without tokens or destinations;
- reminder-worker success/failure per technical Family ID;
- database availability and storage utilization;
- backup completion, age, checksum and last successful restore test.

Alert priorities:

- CRITICAL: application/database down, tenant-isolation incident, backup/restore integrity failure;
- HIGH: repeated 5xx, systemic login/recovery failure or systemic delivery failure;
- MEDIUM: individual workflow failure or sustained performance degradation.

No real alerting channel is configured by this repository change. During the alpha, a named operator reviews structured logs and the health dashboard at least daily and after every alert.

## Incident response and stop conditions

For an ordinary defect: record, reproduce with synthetic data, prioritize, fix in a branch and re-run gates.

For a security/integrity event: `STOP PILOT` -> disable the relevant feature -> preserve evidence -> investigate -> rotate affected secrets -> restore if necessary -> complete an incident record -> obtain explicit restart approval.

Stop immediately for cross-tenant access, another Family's data appearing, data loss, authentication bypass, secret exposure, persistent corruption, a required backup without a viable alternative, or any CRITICAL/HIGH security issue.

## Entry and exit gates

Entry requires all application and PostgreSQL gates green, a successful backup/restore rehearsal, RPO/RTO mechanisms provisioned, secret status resolved, no CRITICAL/HIGH finding, approved tester list and rollback owner.

Exit succeeds when planned scenarios are completed, no unresolved CRITICAL/HIGH issue exists, feedback is triaged, restore capability remains valid and an explicit decision is made to stop, extend or promote the pilot.
