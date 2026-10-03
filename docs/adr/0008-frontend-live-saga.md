# 0008 — Angular: SignalStore fed by SignalR, the saga made visible

- **Status:** Accepted · 2026-10-02

## Context

Eventual consistency is usually hidden behind spinners. Here it is the product: the customer should watch a transfer go
through Ledger, Fraud and an analyst, live.

## Decision

- Zoneless Angular 22, OnPush everywhere (ESLint-enforced), `httpResource` for reads, Signal Forms for the transfer.
- **NgRx SignalStore** (`TransfersStore`) holds the user's transfers. A SignalR notification refetches only the transfer
  it concerns and patches it in place; pages read the store, never fetch transfers themselves.
- The transfer page renders the saga as a timeline with the service owning each step; the analyst's queue updates the
  moment a transfer is held.
- API errors are worded client-side from stable codes, in French and English (compile-time i18n, two builds).

## Consequences

- ✅ One source of truth for transfers; realtime and HTTP never disagree.
- ✅ The idempotency key is visible in the form: the robustness is part of the demo.
- ⚠️ Saga step details (fraud reasons) come from the server in English; rule names are localized client-side.
