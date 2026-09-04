# Proof — PLAT-068 (command-log)

## Wave verification

EPIC-012 wave 1 was verified ONCE, together, at the wave's merge commit —
that shared run is the evidence for all five wave-1 tickets, since the wave
SHA contains every ticket's code.

- Wave SHA (`origin/dev` at verification time): `80f0ca262b0fe2ca354a5dfb18933dc3f105b917`
- Verification worktree: `C:\Users\PC\Documents\GitHub\pegasus\.worktrees\verify-w1`
  (disposable, detached at the wave SHA)
- PLAT-068's own merge SHA: `3f0cb45edf5eef0e9cd592b7e7305aaea8e96c44`
- Ancestry check: `git merge-base --is-ancestor 3f0cb45edf5eef0e9cd592b7e7305aaea8e96c44 origin/dev`
  → exit 0 (PLAT-068's merge commit is contained in the wave SHA)

## Command log

| Command | Exit | Notes |
| --- | --- | --- |
| `pwsh -NoProfile -File ./scripts/Test-MigrationGrants.ps1` | 0 | 91 migration files checked (PLAT-068's migration adds no `GRANT`, matching its plan) |
| `dotnet restore ./Pegasus.slnx --locked-mode` | 0 | |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | 0 | |
| `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"` | 0 | Core 1219, Architecture 100, Integration 1128 passed/2 skipped |
| `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"` | 0 | PLAT-068's own plan states the canonical verification filter is `Category!=Corpus` (not narrowed to exclude Browser). This full run includes Browser-tagged tests. Core 1219, Architecture 100, Integration 1248 passed/2 skipped |
| `pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Verify` | 0 | fresh capture + verify, covers the regenerated Accounts snapshot (Sign-off Engineer column/Settings control) |
| `pwsh -NoProfile -File ./scripts/Test-UiCatalogue.ps1` | 0 | 54 routed sources, 58 prototypes, 0 broken references |
| `pwsh -NoProfile -File ./scripts/Test-DocumentationLinks.ps1` | 0 | 125 files checked, all relative Markdown links resolve |
| `pwsh -NoProfile -File ./scripts/Test-MarkdownPlacement.ps1` (bare) | 1 | Not a real failure — this script takes mandatory `-Base`/`-Head` parameters; a bare invocation fails on parameter binding before any check runs. CI itself never invokes this script directly. |
| `pwsh -NoProfile -File ./scripts/Test-TestMarkdownPlacement.ps1` | 0 | The actual CI-wired gate (`.github/workflows/ci.yml:90-92`). Output: "Markdown placement regression tests passed." Substituted for the bare invocation above as the honest equivalent. |

## Verdict: PASS

Every command relevant to this ticket's acceptance exited 0, including the
`Category!=Corpus` full run (with Browser) and both Test UI snapshot
commands PLAT-068's own plan requires for the Accounts page change.
