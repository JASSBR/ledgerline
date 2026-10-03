# 0006 — Event-carried state transfer, with a directory resync

- **Status:** Accepted · 2026-10-02 · revised 2026-10-03

## Context

Payments must resolve a beneficiary IBAN to an account; Fraud shows account names to analysts. Asking the Ledger
synchronously on every request would couple their availability to it.

## Decision

- The Ledger publishes `AccountRegistered`; Payments and Fraud keep **their own copy** (`DirectoryAccount`,
  `KnownAccount`) and never call the Ledger.
- **Resync at startup**: each subscriber sends `RequestAccountDirectory` when it starts; the Ledger republishes every
  account. The request is sent to the Ledger's queue, declared by the sender, so it waits there if the Ledger is down.

## Consequences

- ✅ Payments keeps accepting transfers while the Ledger restarts.
- ✅ Start order does not matter. The resync exists because Kubernetes showed the bug: RabbitMQ drops events published
  before a subscriber's queue exists, so a fresh deployment could leave Payments with an empty directory. The
  integration fixture now starts the Ledger *first*, on purpose.
- ⚠️ Copies are eventually consistent: a brand-new account is payable a few milliseconds after it exists.
