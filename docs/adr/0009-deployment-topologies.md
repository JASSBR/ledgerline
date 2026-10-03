# 0009 — Kubernetes as the reference topology, one Container App for the demo

- **Status:** Accepted · 2026-10-03

## Context

The system should be shown as it would run in production, but the public demo runs on an Azure for Students credit.
Six always-on Container Apps (three services, gateway, broker, identity provider) would cost about €150 a month.

## Decision

- **Kubernetes** (`deploy/k8s`, Kustomize) is the reference: one Deployment per service with replicas, probes,
  restricted Pod Security, read-only root filesystems, default-deny NetworkPolicies. CI deploys it on **kind** for every
  push, smoke-tests it and runs the Playwright suite against it.
- **Azure** (`deploy/terraform`): the gateway, the three services and RabbitMQ are containers of **one Container App**
  (sidecars on localhost) that scales to zero, plus Keycloak in a second one. PostgreSQL Flexible Server (B1ms) holds
  one database per service. Images are pulled with a managed identity.

## Consequences

- ✅ The demo costs nothing when idle; the architecture is still demonstrated in full on Kubernetes.
- ⚠️ On Azure the bank must stay at one replica (each replica would carry its own broker) and the first request after
  idle waits for a cold start.
- ⚠️ Keycloak runs in dev mode (embedded H2, realm re-imported at start) in both topologies; production would back it
  with its own database.
