# 0004 — Transfers as a saga: hold, screen, capture or release

- **Status:** Accepted · 2026-10-02

## Context

A transfer spans three services. There is no distributed transaction, yet the customer must never be debited for a
payment that is refused, and two transfers must never spend the same euro.

## Decision

The `Transfer` saga (Payments, persisted by Wolverine in Marten) drives:

1. **Reserving** → `ReserveFunds` to the Ledger, which places a **hold**: the available balance drops, the books don't.
2. **Screening** → `ScreenTransfer` to Fraud: *cleared*, *blocked*, or *held for review* by an analyst.
3. **Capturing** → `CaptureTransfer`: the Ledger posts the journal entry and consumes the hold, atomically.
4. **Compensation**: a block, an analyst rejection, a capture failure or the **48 h review deadline** (a scheduled
   message) sends `ReleaseFunds`; the hold disappears, the transfer ends *Rejected* or *Failed*.

Every step is recorded in a `TransferView` timeline and pushed to the browser ([0008](0008-frontend-live-saga.md)).
Handlers ignore replies that do not match the current state (late or duplicated messages).

## Consequences

- ✅ Money is reserved before anyone else decides: two transfers cannot both spend the last euro (integration test:
  20 concurrent transfers → exactly the affordable number complete).
- ✅ Every failure path ends with the hold released — tested for blocklist, rejection, insufficient funds and deadline.
- ⚠️ Eventual consistency is visible: the customer sees "Screening" for a moment. The UI shows it as a timeline instead
  of hiding it.
