# Proof — AUTO-018 (command-log)

## Wave verification

EPIC-012 wave 1 was verified ONCE, together, at the wave's merge commit —
that shared run is the evidence for all five wave-1 tickets, since the wave
SHA contains every ticket's code.

- Wave SHA (`origin/dev` at verification time): `80f0ca262b0fe2ca354a5dfb18933dc3f105b917`
- Verification worktree: `C:\Users\PC\Documents\GitHub\pegasus\.worktrees\verify-w1`
  (disposable, detached at the wave SHA)
- AUTO-018's own merge SHA: `80f0ca262b0fe2ca354a5dfb18933dc3f105b917` (AUTO-018 is the wave's last-merged
  ticket, so its merge SHA equals the wave SHA)
- Ancestry check: `git merge-base --is-ancestor 80f0ca262b0fe2ca354a5dfb18933dc3f105b917 origin/dev`
  → exit 0 (trivially — AUTO-018's own merge commit is `origin/dev`)

## Command log

| Command | Exit | Notes |
| --- | --- | --- |
| `dotnet restore ./Pegasus.slnx --locked-mode` | 0 | |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | 0 | |
| `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"` | 0 | AUTO-018's own canonical filter. Core 1219, Architecture 100, Integration 1128 passed/2 skipped |
| `pwsh -NoProfile -File ./scripts/Test-MigrationGrants.ps1` | 0 | 91 migration files checked; AUTO-018's migration needs no new `GRANT` (existing Web grants cover `AiJobs`/`CaseValuations`) |
| `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"` | 0 | Additional full run (includes Browser-tagged tests), run once for the wave because DOCS-017/ENG-035/PLAT-068 require it; not required by AUTO-018's own plan but exited clean. Core 1219, Architecture 100, Integration 1248 passed/2 skipped |
| `pwsh -NoProfile -File ./scripts/Test-DocumentationLinks.ps1` | 0 | 125 files checked, all relative Markdown links resolve |
| `pwsh -NoProfile -File ./scripts/Test-MarkdownPlacement.ps1` (bare) | 1 | Not a real failure — this script takes mandatory `-Base`/`-Head` parameters; a bare invocation fails on parameter binding before any check runs. CI itself never invokes this script directly. |
| `pwsh -NoProfile -File ./scripts/Test-TestMarkdownPlacement.ps1` | 0 | The actual CI-wired gate (`.github/workflows/ci.yml:90-92`). Output: "Markdown placement regression tests passed." Substituted for the bare invocation above as the honest equivalent. |

AUTO-018 changes no routed Razor page (Step 4b changes a page-model predicate
only), so its own plan does not require Test UI snapshot commands. They were
run once for the wave anyway (see PLAT-070's, DOCS-017's, and PLAT-068's
proofs) and passed with no snapshot diff attributable to AUTO-018.

## Verdict: PASS

Every command relevant to this ticket's acceptance exited 0.

**Evidence-tier note (per AUTO-018's own plan):** this ticket proves the Core
contract, the replay-safe persistence transaction, the MCP claim/complete
path through the real Automation ingress and production DI, the migration,
and the staff closure action (Operations "Complete job" for a MarketResearch
job). It is **not** activated end-to-end until [[CASE-029]] merges the
Case Valuation-section creation caller — this proof makes no claim that the
operator-facing capability is delivered.
