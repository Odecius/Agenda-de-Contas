# Bill Scheduler / Agendador de Contas

A .NET 8 web application for managing recurring bills, monthly due dates, payments and reminders.

## Project Overview

Agendador de Contas began as a small JSON-backed bill tracker and evolved into a more structured ASP.NET Core application. The current production runtime remains intentionally simple and stable, while a PostgreSQL, Identity and multi-family architecture is developed behind a disabled feature flag.

## Problem It Solves

Recurring expenses are easy to miss when information is spread across notes and calendars. The application centralizes accounts, calculates monthly due dates, tracks payments and sends daily reminders without incorrectly combining totals from different currencies.

## Current Features

- Recurring and fixed-duration bills
- Monthly due-date and payment tracking
- Country and currency support for GBP, EUR and BRL
- Telegram reminders
- Configurable reminder schedule and timezone
- CSV export
- Manual and automatic JSON backups with guarded restore
- Health check and access-protection middleware
- Responsive HTML, CSS and JavaScript interface
- Docker support and automated tests

## Architecture

### Current production runtime

- ASP.NET Core 8 Minimal APIs
- `ContaStore` with JSON persistence
- Legacy access protection based on a secure cookie
- Background reminder and backup services
- Docker Compose deployment on Linux

This is still the active production path. PostgreSQL is not required when `MultiFamily:Enabled=false`, which remains the default.

### Multi-family evolution

The repository also contains a controlled relational foundation for:

- PostgreSQL and Entity Framework Core
- ASP.NET Core Identity
- Individual user accounts
- Family membership with Owner, Admin and Member roles
- Server-side active-family selection
- Tenant-aware repositories and APIs
- Explicit `FamilyId` isolation
- Family-specific settings and Telegram configuration
- An idempotent administrative bootstrap
- A controlled, transactional JSON-to-PostgreSQL migrator

The multi-family mode is limited to development and testing. It does not run automatically, does not execute migrations at startup and has not replaced the JSON production runtime.

## Security

- Tenant identity is resolved server-side rather than accepted from request payloads.
- Cross-family reads and writes are covered by integration tests.
- Mutations use antiforgery protection.
- Identity includes password hashing, lockout and rate limiting.
- The last active Owner cannot be silently removed or downgraded.
- Telegram tokens and production credentials stay outside source control.
- The migration workflow supports validation, dry-run, idempotency and transactional rollback.

See [SECURITY.md](SECURITY.md) and [the multi-family operational documentation](docs/multi-family-operational-flow.md).

## Testing

The test harness covers the JSON domain, backup/restore behavior, Identity, tenant selection, role authorization, cross-family isolation, PostgreSQL constraints, the migration workflow and multi-family reminders. PostgreSQL-specific scenarios run against a disposable PostgreSQL 16 instance.

```bash
dotnet build
dotnet run --project tests/AgendadorContas.Tests
dotnet format --verify-no-changes
```

## Local Development

```bash
dotnet restore
dotnet run
```

Development secrets should be supplied with .NET User Secrets or environment variables. No real credential belongs in the repository.

## Current Status

- **Production:** stable single-family runtime using `ContaStore + JSON`.
- **Implemented behind a controlled flag:** PostgreSQL schema, Identity, tenant isolation, multi-family APIs, operational UI, reminder processing and migration tooling.
- **Not completed:** production cutover, real JSON import, production activation of PostgreSQL/multi-family, invitation flow and distributed session/worker coordination.

## Key Lessons Learned

- A safe migration path is more important than replacing working persistence quickly.
- Multi-tenancy requires server-side authorization at every data boundary.
- Feature flags can preserve a stable runtime while a replacement architecture is validated.
- Backups need guarded restore behavior and explicit retention rules.
- Database constraints and disposable-provider tests complement application-level validation.

## Roadmap

- Review and plan the production cutover separately.
- Add invitation and password-recovery workflows.
- Replace in-memory session coordination before multiple replicas.
- Add distributed coordination for background reminders.
- Expand browser-level testing and reporting.

Detailed decisions and phase history are available in [ROADMAP.md](ROADMAP.md), [DECISIONS.md](DECISIONS.md) and [CHANGELOG.md](CHANGELOG.md).
