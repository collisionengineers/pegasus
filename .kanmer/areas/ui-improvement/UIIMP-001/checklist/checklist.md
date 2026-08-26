# Checklist — UIIMP-001

- [x] Confirm [[UIIMP-002]] provides `docs/design/test-ui/index.html`.
- [x] Add the validated `UiMode` parameter and reject unsupported action/control combinations.
- [x] Preserve the existing Live startup path and add dependency-free Windows/Linux Test catalogue opening before lifecycle initialization.
- [x] Add `scripts/Test-UiModes.ps1` coverage for defaults, validation, path resolution, opener selection, and no Live initialization in Test mode.
- [x] Update `README.md` and `docs/runbook.md` with the two-mode contract.
- [x] Run focused script/parser checks and canonical restore/build/tests.
- [x] Inspect Release publish output and prove Test UI is absent.
- [x] Record verification results for the post-implementation report and proof.

## Progress notes

- 2026-08-26: Confirmed [[UIIMP-002]] is merged at current `origin/dev`; its catalogue validator reports 52 routed sources and 60 prototypes.
- 2026-08-26: `Test-UiModes.ps1`, `Test-UiCatalogue.ps1`, documentation links, Release restore, and Release build passed. The full non-Corpus test run was stopped after more than eight minutes without completion; before stopping, Core passed 999/999 and Architecture reported two pre-existing worker-release fixture failures unrelated to this diff (`WorkerSmokeAcceptsExactDisabledCensusAndBindsApprovedTarget`, `WorkerSmokeAcceptsExactApprovedCensus`).
- 2026-08-26: Direct Web and Worker Release publish inspection found 637 files and zero Test UI paths or catalogue markers.
- 2026-08-26: Merged-commit verification at `93060b61` passed the focused launcher/catalogue checks and Release build. Publish isolation was re-proved. The documentation-link check's single failure is unchanged upstream content outside PR #559.

## Closeout checklist

- [x] Confirm PR #559 is merged.
- [x] Confirm merged-commit proof is final.
- [x] Record outcome, commits, PR, and deployment status.
- [x] Remove only the UIIMP-001 worktree and branch.
- [x] Delete the merged remote branch.
- [x] Release the ticket claim last.
