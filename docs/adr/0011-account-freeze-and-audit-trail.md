# 0011 — Account freeze as a ledger event, and the stream as the audit trail

- **Status:** Accepted · 2026-10-03

## Context

Back offices freeze accounts: suspected takeover, dispute, court order, a customer who lost their card. A freeze must
stop money leaving, must not stop money arriving (salary, refunds), must say why, and must be provable afterwards:
who froze it, when, and what happened meanwhile.

The tempting implementation is a `Status` column on an account table, checked by the Payments service before it
starts a transfer. Two problems:

- **The check is in the wrong place.** Payments only holds a copy of the accounts ([ADR 0006](0006-event-carried-state-transfer.md));
  a freeze landing between its check and the Ledger's hold would let a payment through.
- **A column has no history.** "Was this account frozen when that payment left?" would need a separate audit table,
  kept in sync by hand.

## Decision

- **`AccountFrozen(Reason, By, At)` and `AccountUnfrozen(By, At)` are events of the account's stream.** The `Account`
  decider refuses a freeze without a reason, a second freeze, and an unfreeze of an account that isn't frozen.
- **The Ledger enforces it, at the only place money leaves:** `PlaceHold` and uncovered debits return
  `ledger.account_frozen`. The saga already turns a refused hold into a failed transfer with its reason: no change in
  Payments, no new message, no race. Credits are never refused.
- **A hold placed before the freeze can still be captured.** That money was committed to a payment in flight; refusing
  the capture would strand the saga between "reserved" and "captured". The freeze stops *new* commitments.
- **Freeze and unfreeze take the same write path as money:** the decider runs on the stream locked with
  `FetchForExclusiveWriting`, so a freeze cannot interleave with a hold.
- **The audit trail is the stream.** `GET /accounts/{id}/history` returns the account's events as stored. The
  customer sees the reason; only operators see who froze the account.

## Consequences

- ✅ "Was it frozen at the time?" is answered by the same replay that gives the balance at any date.
- ✅ The freeze holds under concurrency for free: it lives behind the stream lock that already protects money.
- ⚠️ A customer only learns of a freeze from the account page or from a refused transfer. A notification
  (`AccountFrozen` published to Payments for the realtime hub) can be added when needed: the event already exists.
- ⚠️ Fraud doesn't know about freezes. A rule such as "never pay a frozen beneficiary" would need the event to be
  published. Not built yet.
