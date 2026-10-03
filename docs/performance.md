# Performance and what the load test found

Measured on 2026-10-03 with [k6](https://k6.io) ([`tests/load/transfers.js`](../tests/load/transfers.js)) running
**inside** the kind cluster deployed by [`deploy/k8s/kind.sh`](../deploy/k8s/kind.sh): production images (Release,
chiseled, pre-generated handlers), gateway ×2, payments ×2, fraud ×2, ledger ×1, one PostgreSQL, one RabbitMQ,
Keycloak-issued tokens. Docker Desktop on an Apple M4 laptop (10 CPUs given to the VM).

**Workload — the worst case on purpose.** 25 transfers per second for 60 s, all between the same two accounts (Alice
and Bob paying each other 1 €), so every capture contends for the same two event streams. 10 % of requests are
immediately retried with the same `Idempotency-Key`. 30 virtual users read balances and transfer lists in parallel.
After the run the script waits for every saga to settle, then checks the books.

```bash
./deploy/k8s/kind.sh && ./tests/load/run-in-kind.sh
```

## Result

| | p50 | p95 |
|---|---|---|
| Transfer accepted (`202`) | 11 ms | **143 ms** |
| Reads (accounts, transfers) | 6.5 ms | **80 ms** |

16 302 requests, **0 errors**, 0 idempotency mismatches, **1 500/1 500 transfers settled**, 0 dead letters,
trial balance **exactly 0** over 1 506 journal entries.

## How it got there — four defects no unit or integration test had caught

The first runs failed. Each failure was a real production defect, fixed in the code, not in the test:

| Symptom under load | Cause | Fix |
|---|---|---|
| 1 008 `sorry, too many clients already` from PostgreSQL | Each pod's Npgsql pool may open 100 connections; the server accepts 100 in total | Pool capped per service (`Maximum Pool Size`), server limit sized to the pod count — and 10 per service on Azure, whose B1ms tier accepts ~50 |
| `deadlock detected` in the Ledger | Alice→Bob and Bob→Alice lock the same two streams in opposite orders | Streams always locked in account-id order ([`Journal.cs`](../src/Services/Ledger/Ledgerline.Ledger/Handlers/Journal.cs)); deadlocks retried as a backstop |
| 200 transfers stuck in *Reserving*/*Capturing*, messages in RabbitMQ's dead-letter queue | Optimistic concurrency on a hot account: retries ran out (10 fast retries) | Ledger writes take a row lock (`FetchForExclusiveWriting`): contention becomes a queue, not a retry storm. Slow retries (2 s → 1 min) before any dead-lettering ([ADR 0003](adr/0003-event-sourced-ledger.md)) |
| Payments `OOMKilled` at 512 Mi | Wolverine compiled its handlers with Roslyn at startup: ~410 MB per idle service | Handlers pre-generated during the image build (`codegen write`): **~90 MB per service** |

## Reading these numbers

- One laptop, one PostgreSQL: they show the cost of the design under contention, not cloud capacity.
- Two accounts receiving everything is harsher than real traffic, where postings spread across accounts; it is the
  shape of a merchant or treasury account, which is why it is the scenario that matters.
- The run lifts the gateway's per-IP rate limit and the fraud velocity rule (all virtual users share one IP and pay
  each other constantly); `run-in-kind.sh` restores both afterwards.
