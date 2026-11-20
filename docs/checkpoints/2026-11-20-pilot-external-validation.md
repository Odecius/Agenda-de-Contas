# Pilot external validation checkpoint - 2026-11-20

## Decision

`PILOT EXTERNAL VALIDATION: PASSED`

Current internal tracking status:

- Multi-Family: 100%;
- Pilot Environment Readiness: 100% - GO;
- Agenda-de-Contas current Pilot scope: 100%.

The percentages refer only to the currently defined Pilot-readiness scope. They do not claim product completeness, a production SLA, security certification or unrestricted rollout readiness.

This checkpoint records sanitized results supplied by the operator after the Pilot GO documentation. Every operational result below is **operator-confirmed**. No raw logs, screenshots, credentials, participant data, private addresses, internal identifiers or database contents were added to the repository.

## External-access and network-boundary results

The operator confirmed that:

- the dedicated ingress Docker network `agenda_pilot_edge` was created;
- the Pilot application is attached to `agenda_pilot_database` and `agenda_pilot_edge`;
- PostgreSQL remains attached only to `agenda_pilot_database`;
- `cloudflared` is attached only to `agenda_pilot_edge`;
- neither the application nor PostgreSQL publishes a host port;
- a dedicated Cloudflare Tunnel connected successfully;
- public HTTPS routing through the tunnel reached the Pilot application;
- the external `/health/ready` check returned `ready`;
- browser-based Owner authentication succeeded through the public HTTPS route.

The resulting boundary is:

```text
Internet
  -> Cloudflare edge/TLS
  -> Cloudflare Tunnel
  -> dedicated ingress Docker network
  -> Pilot application
  -> separate database Docker network
  -> PostgreSQL
```

The tunnel is not a member of the database network and is not described as having direct database access. Separating ingress from the database network reduces unnecessary lateral reach from the ingress component. This is a bounded network-membership claim, not a claim of complete network isolation or a security certification.

## Synthetic functional smoke result

Through the public HTTPS route and an authenticated Owner session, the operator confirmed a controlled synthetic smoke test covering:

1. create an account;
2. read and display the account;
3. edit the account;
4. mark a payment;
5. verify the payment state;
6. unmark the payment;
7. remove the synthetic test data.

No participant data or real Pilot database contents were inspected for this documentation record.

## Post-smoke recovery result

After browser and transactional smoke validation, the operator confirmed that:

- a new PostgreSQL custom-format backup was created;
- the backup was restored into a disposable PostgreSQL 16 target;
- the restore process exited with code `0`;
- the restored `public` schema was present;
- all three EF migrations were present after restore;
- the disposable restore container was removed after validation.

This proves the reported post-smoke artifact was restorable in the controlled validation. It does not establish off-host redundancy for that specific backup, because no publication-safe repository evidence supports that stronger claim.

## Evidence classification

- **Repository-evidenced:** the versioned architecture, runbooks, readiness endpoint, tenant isolation design, database-network design and disposable validation harnesses.
- **Operator-confirmed:** the network memberships, tunnel connection, public HTTPS route, external readiness result, browser login, transactional smoke test, post-smoke backup, disposable restore and cleanup recorded here.
- **Publication-evidenced:** this sanitized checkpoint records what the operator confirmed and the classification applied to it.
- **Not yet publication-evidenced:** independently reviewable sanitized artifacts for the external route, network inspection, browser flow, transactional smoke and post-smoke restore.

Publication must not restate operator-confirmed results as independently reproduced or independently publication-evidenced until separate sanitized artifacts are reviewed and committed.

## Remaining controlled Pilot work

- onboard the first one or two approved Pilot participants;
- monitor operation and collect structured feedback;
- expand to two to five participants only after review;
- decide separately whether to activate Telegram;
- prepare sanitized public evidence if stronger public operational claims are desired;
- pursue later production-readiness work only if that becomes a project objective.

## Scope and safety statement

Creating this checkpoint did not deploy software, query real Pilot data, inspect credentials, restart containers, modify Cloudflare, change Pilot users or alter runtime behavior. It is documentation-only and relies on sanitized operator confirmation.
