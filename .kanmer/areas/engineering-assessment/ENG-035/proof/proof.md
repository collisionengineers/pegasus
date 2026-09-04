# Proof — ENG-035 (command-log)

## Wave verification

EPIC-012 wave 1 was verified ONCE, together, at the wave's merge commit —
that shared run is the evidence for all five wave-1 tickets, since the wave
SHA contains every ticket's code.

- Wave SHA (`origin/dev` at verification time): `80f0ca262b0fe2ca354a5dfb18933dc3f105b917`
- Verification worktree: `C:\Users\PC\Documents\GitHub\pegasus\.worktrees\verify-w1`
  (disposable, detached at the wave SHA)
- ENG-035's own merge SHA: `ce027748aa5d00daea13a0359a5eb4a81aad912d`
- Ancestry check: `git merge-base --is-ancestor ce027748aa5d00daea13a0359a5eb4a81aad912d origin/dev`
  → exit 0 (ENG-035's merge commit is contained in the wave SHA)

## Command log

| Command | Exit | Notes |
| --- | --- | --- |
| `pwsh -NoProfile -File ./scripts/Test-MigrationGrants.ps1` | 0 | 91 migration files checked, every created table granted or exempted |
| `dotnet restore ./Pegasus.slnx --locked-mode` | 0 | |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | 0 | |
| `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"` | 0 | Core 1219, Architecture 100, Integration 1128 passed/2 skipped |
| `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"` | 0 | ENG-035's own plan states the canonical gate is `Category!=Corpus`, "not narrowed to exclude `Category=Browser`, because the rendered-PDF proof for Steps 5 and 6 lives in that category." This full run includes those Browser tests (`AssessmentReportRendererTests`). Core 1219, Architecture 100, Integration 1248 passed/2 skipped |
| `pwsh -NoProfile -File ./scripts/Test-DocumentationLinks.ps1` | 0 | 125 files checked, all relative Markdown links resolve (the previously-tracked broken `.opencode/skills/kanmer-setup/SKILL.md` link, deferred by ENG-035 to KANMER-011, no longer appears — resolved elsewhere on `dev` before this wave SHA) |
| `pwsh -NoProfile -File ./scripts/Test-MarkdownPlacement.ps1` (bare) | 1 | Not a real failure — this script takes mandatory `-Base`/`-Head` parameters; a bare invocation fails on parameter binding before any check runs. CI itself never invokes this script directly. |
| `pwsh -NoProfile -File ./scripts/Test-TestMarkdownPlacement.ps1` | 0 | The actual CI-wired gate (`.github/workflows/ci.yml:90-92`). Output: "Markdown placement regression tests passed." Substituted for the bare invocation above as the honest equivalent. |

ENG-035 changes no routed Razor page, so the Test UI snapshot/catalogue
commands are not part of its own acceptance list, but were run once for the
wave anyway (see PLAT-070's and DOCS-017's proofs) and passed.

## Verdict: PASS

Every command relevant to this ticket's acceptance exited 0, including the
`Category!=Corpus` full run (with Browser) that ENG-035's own plan requires
for its rendered-PDF proof of the expanded vehicle/damage/settlement report
sections.
