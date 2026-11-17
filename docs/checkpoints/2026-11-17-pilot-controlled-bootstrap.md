# Controlled Pilot bootstrap checkpoint

Date: 2026-11-17

## Scope

The existing `bootstrap-multi-family` CLI command may run in Pilot only when Multi-Family is enabled and the temporary private `PilotBootstrap:Enabled` gate is explicitly true. The committed default is false. Development and Testing retain their existing behavior; Production and unknown environments always reject the command.

## Safety properties

- no public bootstrap endpoint;
- public registration remains disabled and independent;
- normal startup never creates a user, Family or membership;
- email, password and Family name come only from private runtime configuration;
- missing values identify only the missing configuration key;
- logs and command output contain no email, password, User ID or Family ID;
- idempotent replay confirms valid state, while conflicting state fails closed;
- no schema change or migration.

## Operational boundary

This repository change does not authorize or perform a real bootstrap. A later controlled operation must enable the gate only for the explicit command, then remove the gate and all temporary bootstrap values before normal Pilot startup. No real database, existing container, Telegram configuration or participant data was accessed.

## Disposable validation

- local application suite: 65/65 PASS;
- PostgreSQL 16 suite: 75/75 PASS, including two-Family bidirectional isolation;
- explicit Pilot CLI bootstrap: first run, idempotent replay and conflict fail-closed PASS;
- CLI output secret/email exposure check: PASS;
- normal Pilot startup, registration-disabled and no-HTTP-bootstrap checks: PASS;
- Docker publish: PASS;
- migration drift: zero;
- format, diff, secret and private-metadata checks: PASS.
