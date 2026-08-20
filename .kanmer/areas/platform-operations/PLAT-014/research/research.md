# Research — PLAT-014: missing Windows LocalDB detection

## Question

Why does the supported `DevelopmentOffline` lifecycle reject a new Windows run as though its LocalDB instance already existed, and what is the smallest safe correction that preserves its ownership guard?

## Findings

- The reported failure is reproducible with a fresh, non-existent `PegasusDevelopment_probe_<guid>` instance on this workstation (Windows, SQL Server LocalDB 2025). `sqllocaldb info <name>` emits “LocalDB instance … doesn't exist!” **and exits 0**.
  - Direct read-only experiment on 2026-08-20: the shared helper returned `Unknown` for that exact command result.
- `scripts/PegasusPlatform.ps1` owns `Get-PegasusDatabaseState`, the sole implementation of the four-state database contract: `Missing`, `Stopped`, `Running`, or `Unknown`.
  - Its Windows branch currently returns `Missing` only for a non-zero exit code; it returns `Unknown` when a zero-exit response lacks a recognized `State: Running|Stopped` line.
- `scripts/Invoke-LocalDevelopment.ps1` reuses that helper through `Get-RunDatabaseState`. `Test-RunDatabaseExists` intentionally treats every state other than `Missing` as existing.
  - Start checks this before creating the instance and refuses with “exists without completed run ownership”; that fail-closed guard is correct for an actual existing or unparseable instance.
- The Offline runbook defines one owned lifecycle—Doctor, Initialize, Start, Status, Smoke, Stop, Reset—and states that Windows uses a per-run LocalDB instance. It explicitly prohibits manually composing service terminals. The correction therefore belongs in the common state classifier, not in PLAT-005 or an ad-hoc manual workaround.
- No existing PowerShell test harness covers `Get-PegasusDatabaseState` or `Invoke-LocalDevelopment`. The repository’s script tests are standalone assertion scripts; CI has Windows runners, but current script-test steps cover unrelated tooling.
  - A focused Windows script test can exercise the helper through a temporary command shim that returns the observed zero-exit missing-instance response, without creating LocalDB state. This is preferable to weakening the lifecycle guard or relying only on a manual run.

## Implications

- Recognize the known LocalDB “doesn't exist” response as `Missing` even when its exit code is zero. Keep any other zero-exit, no-state response as `Unknown`; it must continue to block creation and destructive cleanup.
- Preserve the existing `Test-RunDatabaseExists` and Start ownership logic. They already encode the correct conservative policy once the state is classified correctly.
- Add a focused regression check for: recognized missing response → `Missing`; running/stopped state responses → their existing states; unrelated zero-exit output → `Unknown`. It must not require a live LocalDB instance or cloud access.
- After the focused check, use the documented local-only lifecycle for one new run and reset only its exact run id. That produces the caller-backed verification needed to unblock [[PLAT-005]].

## Open questions

No operator decision is required. The implementation must retain fail-closed handling for every response other than an explicitly recognized missing-instance response.
