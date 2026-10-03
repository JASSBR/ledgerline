# Ledgerline — instructions for Claude Code

Portfolio project: a transfer bank as three .NET 10 microservices (Ledger, Payments, Fraud) on Marten + Wolverine +
RabbitMQ, a YARP gateway, Keycloak, and an Angular 22 SPA. Orchestrated locally by .NET Aspire.
Goal: production-grade engineering a tech lead can review. Correctness of money > feature count.

## Commands

```bash
export PATH="$HOME/.dotnet:$PATH"              # SDK is user-level on this machine
dotnet run --project src/Ledgerline.AppHost     # whole system (needs Docker)
dotnet build Ledgerline.slnx                    # must stay at 0 warnings
dotnet test --solution Ledgerline.slnx          # integration tests need Docker (PostgreSQL + RabbitMQ)
dotnet format Ledgerline.slnx --verify-no-changes
python3 eng/coverage-gate.py 80                 # after `dotnet test --coverage --coverage-output-format cobertura`
cd web && nvm use && npm run format:check && npm run lint && npm test && npm run build
cd web && npm run e2e                           # Playwright against the running AppHost (or the kind cluster in CI)
cd web && npm run i18n                          # after any UI text change (fails on a missing translation)
./deploy/k8s/kind.sh                            # build images, deploy on kind, smoke test
./tests/load/run-in-kind.sh                     # k6 inside the cluster + post-run invariants
./deploy/azure.sh && API_URL=… AUTHORITY=… ./deploy/vercel.sh
```

## ECC setup in this repo

- Rules: `.claude/rules/{common,csharp,angular}` (from affaan-m/ECC), Angular globs retargeted to `web/src/**`.
- Agent: `csharp-reviewer` on every backend diff. Skills: `dotnet-patterns`, `csharp-testing`, `angular-developer`,
  plus `tdd-workflow`, `verification-loop`, `security-review`.
- Hooks (`.claude/settings.json`): format each edited file; on Stop, build + lint the side that changed.

## Workflow per feature

1. ADR in `docs/adr/` when the change involves a decision someone could challenge.
2. TDD: domain test (or property) → integration test (real PostgreSQL + RabbitMQ) → implementation.
3. `csharp-reviewer` on the diff; `/security-review` when auth, input handling or money movement is touched.
4. `/verification-loop`; for anything on the write path of money, also the k6 run on kind.
5. Conventional commit, subject ≤ 70 chars, **no AI attribution**.

## Hard rules

- Money is `long` cents in the domain (`Money`); decimals only at the API boundary.
- Every Ledger write goes through a decider (`Account`, `JournalEntry`) and locks streams with
  `FetchForExclusiveWriting`, **in account-id order** (ADR 0003). A journal entry sums to zero.
- Every handler is idempotent: transfer id = hold id = entry id; replies not matching the saga state are ignored.
- Services talk only through `Ledgerline.Contracts` (records of primitives) — never a project reference to another
  service, never a synchronous call between services (ArchitectureTests).
- A service keeping another's data subscribes to its events *and* requests a resync at startup (ADR 0006).
- `*.Domain` projects reference `SharedKernel` only. Business failures are `Result` values mapped by `ToProblem()`.
- Handler classes end in `Handler`; new message types live in the Contracts namespace to be routed.
- Production images ship pre-generated Wolverine code (`codegen write` in the Dockerfile); tests rely on
  `JasperFxEnvironment.AutoStartHost`.
- Angular: OnPush, `inject()`, `httpResource` for reads, Signal Forms, transfers only through `TransfersStore`,
  every UI string through `i18n`/`$localize` with a stable id.
- Analyzer warning? Fix it, or disable it in `.editorconfig` with a one-line justification — never `#pragma`.
- No secrets in the repo: Aspire parameters locally, generated Secrets on kind, Terraform-generated on Azure.
