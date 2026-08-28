# MAIL-019 post-implementation report

- Branch `task/mail-019-intake-liveness-smoke`, commit `a7b44e327b7e7780874b9c0250c1fad5145f424c`, PR #573 → `dev`.
- Files: `scripts/Invoke-ProductionSmoke.ps1` (+49), `docs/runbook.md` (+8/-2),
  `.agents/skills/pegasus-release/SKILL.md` (+5/-2). No .NET file changed.

## What changed

The full smoke path reads the production database read-only (bootstrap
access-token pattern) and fails unless: no `Approved` + `AllowInboundIntake`
mailbox has `ActivatedAtUtc IS NULL`; an unexpired `Active` subscription
exists; the newest `LastCompletedAtUtc` is within 15 minutes on the database
clock. `-WorkerOnly` path unchanged.

## Commands run

- Script parse (`[scriptblock]::Create`) — OK.
- Live read-only execution of the new block against prod — exit 0:
  `Inbox intake liveness smoke passed (last poll 2026-08-27 14:30:00Z, subscription expires 2026-09-02 10:25:00Z).`
- `dotnet test tests/Pegasus.ArchitectureTests -c Release --filter FullyQualifiedName~WorkerActivationReleaseContractTests`
  — 14/14 passed, exit 0 (first run built and hit a transient test-host
  crash after 7 passes; `--no-build` rerun clean).

## Commands not run and why

- Canonical `dotnet restore --locked-mode` / `dotnet build` / full
  `dotnet test --filter "Category!=Corpus"`: not run — no .NET source changed
  (script, docs, skill text only); the controller declined a local
  full-suite run and the PR's CI shards are the test evidence.
- No integration filter run: no integration test covers the smoke script.

## Deviations

- "Identities bound" in the brief has no schema counterpart; the hard-FAIL is
  `Approved + AllowInboundIntake + ActivatedAtUtc IS NULL`.
