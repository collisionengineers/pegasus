# Research — PLAT-014: missing Windows LocalDB detection

## Question

Why does the supported `DevelopmentOffline` lifecycle reject a new Windows run as though its LocalDB instance already existed, and what is the smallest safe correction that preserves its ownership guard?

## Findings

- The failure is reproducible with a fresh, non-existent `PegasusDevelopment_PLAT014_readonly_probe_7f8d2c` instance on this workstation.
  - Read-only command evidence on 2026-08-20: SQL Server LocalDB 2025 (17.0.4025.3) printed `LocalDB instance "<name>" doesn't exist!` and returned exit code 0. The current helper classified that result as `Unknown`.
  - A read-only query of the existing `MSSQLLocalDB` instance returned exit code 0 with `State: Stopped`, which the current state-line parser recognizes.
- `scripts/PegasusPlatform.ps1` owns `Get-PegasusDatabaseState`, the repository's sole implementation of the four-state local database contract: `Missing`, `Stopped`, `Running`, or `Unknown`.
  - Its Windows branch returns `Missing` only for a non-zero exit code. A zero-exit response without a recognized `State: Running|Stopped` line falls through to `Unknown`.
  - `git blame` traces this unchanged branch to commit `3f4a35ba`; the relevant helper and caller files match the current `origin/dev` base (`bc0646a6`).
- `scripts/Invoke-LocalDevelopment.ps1` reuses the helper through `Get-RunDatabaseState`. `Test-RunDatabaseExists` intentionally treats every state other than `Missing` as existing.
  - Start refuses an unowned database when that predicate is true. Stop/Reset also refuse an unproved `Unknown` state. Those are the correct fail-closed ownership semantics and are not the defect.
- `docs/runbook.md#offline-development-profile` defines one owned lifecycle—Doctor, Initialize, Start, Status, Smoke, Stop, Reset—and states that Windows uses a per-run LocalDB instance. It prohibits manually composing service terminals. The correction therefore belongs in the common classifier, not in [[PLAT-005]] or an ad-hoc workaround.
- No existing PowerShell test exercises `Get-PegasusDatabaseState` or the local lifecycle state contract. Repository script tests are standalone assertion scripts invoked explicitly by CI.
  - The helper's existing `-Command` seam accepts a PowerShell function name. A test-only function can emit the observed diagnostic and set `$LASTEXITCODE`, so focused coverage needs neither a live LocalDB mutation nor a temporary executable.
  - CI has Windows runners, but no current step owns this local-lifecycle script contract. `scripts/Get-CiChangeFlags.ps1` also does not classify `PegasusPlatform.ps1` as build-relevant, so attaching the check to a conditional lane would require updating that classifier and its regression test.

## Implications

- Recognize the explicit LocalDB missing-instance diagnostic as `Missing` even when the command exits 0. Preserve `Unknown` for every other zero-exit response without a recognized state line.
- Preserve `Test-RunDatabaseExists`, Start's unowned-instance refusal, and Stop/Reset's unknown-state refusal. Once classification is corrected, those existing callers enforce the required ownership boundary.
- Add one focused standalone script test beside the shared helper. It should cover: explicit zero-exit missing response → `Missing`; running/stopped state lines → their existing states; unrelated zero-exit output → `Unknown`; non-zero response → `Missing`. The test can use the existing command-injection seam and must not create LocalDB, Docker, Azure, or vendor state.
- Give that test an explicit Windows CI caller. If planning chooses an existing conditional lane, it must also update and test the lane's changed-path classification; an isolated focused Windows job avoids coupling this script contract to the .NET build.
- After focused verification, run the documented local-only Start → Status → Smoke → Reset lifecycle for one new run and reset only that exact run id. This caller-backed check is what unblocks [[PLAT-005]].

## Open questions

No operator decision is required. Planning may choose the smallest honest Windows CI placement, but it must not omit an automated caller or weaken the fail-closed ownership behavior.
