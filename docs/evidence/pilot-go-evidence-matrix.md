# Pilot GO evidence matrix

## Evidence levels

- **Repository-evidenced:** supported by versioned design, implementation notes, tests or checkpoints.
- **Operator-confirmed:** reported from the controlled Pilot operation, but the underlying private evidence is intentionally not committed.
- **Publication-evidenced:** a sanitized artifact is present in Git and supports the bounded claim made from it.
- **Not yet publication-evidenced:** safe public evidence is not yet present in the repository.

| Category | Claim | Repository evidence | Test/checkpoint/reference | Status | Publication-safe wording |
| --- | --- | --- | --- | --- | --- |
| Database isolation | Pilot uses a dedicated PostgreSQL 16 lifecycle, network and volume with no host port by default. | `deploy/pilot/README.md`; `DECISIONS.md` | `2026-11-17-pilot-postgresql-preparation.md` | Repository-evidenced; operation operator-confirmed | “The Pilot was designed around an isolated PostgreSQL lifecycle with no host database port.” |
| Least privilege | Bootstrap, migration and runtime identities are separated; runtime ownership and DDL are denied. | `deploy/pilot/README.md`; `tests/run-pilot-role-hardening.ps1` | PostgreSQL preparation checkpoint | Repository-evidenced; real result operator-confirmed | “Disposable tests validated the role boundary; the controlled Pilot check reported the same boundary.” |
| Migration integrity | Three reviewed migrations are explicit and never run at startup. | `deploy/pilot/README.md`; `docs/pilot-runbook.md` | Pilot preparation and GO checkpoints | Repository-evidenced; 3/3 application operator-confirmed | “Migrations are an explicit operator action, not an application startup side effect.” |
| Backup | Backup creation uses fail-closed integrity controls. | `docs/off-host-backup.md`; `deploy/backup-postgresql.sh` | Off-host hardening checkpoint | Repository-evidenced; real Pilot backup operator-confirmed | “The Pilot GO check reported a completed backup; private targets and contents remain outside Git.” |
| Restore | A backup was restored into a disposable target and validated. | `tests/run-pilot-environment-rehearsal.ps1`; `docs/pilot-runbook.md` | GO checkpoint | Operator-confirmed | “A disposable restore was reported during the controlled Pilot validation.” |
| Authentication | Individual ASP.NET Identity login is available in Multi-Family mode. | `docs/multi-family-runtime-isolation.md`; `docs/multi-family-operational-flow.md` | GO checkpoint | Repository-evidenced; real login operator-confirmed | “The controlled Pilot smoke check reported successful Identity login.” |
| Administrative bootstrap | First Owner and Family creation is CLI-only, gated, disabled by default and absent from startup/HTTP. | `docs/checkpoints/2026-11-17-pilot-controlled-bootstrap.md`; `DECISIONS.md` | Bootstrap checkpoint | Repository-evidenced; real execution operator-confirmed | “Initial administration used an explicit temporary CLI gate.” |
| Administrative password reset | Reset is CLI-only and uses Identity APIs without direct hash manipulation. | `docs/checkpoints/2026-11-17-pilot-controlled-password-reset.md`; `docs/multi-family-operational-flow.md` | Password-reset checkpoint | Repository-evidenced; real execution operator-confirmed | “Recovery used the Identity reset flow behind a temporary private gate.” |
| Tenant isolation | Family context is server-side and repositories do not accept client tenant authority. | `docs/multi-family-tenant-repositories.md`; `docs/multi-family-runtime-isolation.md` | PostgreSQL disposable suites in checkpoints | Repository-evidenced in disposable tests | “Disposable PostgreSQL tests exercised cross-Family isolation and server-side tenant selection.” |
| Application readiness | Liveness and migration-aware readiness endpoints are defined and sanitized. | `SECURITY.md`; `docs/pilot-runbook.md` | GO checkpoint | Repository-evidenced; real PASS operator-confirmed | “The controlled Pilot check reported passing liveness and readiness probes.” |
| Registration protection | Public registration is independently disabled by default. | `appsettings.Pilot.json`; `docs/pilot-runbook.md` | Bootstrap and password-reset checkpoints | Repository-evidenced; runtime state operator-confirmed | “Public registration remained disabled during controlled administration.” |
| Temporary-secret cleanup | Temporary bootstrap/reset inputs were removed before normal runtime. | Runbook requires cleanup. | GO checkpoint | Operator-confirmed; NOT YET PUBLICATION-EVIDENCED independently | “The operator reported removal of temporary administrative inputs before normal runtime.” |
| Ingress boundary | Tunnel ingress and the database use separate Docker networks; the tunnel is not a database-network member. | Pilot runbook and network design; external-validation checkpoint | External validation checkpoint | Design repository-evidenced; real memberships operator-confirmed; independently NOT YET PUBLICATION-EVIDENCED | “The operator reported a dedicated ingress network separated from the database network.” |
| Public HTTPS and readiness | A dedicated tunnel routed public HTTPS to the Pilot and external `/health/ready` returned ready. | Readiness endpoint and Pilot runbook | External validation checkpoint | Repository-evidenced capability; real route/result operator-confirmed; independently NOT YET PUBLICATION-EVIDENCED | “The operator reported successful public HTTPS routing and an external ready response.” |
| Browser authentication | Owner authentication succeeded through the public HTTPS route. | Identity and authentication implementation | External validation checkpoint | Repository-evidenced capability; real result operator-confirmed; independently NOT YET PUBLICATION-EVIDENCED | “The operator reported successful Owner authentication through the public route.” |
| Transactional smoke | Synthetic create/read/edit/payment/unpayment/cleanup completed through the browser flow. | Tenant-aware repositories and UI tests | External validation checkpoint | Repository-evidenced capability; real result operator-confirmed; independently NOT YET PUBLICATION-EVIDENCED | “The operator reported a completed synthetic transactional smoke test with cleanup.” |
| Post-smoke recovery | A custom-format backup was restored into disposable PostgreSQL 16; exit code 0, public schema and 3/3 migrations were verified; target removed. | Backup/restore harnesses and runbook | External validation checkpoint | Repository-evidenced method; real result operator-confirmed; independently NOT YET PUBLICATION-EVIDENCED | “The operator reported a successful disposable restore of the post-smoke backup, including all three migrations.” |

## Publication gaps

The repository intentionally does not contain private operational logs, screenshots, credentials, topology or participant records. Before making strong public operational claims, prepare sanitized evidence for:

- the migration result and readiness response;
- runtime-role denial/grant checks in the real Pilot;
- backup and disposable restore completion;
- administrative-input cleanup;
- external tunnel/network membership and readiness results;
- authenticated browser and transactional smoke tests;
- post-smoke backup/restore output and disposable-target cleanup.

Until such artifacts are reviewed, these claims must retain the label **operator-confirmed** rather than being presented as independently reproducible public evidence.
