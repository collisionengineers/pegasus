# Plan — earn back a full working day of telemetry

## Chosen approach

Suppress successful EF Core database-command logs from Web stdout by setting `Microsoft.EntityFrameworkCore.Database.Command` to `Warning` in the shipped Web configuration. Live seven-day evidence shows those readiness-driven Information logs dominate ingestion (about 470 MB of 755 MB), so this is the smallest measured earn-back change.

Preserve EF warnings/errors, readiness checks, Container Apps console diagnostics, Application Insights wiring, Worker sampling, and the 0.1 GB cap. Do not raise quota or make a cloud write unless DELIV-021 measurement proves the filter insufficient.

## Governing docs

This chore changes shipped logging configuration only. `docs/current-architecture.md`, `docs/operations.md`, and `docs/runbook.md` remain current-state/deployed evidence owners and are updated by DELIV-021 after deployment, not speculatively here.

## Ordered steps

1. Create/take a PLAT-036 worktree from current `origin/dev`.
2. Add the single Web logging category override at Warning.
3. Add a focused architecture test that parses shipped JSON and asserts the exact category/level.
4. Run the focused test, Release build/relevant suite, JSON validation, and `git diff --check`.
5. Run simplification lenses; the intended production diff remains one configuration line plus one proportional contract test.
6. Report, commit, push, open PR to `dev`, and move to Review.

## Proof

Source verification proves valid JSON and exact category level; Release build proves configuration packaging. The implementation claim is only that successful EF commands will not be emitted at Information. DELIV-021 must deploy, observe normalized daily ingestion, confirm no cap window, and prove warning/error signal remains.

## Risks and mitigations

- **Wrong category:** exact contract test uses the full EF category string.
- **Hiding failures:** Warning retains warnings and errors.
- **Losing independent diagnostics:** console diagnostic setting is untouched.
- **Premature paid change:** no quota/IaC/cloud mutation.
