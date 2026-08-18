# Post-implementation report — DELIV-002

## Summary

Replaced the merge-commit release process with an exact-SHA, non-force
`dev` → `main` promotion. The main-push guard now proves append-only history
and that the pushed `main` head is contained in `dev`; it does not claim to
determine human release authority. The temporary DELIV-003 convergence path is
explicitly branch-local, reviewed, and single-use.

## Changes

| File | Change | Why |
|---|---|---|
| `docs/engineering.md` | Defined the exact-SHA promotion procedure, exclusions, detective-CI limit, and one-time convergence transition. | Establishes the canonical delivery process. |
| `AGENTS.md` | Aligned release authority and allowed Git operations, including the narrow DELIV-003 exception. | Lets the transition occur without direct shared-branch edits or rewrites. |
| `scripts/Test-MainBranchHistory.ps1` | Replaced the two-parent-merge predicate with release-branch ancestry validation. | Enforces the structural invariant of the new strategy. |
| `.github/workflows/ci.yml` | Fetches `origin/dev` and supplies it to the existing main-push guard. | Makes the guard evaluate the source history on the pushed revision. |
| `tests/Pegasus.ArchitectureTests/MainBranchHistoryGuardTests.cs` | Added fast-forward, later-`dev`, direct-main, synthetic-merge, and invalid-release-branch coverage. | Proves the guard accepts the intended shape and rejects heads outside `dev`. |

## Governing docs

No PRD, FRD, or ADR applies. `docs/index.md` routes repository delivery
guidance to `docs/engineering.md` and task authority to `AGENTS.md`; both
were updated in their owning scopes. No GitHub protection or ruleset was
configured, by the recorded subscription-boundary decision.

## Risks / follow-ups

GitHub-side prevention remains intentionally unavailable, so CI is detective.
A structurally valid direct fast-forward cannot establish who authorised it;
the release actor must follow the explicit `MERGE AUTH GRANTED`, exact-SHA
preflight, and read-back procedure. [[DELIV-003]] owns the one-time
convergence, first remote promotion, and resulting merged-`main` proof.

## Verification hand-off

On the merged branch, review that the policy and CI guard match, then have
[[DELIV-003]]: fetch remote refs; confirm `origin/main` is an ancestor of
`origin/dev` after its reviewed convergence PR; obtain explicit release
authority for the then-current refs; non-force push the reviewed `dev` SHA to
`main`; fetch again and confirm both heads equal that SHA; and confirm the
revised main-push CI run passes. Do not perform that release while DELIV-002 is
only under review.

Local validation passed:

- `dotnet restore`
- `dotnet build Pegasus.slnx --configuration Release --no-restore`
- `dotnet test tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build` — 96 passed
- `pwsh ./scripts/Test-DocumentationLinks.ps1` — 220 files checked
- `pwsh ./scripts/Test-CiChangeFlags.ps1`
- `pwsh ./scripts/Test-TestMarkdownPlacement.ps1`
- `pwsh ./scripts/Test-MarkdownPlacement.ps1 -Base <origin/dev merge-base> -Head HEAD`

## Review follow-up — 2026-08-18

PR #396 P2 was resolved in `00f9de38`. The release command is now an atomic,
lease-checked transaction that includes the reviewed SHA as a no-op `dev`
refspec; concurrent `dev` movement rejects the transaction before `main`
changes. `pwsh ./scripts/Test-DocumentationLinks.ps1` and
`pwsh ./scripts/Test-MarkdownPlacement.ps1 -Base <origin/dev merge-base> -Head HEAD`
passed after the change.
