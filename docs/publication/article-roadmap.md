# Future article roadmap

No item in this roadmap is approved for publication. Each article requires a privacy review, factual review and author approval.

| Order | Working title | Central engineering question | Evidence available | Evidence still missing | Originality |
| --- | --- | --- | --- | --- | --- |
| 1 | Tenant Isolation Beyond `FamilyId` | How can tenant authority remain server-side across repositories, authorization and relational constraints? | Runtime-isolation and tenant-repository docs; disposable PostgreSQL isolation tests | Publication-safe diagrams and concise test excerpts | High: connects application context, authorization and database integrity |
| 2 | From Local JSON to PostgreSQL Without a Forced Cutover | How can a working local-state application evolve incrementally while preserving rollback and reconciliation? | Migration plan, migrator design, dry-run/idempotency checkpoints | Sanitized cutover evidence; final reconciliation artifact | High: emphasizes coexistence boundaries instead of a big-bang rewrite |
| 3 | Controlled Administration Without Public Registration | How do CLI-only bootstrap and password recovery avoid permanent administrative endpoints? | Bootstrap/reset decisions, checkpoints and tests | Sanitized operational sequence approved for publication | High: two lifecycle operations under the same temporary-gate principle |
| 4 | Least-Privilege PostgreSQL for a Small .NET Application | Which ownership and grants belong to bootstrap, migrator and runtime roles? | Pilot database design and disposable role-hardening harness | Publication-safe real-Pilot verification summary | Medium-high: practical ownership model for small systems |
| 5 | Why Backup Is Not Enough: Proving Restore | What evidence turns a backup file into a recovery capability? | Backup design, checksum/marker-last hardening, disposable restore rehearsal and operator-confirmed post-smoke restore | Independently reviewable sanitized restore artifact and measured recovery timeline | Medium-high: joins integrity, publication semantics and restore validation |
| 6 | Operational Readiness as an Engineering Deliverable | How do readiness, rollback, evidence classification and stop conditions shape a Pilot? | Runbook, checkpoints, evidence matrix and operator-confirmed external/browser/transactional gates | Independently reviewable sanitized operational artifacts and monitoring-cycle results | High: treats operations as designed, reviewable work |
| 7 | Fail-Closed Pilot Environment | How do explicit gates, disabled defaults and bounded administration constrain a Pilot? | Pilot configuration, CLI gates, runbook and GO checkpoints | Publication-safe operational excerpts | High: connects configuration defaults to operating discipline |
| 8 | Network Boundary Between Ingress and Database | How can a small deployment reduce unnecessary lateral access without overstating isolation? | Architecture notes and operator-confirmed network membership checkpoint | Sanitized network artifact or diagram approved for publication | Medium-high: a bounded and practical ingress/database separation story |

## Recommended sequence

Start with tenant isolation because it is strongly supported by code-level and disposable-test evidence and does not depend on private operational claims. Follow with the JSON-to-PostgreSQL evolution and controlled administration. Publish database-role, recovery and broader operational-readiness pieces only after their sanitized operational evidence is reviewed.

## Publication workflow

1. confirm claims and personal contribution attribution with the author;
2. select only evidence cleared for public use;
3. draft for DEV Community first;
4. perform technical, privacy and link review;
5. publish on DEV Community;
6. adapt for Medium using the DEV article as the canonical source where supported;
7. create a shorter LinkedIn adaptation linking to the canonical article;
8. record publication URLs and dates in the evidence register.

## Evidence discipline

Every draft must label claims as implemented, validated in disposable tests, operationally verified or planned. Repository history may demonstrate original engineering work and decision-making, but it must not be presented as legal advice, an endorsement guarantee or proof of impact beyond the available evidence.
