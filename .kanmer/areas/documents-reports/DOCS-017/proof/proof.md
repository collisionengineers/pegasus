# Proof — DOCS-017 (command-log)

## Wave verification

EPIC-012 wave 1 was verified ONCE, together, at the wave's merge commit —
that shared run is the evidence for all five wave-1 tickets, since the wave
SHA contains every ticket's code.

- Wave SHA (`origin/dev` at verification time): `80f0ca262b0fe2ca354a5dfb18933dc3f105b917`
- Verification worktree: `C:\Users\PC\Documents\GitHub\pegasus\.worktrees\verify-w1`
  (disposable, detached at the wave SHA)
- DOCS-017's own merge SHA: `86ce276dcc78398bcfd2d6526cf27265d49afa7b`
- Ancestry check: `git merge-base --is-ancestor 86ce276dcc78398bcfd2d6526cf27265d49afa7b origin/dev`
  → exit 0 (DOCS-017's merge commit is contained in the wave SHA)

## Command log

| Command | Exit | Notes |
| --- | --- | --- |
| `dotnet restore ./Pegasus.slnx --locked-mode` | 0 | |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | 0 | |
| `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"` | 0 | Core 1219, Architecture 100, Integration 1128 passed/2 skipped |
| `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"` | 0 | DOCS-017's own plan states the canonical gate is `Category!=Corpus` **without** excluding Browser — `AssessmentReportRendererTests` carries `[Trait("Category","Browser")]`, and a run that excludes it is INCONCLUSIVE for the PDF-rendering acceptance claims (Ed's tuple / Neil's name-only rendering). This full run includes those Browser tests. Core 1219, Architecture 100, Integration 1248 passed/2 skipped |
| `pwsh -NoProfile -File ./scripts/Test-MigrationGrants.ps1` | 0 | 91 migration files checked (DOCS-017 adds no migration) |
| `pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Verify` | 0 | fresh capture + verify; DOCS-017's own step 6 requires this because the added `Sign-off Engineer` readiness item changes the Assessment page's rendered readiness sentence |
| `pwsh -NoProfile -File ./scripts/Test-UiCatalogue.ps1` | 0 | 54 routed sources, 58 prototypes, 0 broken references |
| `pwsh -NoProfile -File ./scripts/Test-DocumentationLinks.ps1` | 0 | 125 files checked, all relative Markdown links resolve |
| `pwsh -NoProfile -File ./scripts/Test-MarkdownPlacement.ps1` (bare) | 1 | Not a real failure — this script takes mandatory `-Base`/`-Head` parameters; a bare invocation fails on parameter binding before any check runs. CI itself never invokes this script directly. |
| `pwsh -NoProfile -File ./scripts/Test-TestMarkdownPlacement.ps1` | 0 | The actual CI-wired gate (`.github/workflows/ci.yml:90-92`). Output: "Markdown placement regression tests passed." Substituted for the bare invocation above as the honest equivalent. |

## Verdict: PASS

Every command relevant to this ticket's acceptance exited 0, including the
`Category!=Corpus` full run (with Browser) that DOCS-017's own plan requires
for its PDF-rendering claims. Per this ticket's plan, DOCS-017 proves the
Core signatory contract, the fail-closed interim production caller, and Ed/
Neil renderer output; the Case-sourced signatory selection is proven by the
CASE-040 + PLAT-068 integration, not by this ticket.
