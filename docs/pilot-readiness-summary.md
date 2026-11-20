# Pilot readiness technical summary

## Architecture

The Pilot is a separate operating mode for the ASP.NET Core application. It uses ASP.NET Identity, server-side Family selection and tenant-aware repositories backed by an isolated PostgreSQL instance. The legacy JSON runtime remains a separate deployment path and is not a concurrent writer to Pilot data.

The Pilot database has dedicated storage and networking and does not expose PostgreSQL through a host port. Bootstrap administration, schema migration and application runtime use separated identities. The migration identity owns the dedicated schema; the runtime identity receives only the data and sequence permissions required by the application.

## Schema and migrations

The relational foundation contains three reviewed migrations. They are applied explicitly by an operator using the migration identity. Application startup checks readiness but does not apply migrations. The readiness endpoint reports a generic result for connectivity and migration currency without revealing topology or versions.

## Identity and tenant isolation

Individual login uses ASP.NET Identity. A user can hold memberships in one or more Families, with Owner, Admin or Member authorization evaluated within the active Family. The active Family is revalidated against membership, and repositories resolve it from server-side context rather than accepting arbitrary tenant authority from clients.

## Controlled administration

The initial Owner and Family are created only through `bootstrap-multi-family`. Pilot execution requires Multi-Family mode plus a temporary private gate; the committed default is disabled. Normal startup has no bootstrap side effect and no HTTP bootstrap endpoint exists.

Password recovery for an existing Pilot identity uses `reset-pilot-password` under a separate temporary private gate. It calls ASP.NET Identity reset APIs rather than editing `PasswordHash` or authentication tables directly. Production and unknown environments reject both administrative commands.

## Backup and restore

The repository contains fail-closed backup and off-host integrity controls, including manifest validation, SHA-256 verification and marker-last publication. Disposable rehearsals distinguish code-level validation from real operational evidence. The Pilot GO checkpoint records a sanitized operator-confirmed backup and disposable restore result without storing private targets or data.

## Runtime protections

- `Registration:Enabled=false` remains the Pilot default.
- Notification delivery and Telegram remain separately gated.
- `/health` provides liveness; `/health/ready` provides a generic database/migration readiness signal.
- Temporary bootstrap and reset inputs must be removed before normal runtime.
- Secrets and private deployment values remain outside the repository.

## Readiness statement

The current checkpoint records `PILOT ENVIRONMENT READINESS: GO` for the controlled Pilot sequence. External exposure, browser validation, transactional smoke testing, post-smoke backup and participant onboarding remain explicit future operational gates.
