# Architecture Decision Records

Each record captures one decision, the context that forced it, and what it costs. Format: [MADR-lite](https://adr.github.io/madr/).

| # | Decision | Status |
|---|----------|--------|
| [0001](0001-microservices-around-the-ledger.md) | Three services around a system of record, one database each | Accepted |
| [0002](0002-critter-stack-not-masstransit.md) | Marten + Wolverine (MIT), not MassTransit 9 or EF Core | Accepted |
| [0003](0003-event-sourced-ledger.md) | Event-sourced, double-entry ledger in integer cents | Accepted |
| [0004](0004-transfer-saga-with-holds.md) | Transfers as a saga: hold, screen, capture or release | Accepted |
| [0005](0005-idempotency-end-to-end.md) | Idempotency from the button to the journal | Accepted |
| [0006](0006-event-carried-state-transfer.md) | Event-carried state transfer, with a directory resync | Accepted |
| [0007](0007-keycloak-and-yarp.md) | Keycloak (OIDC + PKCE) behind a YARP gateway | Accepted |
| [0008](0008-frontend-live-saga.md) | Angular: SignalStore fed by SignalR, the saga made visible | Accepted |
| [0009](0009-deployment-topologies.md) | Kubernetes as the reference topology, one Container App for the demo | Accepted |
| [0010](0010-testing-strategy.md) | Testing: properties, a real broker, and load as a correctness test | Accepted |
