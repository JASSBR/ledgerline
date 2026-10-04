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
  one database per service. Images are built by GitHub Actions and pulled from **ghcr.io** (public, free): the
  deploy script builds nothing and only points Azure at the commit's tag. (An earlier version pulled from Azure
  Container Registry with a managed identity: a fixed ~€4.5 a month, and local `amd64` builds under emulation.)

- Two Azure constraints shape the Terraform: since 2026 a new Container Apps environment defaults to *Express* mode,
  which refuses sidecars, so the environment is declared through the ARM API (`azapi`) in *WorkloadProfiles* mode; and
  Azure for Students allows one environment per region, so the demo shares ClaimFlow's (`existing_environment`), as it
  shares its PostgreSQL server (one database per service).

## Consequences

- ✅ The demo costs nothing when idle, and no registry bills by the day; the architecture is still demonstrated in
  full on Kubernetes.
- ✅ The welcome page pings Keycloak and the gateway on arrival, so they wake while the visitor reads it.
- ⚠️ On Azure the bank must stay at one replica (each replica would carry its own broker) and the first request after
  idle waits for a cold start.
- ⚠️ Keycloak runs in dev mode (embedded H2, realm re-imported at start) in both topologies; production would back it
  with its own database.
