# Post-implementation report — PLAT-014

## Summary

Corrected the shared Windows LocalDB state classifier so the exact zero-exit “instance doesn't exist” diagnostic is treated as Missing, while ambiguous output remains fail-closed as Unknown. The documented Offline lifecycle now created, smokes, and exactly resets one owned LocalDB run without touching the pre-existing default instance; this unblocks [[PLAT-005]] once PLAT-014 completes independent review and enters Verifying.

## Changes

| File | Change | Why |
|---|---|---|
| `scripts/PegasusPlatform.ps1` | Modified the Windows `Get-PegasusDatabaseState` branch to recognize only the requested instance's line-anchored missing diagnostic, preserve Running/Stopped, and classify contradictory or other zero-exit output as Unknown. | Restores correct absence detection without weakening the existing ownership guard. |
| `scripts/Test-PegasusPlatform.ps1` | Added a Windows-only standalone contract test using the existing `-Command` seam. | Covers the exact two-line LocalDB 2025 fixture, wrong-instance/wrapper/unrecognized/contradictory responses, state lines, and non-zero behavior without mutating LocalDB. |
| `.github/workflows/ci.yml` | Added the always-run `local-development-scripts` Windows job. | Gives the Windows-specific parser contract an explicit automated caller without conditional change-classification plumbing. |

## Governing docs

No PRD, FRD, or ADR is linked or changed: this fix restores the existing local tooling contract and adds no product behavior or architectural boundary. It follows `docs/runbook.md#offline-development-profile` by using the supported Offline Doctor → Initialize → Start → Status → Smoke → exact-run Reset lifecycle. `scripts/PegasusPlatform.ps1` remains the sole state-policy owner; `scripts/Invoke-LocalDevelopment.ps1` and its fail-closed ownership behavior are unchanged.

## Risks / follow-ups

- The canonical `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"` did not return within either a 10-minute or 30-minute command window, without emitting a terminal failure. It is a timeout, not a passing result; the reviewer should inspect CI and decide whether a separate diagnostic ticket is warranted.
- Offline initialization's first Doctor invocation reported Azurite absent, then installed the repository-pinned packages and completed a passing final Doctor. This is bootstrap behavior, not a code change.
- [[PLAT-005]] remains held and blocked by this ticket. Do not take over its existing worktree; resume it only after PLAT-014 independently passes review and is moved to Verifying.

## Verification hand-off

Pre-merge evidence recorded on commit `6cb9c59a`:

- `pwsh ./scripts/Test-PegasusPlatform.ps1` — passed.
- `pwsh ./scripts/Test-CiChangeFlags.ps1` — passed.
- `dotnet restore ./Pegasus.slnx --locked-mode` — passed.
- `dotnet build ./Pegasus.slnx --configuration Release --no-restore` — passed with 0 warnings and 0 errors.
- Owned Offline run `67a53c21ebc54bcc8c3cc98d6dab7c19` — Status healthy; Smoke passed with source SHA `6cb9c59a761909a5e926452a2684af0438559cb9`; exact-run Reset removed its run directory and `PegasusDevelopment_67a53c21ebc54bcc8c3cc98d6dab7c19`, while `MSSQLLocalDB` remained.

After independent review and merge into `dev`, move PLAT-014 to Verifying and leave it there as requested; do not promote anything to `main`. A later authorized verification on the appropriate merged source should rerun the focused script and the documented owned lifecycle, while recording the full non-corpus suite only if it reaches a terminal result.
