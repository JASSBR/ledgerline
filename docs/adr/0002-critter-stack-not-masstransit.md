# 0002 — Marten + Wolverine (MIT), not MassTransit 9 or EF Core

- **Status:** Accepted · 2026-10-02

## Context

The services need an event store (the ledger), documents (transfer views, screenings), sagas, a transactional
inbox/outbox, retries and RabbitMQ routing. In .NET the usual answer is MassTransit + EF Core. In 2026, MassTransit v9
is licensed commercially; v8 remains Apache-2.0 but is no longer where the project goes.

## Decision

- **Marten** (MIT) on PostgreSQL: event store with inline projections (`Account`, `JournalEntryView`) and a document
  database for read models.
- **Wolverine** (MIT): handlers, sagas, durable inbox/outbox **in the same PostgreSQL transaction as the business
  change**, RabbitMQ with conventional routing (one exchange per message type, one queue per service and type).
- No EF Core: there is no relational model to map; streams and documents are the model.

## Consequences

- ✅ One dependency family, one database per service, exactly-once *effects* with at-least-once delivery.
- ✅ Handlers are plain static methods returning messages (cascading); they unit-test without mocks.
- ⚠️ Smaller ecosystem than MassTransit; some APIs move fast (two namespace moves were absorbed during development).
- ⚠️ Wolverine compiles generated handler code at startup by default (Roslyn, ~410 MB per idle service). Images
  pre-generate it during the build (`codegen write`), bringing a service to ~90 MB — found by the load test
  ([performance](../performance.md)).
