# Controlled Pilot password reset checkpoint

Date: 2026-11-17

## Scope

The new `reset-pilot-password` command is CLI-only. It requires Multi-Family and, in Pilot, the temporary private `PilotPasswordReset:Enabled` gate. The committed default is false. Development and Testing permit local administration; Production and unknown environments always reject.

## Identity and privacy properties

- active existing users only, with a generic equivalent error for unknown/inactive identities;
- `UserManager.GeneratePasswordResetTokenAsync` and `ResetPasswordAsync` only;
- no direct password hashing or `PasswordHash` write;
- existing Identity password validators remain authoritative;
- missing inputs identify only the missing configuration key;
- output/logs contain no email, password, token or User ID;
- no HTTP administrative reset endpoint;
- normal startup never performs a reset;
- public registration remains disabled;
- no Family, membership, account or payment change;
- no schema change or migration.

## Disposable validation

- local application suite: 68/68 PASS;
- PostgreSQL 16 suite: 78/78 PASS;
- disposable CLI reset and old/new login verification: PASS;
- invalid password, unknown/inactive identity and unchanged Family data: PASS;
- normal Pilot startup, registration-disabled and no-HTTP-endpoint checks: PASS;
- CLI output private-input check: PASS;
- disposable resource cleanup: PASS.

## Operational boundary

No real reset, database access, deployment or user modification was performed. A later separately authorized operation must provide the gate and reset values through protected temporary configuration, run the command once, remove those values, and then validate login with the gate disabled.
