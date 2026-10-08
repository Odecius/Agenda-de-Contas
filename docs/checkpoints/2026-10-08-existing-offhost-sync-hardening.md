# Checkpoint - Existing off-host sync hardening - 2026-10-08

## Scope

Implemented and tested a sanitized candidate for the existing HP-to-Lenovo bundle transport. No real host, destination, timer, service, key, backup, database, Telegram setting or production runtime was accessed or changed.

## Delivered

- generic Bash transport preserving hourly-compatible timestamp bundle discovery, SSH/SCP, `SHA256SUMS` and `BACKUP_OK`;
- strict local manifest validation before `sha256sum -c` or remote interaction;
- PowerShell destination verifier with flat-path enforcement and case-insensitive SHA-256 comparison;
- remote verification before temporary-marker copy and atomic final-marker rename;
- fail-closed revalidation of complete existing bundles without overwrite;
- retry-safe recopy and publication of incomplete bundles;
- sanitized events and optional atomic status file;
- synthetic harness for transport, corruption, manifest-security, marker and idempotency scenarios.

## Security properties

- bundle names and operational identifiers are allow-listed;
- remote paths must be absolute Windows paths with a restricted character set;
- PowerShell commands use encoded input after validation;
- manifests reject absolute paths, traversal, separators, empty names, malformed hashes, duplicates, symlinks and unlisted payloads;
- logs and status exclude hostnames, usernames, paths, keys, credentials and file contents;
- a complete corrupt remote bundle is preserved for investigation and causes non-zero exit.

## State

`HARDENING IMPLEMENTED / NOT YET DEPLOYED`

`REUSE EXISTING SYNC - APPROVED WITH HARDENING`

`PILOT ENVIRONMENT READINESS: NO-GO`

The real script and systemd units remain unchanged. Remote hardware validation, monitoring, restore proof, Telegram rotation and creation/identification of the isolated pilot PostgreSQL database remain required.

## Next authorized gate

After review and merge, perform a separately authorized controlled deployment: preserve and hash the current script, install both reviewed scripts while retaining private configuration, validate syntax, run synthetic transfer/corruption/restore gates, validate one one-shot execution, observe the next scheduled run and roll back to the preserved script on any failure.
