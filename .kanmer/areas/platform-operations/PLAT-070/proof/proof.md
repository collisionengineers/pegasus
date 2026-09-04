# Proof — PLAT-070 (command-log)

## Wave verification

EPIC-012 wave 1 was verified ONCE, together, at the wave's merge commit —
that shared run is the evidence for all five wave-1 tickets, since the wave
SHA contains every ticket's code.

- Wave SHA (`origin/dev` at verification time): `80f0ca262b0fe2ca354a5dfb18933dc3f105b917`
- Verification worktree: `C:\Users\PC\Documents\GitHub\pegasus\.worktrees\verify-w1`
  (disposable, detached at the wave SHA)
- PLAT-070's own merge SHA: `60fc84dc0ef7e1c4746dd9b3961d287598845871`
- Ancestry check: `git merge-base --is-ancestor 60fc84dc0ef7e1c4746dd9b3961d287598845871 origin/dev`
  → exit 0 (PLAT-070's merge commit is contained in the wave SHA)

## Command log

| Command | Exit | Notes |
| --- | --- | --- |
| `dotnet restore ./Pegasus.slnx --locked-mode` | 0 | |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | 0 | |
| `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"` | 0 | Core 1219, Architecture 100, Integration 1128 passed/2 skipped |
| `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"` | 0 | Additional full run (includes Browser-tagged tests) run per DOCS-017/ENG-035/PLAT-068's own plans, which state the narrower filter is INCONCLUSIVE for their PDF/rendering claims. Core 1219, Architecture 100, Integration 1248 passed/2 skipped |
| `pwsh -NoProfile -File ./scripts/Test-MigrationGrants.ps1` | 0 | 91 migration files checked, every created table granted or exempted |
| `pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Verify` | 0 | fresh capture + verify, includes UI catalogue web tests (297 passed) |
| `pwsh -NoProfile -File ./scripts/Test-UiCatalogue.ps1` | 0 | 54 routed sources, 58 prototypes, 0 broken references |
| `pwsh -NoProfile -File ./scripts/Test-DocumentationLinks.ps1` | 0 | 125 files checked, all relative Markdown links resolve |
| `pwsh -NoProfile -File ./scripts/Test-MarkdownPlacement.ps1` (bare) | 1 | Not a real failure — this script takes mandatory `-Base`/`-Head` parameters; a bare invocation fails on parameter binding before any check runs. CI itself never invokes this script directly (confirmed: `.github/workflows/ci.yml` has no `-Base`/`-Head` call site). |
| `pwsh -NoProfile -File ./scripts/Test-TestMarkdownPlacement.ps1` | 0 | The actual CI-wired gate (`.github/workflows/ci.yml:90-92`, "Markdown placement regression tests") — this is the script's own regression-test wrapper and is what CI runs. Output: "Markdown placement regression tests passed." Substituted for the bare invocation above as the honest equivalent of the named command. |
| `git grep -n -i "ReviewedByStaff\|RequireStaffImageReview\|RequireStaffInstructionReview\|staff-reviewed"` (PLAT-070's own acceptance check) | 0 (matches found) | Manually inspected: every match outside `src/Pegasus.Infrastructure/Persistence/Migrations/**` (historical migrations, never edited) is confined to `tests/Pegasus.IntegrationTests/WorkflowConfigurationWebTests.cs:31-32`, which are `Assert.DoesNotContain(...)` lines proving the retired strings are absent from rendered HTML — i.e., proof of the retirement, not a leftover. Acceptance condition satisfied. |

## Verdict: PASS

Every command relevant to this ticket's acceptance exited 0. The one nominal
non-zero exit (`Test-MarkdownPlacement.ps1` bare) was a parameter-binding
error from an incomplete invocation, not a placement violation; the actually
CI-wired equivalent (`Test-TestMarkdownPlacement.ps1`) passed. PLAT-070's own
named acceptance check (`git grep`) confirms the retired staff-review surface
is gone from all non-historical files.
