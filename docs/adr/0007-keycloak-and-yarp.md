# 0007 — Keycloak (OIDC + PKCE) behind a YARP gateway

- **Status:** Accepted · 2026-10-02

## Context

A banking front-end authenticates with a real identity provider, never with passwords handled by the API. Customers and
compliance analysts have different rights, enforced by every service, not only by the UI.

## Decision

- **Keycloak** realm `ledgerline` (exported in `deploy/keycloak`): public client `ledgerline-web`, authorization code
  flow with **PKCE (S256)**, an audience mapper (`ledgerline-api`) and a flat `roles` claim.
- The SPA uses `oidc-client-ts`; tokens live in `sessionStorage` and are renewed with the refresh token.
- Every service validates the JWT itself (issuer, audience, signature) and applies the `customer` / `operator`
  policies; ownership checks answer **404** for someone else's account (no existence oracle).
- **YARP** gateway: one origin for the browser (CORS, per-IP rate limit, security headers, WebSockets for SignalR),
  routes resolved by service discovery.
- Locally, Aspire runs Keycloak through `Aspire.Hosting.Keycloak`, a **preview** package: acceptable for a dev-time
  orchestrator, not shipped to production.

## Consequences

- ✅ The demo signs in exactly like a production portal; the E2E suite goes through the real login page.
- ⚠️ Demo users share a password shown on the welcome page: the realm is a demo, the protocol is real.
- ⚠️ Issuer discipline: the browser and the services must see Keycloak under the same URL (the CI sets a hosts entry).
