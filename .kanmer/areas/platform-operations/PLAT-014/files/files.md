# Files — PLAT-014

## Where the change lands

| Path | Why |
| --- | --- |
| `scripts/PegasusPlatform.ps1` | Correct the shared Windows LocalDB state classifier; it is the single policy owner for `Missing` / `Stopped` / `Running` / `Unknown`. |
| `scripts/Test-LocalDevelopment.ps1` *(new, if no existing script-test seam is found during planning)* | Exercise the classifier with a temporary command shim, including the observed zero-exit missing-instance response, without creating a database. |
| `.github/workflows/ci.yml` *(only if needed to execute the focused script test on its existing Windows runner)* | Make the regression check an automated Windows check rather than a one-off local observation. |

## Context files

| Path | What it tells the implementer |
| --- | --- |
| `scripts/Invoke-LocalDevelopment.ps1` | The helper’s callers convert only `Missing` to “absent”; Start and Reset rely on that for run ownership and must not gain a bypass. |
| `scripts/PegasusPlatform.ps1` | Windows uses `sqllocaldb`; Linux uses Docker with the same four-state return contract. The Linux branch must remain untouched. |
| `docs/runbook.md#offline-development-profile` | The supported no-cloud lifecycle and its per-run Windows LocalDB ownership boundary. |
| `.github/workflows/ci.yml` | Existing Windows CI lanes and the current standalone PowerShell assertion-script convention. |
| `PLAT-005 checklist.md` | The blocked evidence task that must resume only after a successful supported Start → Status → Smoke → Reset run. |

## Ripple effects

- All Windows Offline lifecycle callers benefit because they share `Get-PegasusDatabaseState`; no application project or deployed runtime changes.
- The state classifier’s conservative `Unknown` result protects Start and Reset from acting on an ambiguous pre-existing instance.
- A focused automated script test may require a narrowly scoped Windows CI invocation; it must not provision LocalDB, Docker, Azure, or vendor resources.
- A successful local lifecycle verification unblocks [[PLAT-005]] but does not itself produce PLAT-005’s screenshots.

## Out of scope

- Any manual database deletion, ownership bypass, or alteration to the LocalDB naming scheme.
- Linux/Docker database behavior, application database migrations, Web/Worker code, production infrastructure, or documentation that changes product behavior.
- Completing PLAT-005’s visual evidence.
