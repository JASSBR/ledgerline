# 0003 — Event-sourced, double-entry ledger in integer cents

- **Status:** Accepted · 2026-10-02 · revised 2026-10-03 (locking, after the load test)

## Context

A ledger must answer "what was the balance on the 15th?", explain every cent, and never create or destroy money,
including under concurrent writes on the same account.

## Decision

- **Money is `long` cents** (`Money` value type). Decimals exist only at the API boundary; sub-cent amounts are refused.
- **Each account is an event stream**: `AccountOpened`, `FundsHeld`, `HoldReleased`, `EntryPosted`. The current state is
  an inline projection; any past state is a replay — the balance-as-of endpoint filters by value date.
- **Decider pattern**: `Account.PlaceHold/Post/ReleaseHold` and `JournalEntry.Decide` are pure functions from state to
  events; `Evolve` applies them. No I/O in the domain.
- **Double entry**: a journal entry has at least two lines on distinct accounts summing to zero, appended to all its
  streams in one commit. Deposits come from an internal Treasury account, so the **trial balance is always zero** — a
  property test generates thousands of random operation sequences and checks it.
- **Concurrency**: Ledger writes lock the streams they touch (`FetchForExclusiveWriting`, `SELECT … FOR UPDATE`),
  **always in account-id order**.

## Consequences

- ✅ Statements, running balances and balance-as-of come from the streams, with no history table to keep in sync.
- ✅ "Never overdraws, never creates money" is tested as a property and under load (20 concurrent transfers in an
  integration test; 1 500 on two accounts in the k6 run).
- ⚠️ Why locks and not optimistic concurrency: the first version used optimistic versions with fast retries. Under the
  load test, a hot account (every payment touching the same two streams) exhausted the retries and dead-lettered
  captures, leaving funds held. A bank's busy accounts (merchants, treasury) are the norm, so contention is queued by a
  lock; ordering the locks removed the deadlocks the same test revealed.
- ⚠️ The `Account` document carries the open holds: an account with thousands of simultaneous pending holds makes each
  write heavier. Acceptable for retail accounts; a merchant account would keep holds in their own documents.
