# 0010 — Testing: properties, a real broker, and load as a correctness test

- **Status:** Accepted · 2026-10-03

## Decision

| Layer | What | How |
|---|---|---|
| Domain | Money, IBAN, accounts, journal entries, fraud rules | xUnit v3; **CsCheck property**: random operation sequences never create money or overdraw (mutation-checked) |
| Saga | Every transition and stale-reply guard | Unit tests on the saga class, no infrastructure |
| Architecture | Pure domains, no service referencing another, primitive contracts | NetArchTest |
| Integration | The three services together | Testcontainers: **real PostgreSQL and RabbitMQ**, one vhost per fixture; the Ledger starts first on purpose |
| Frontend | Store, auth, forms, pages | Vitest, 96 % statements |
| End to end | Review, overdraft, blocklist, books | Playwright through the real Keycloak login, against the **Kubernetes** deployment in CI |
| Load | Hot-account contention, idempotency replays | k6 inside the cluster, with **post-run invariants**: no saga stuck, books at zero |

## Consequences

- ✅ The load test is a correctness test: it found four production defects that every other layer missed
  ([performance](../performance.md)).
- ⚠️ The integration suite needs Docker; the full CI run takes several minutes.
