2026-08-20 read-only validation: `sqllocaldb info PegasusDevelopment_PLAT014_readonly_probe_7f8d2c` on SQL Server LocalDB 2025 (17.0.4025.3) returned exit code 0 and the explicit `doesn't exist!` diagnostic; current `Get-PegasusDatabaseState` classified it as `Unknown`. `sqllocaldb info MSSQLLocalDB` returned exit 0 with `State: Stopped`, confirming the existing state-line parser. Passing an in-process PowerShell function by name through the helper's existing `-Command` parameter successfully supplies synthetic native-command output and `$LASTEXITCODE`, so the regression test needs no temporary executable or live database mutation. Relevant helper/caller/CI files match `origin/dev` at bc0646a6; only unrelated runbook sections differ from the current local `dev` checkout.

2026-08-20 gap review against live LocalDB 2025 output and sibling callers.

- Reproduced missing-instance `sqllocaldb info` for `PegasusDevelopment_PLAT014_readonly_probe_7f8d2c`: exit 0, two stdout lines, trailing space after `doesn't exist!`, objects are `System.String` (not ErrorRecord).
- Existing instances on this workstation: only `MSSQLLocalDB`.
- `Initialize-LocalDevelopment.ps1` still uses raw `$LASTEXITCODE` for the default instance; not the PLAT-005 blocker.
- `Stop-RunResources` throws on `Unknown` when `created` is false, so leftover Failed runs from prior Start attempts also become Reset-able once classification is corrected.
