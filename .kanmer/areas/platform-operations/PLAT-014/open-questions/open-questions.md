# Open questions — PLAT-014

No operator decision is required.

## Resolved technical constraints

- [x] Read-only reproduction on SQL Server LocalDB 2025 (17.0.4025.3) confirms that a missing instance can emit the explicit `doesn't exist!` diagnostic and exit 0.
- [x] The live diagnostic is two stdout lines, with a trailing space after `doesn't exist!`; only the inner requested-instance missing line may produce `Missing`. The wrapping `information failed` line without that inner diagnostic remains `Unknown`.
- [x] Only that explicit missing-instance signal (or the existing non-zero exit behavior) may produce `Missing`; any unrelated zero-exit response without a recognized state remains `Unknown` and fail-closed.
- [x] The existing `-Command` parameter is a sufficient test seam: an in-process PowerShell function can supply deterministic output and exit codes without a live LocalDB mutation or new abstraction.
- [x] The regression test requires an explicit Windows CI caller because `Get-PegasusDatabaseEngineKind` is OS-based. Planning may choose its smallest CI placement, accounting for changed-path classification if it uses a conditional lane.

## Parked (explicitly deferred)

- [Reason: outside this fix] Any change to database ownership, reference allocation, LocalDB naming, or the Linux container lifecycle.
- [Reason: sibling caller, different instance] `Initialize-LocalDevelopment.ps1` still infers `MSSQLLocalDB` presence from `$LASTEXITCODE`. That is default-instance bootstrap, not the per-run Offline instance that refuses Start. File a follow-up only if Initialize fails on a machine whose default instance is absent.
- [Reason: owned by linked ticket] Screenshot capture and visual proof remain in [[PLAT-005]] after this lifecycle is verified.
