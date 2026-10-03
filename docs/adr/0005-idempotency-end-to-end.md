# 0005 — Idempotency from the button to the journal

- **Status:** Accepted · 2026-10-02

## Context

A double click, a mobile retry after a timeout, a broker redelivery: each could execute a payment twice. Delivery in
the system is at-least-once by design ([0002](0002-critter-stack-not-masstransit.md)).

## Decision

- **API**: `POST /api/payments/transfers` requires an `Idempotency-Key` header. The key is scoped to the user and stored
  with a SHA-256 fingerprint of the request. Same key + same request → the original transfer, `200` with
  `Idempotent-Replayed: true` (Stripe's convention). Same key + different request → `409`.
- **UI**: the key is drawn **once per form**, not per click; a retry after a network error resends the same key.
- **Messages**: the transfer id is also the hold id and the journal entry id. A redelivered `ReserveFunds` finds its hold,
  a redelivered `CaptureTransfer` finds its entry: both are no-ops. Wolverine's durable inbox drops exact duplicates.

## Consequences

- ✅ Verified at three levels: unit (account idempotency), integration (replay and conflicting reuse), and load (10 % of
  k6 requests are replays: 0 mismatches).
- ⚠️ Idempotency records are kept forever here; production would expire them (24 h, like Stripe).
