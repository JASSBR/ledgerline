# 0001 — Three services around a system of record, one database each

- **Status:** Accepted · 2026-10-02

## Context

A bank's ledger is the system of record: it must be correct before it is fast, it changes rarely, and every other
capability (payments, fraud, statements, cards) depends on it. Payments change often (new schemes, new rules). Fraud is
owned by a compliance team with its own release pace and its own data (rules, blocklists, analyst decisions).
My other portfolio project, ClaimFlow, is deliberately a modular monolith; this one exists to show the distributed
version and its real costs.

## Decision

- **Ledger** (accounts, holds, double-entry journal), **Payments** (transfer saga, idempotency, live status) and
  **Fraud** (rules, review queue) are separate deployables, each with **its own PostgreSQL database**.
- They communicate **only by messages** over RabbitMQ, using contracts in `Ledgerline.Contracts`: records of primitives,
  no domain types (enforced by `ArchitectureTests`). No service calls another one synchronously.
- A YARP gateway is the single entry point for the browser ([ADR 0007](0007-keycloak-and-yarp.md)).

## Consequences

- ✅ The Ledger can be audited and deployed on its own; a Fraud outage delays payments instead of corrupting money.
- ✅ Each service owns its schema: no cross-database join can couple them.
- ⚠️ Everything that was a transaction becomes a protocol: holds and compensation ([0004](0004-transfer-saga-with-holds.md)),
  idempotency ([0005](0005-idempotency-end-to-end.md)), local copies of other services' data ([0006](0006-event-carried-state-transfer.md)).
- ⚠️ Operations cost more: three databases, a broker, tracing across hops (Aspire dashboard locally).
- The honest alternative for a team of this size is a modular monolith. The split is justified here by the ownership
  boundaries above, and it is the reason the project exists.
