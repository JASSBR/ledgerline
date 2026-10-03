## What and why

<!-- One paragraph: the problem, the change, the decision. Link an ADR if the change involves one. -->

## How it was verified

- [ ] `dotnet build` (0 warnings) and `dotnet format --verify-no-changes`
- [ ] `dotnet test --solution Ledgerline.slnx` (unit + architecture + integration)
- [ ] `npm run lint && npm test && npm run build` in `web/`
- [ ] Playwright, if a user flow changed
- [ ] Coverage gate holds (≥ 80 %)
