# Files — PLAT-014

## Where the change lands

| Path | Why |
| --- | --- |
| `scripts/PegasusPlatform.ps1` | Correct the shared Windows LocalDB classifier. Risk: an over-broad match (especially the wrapping `information failed` line) could misclassify an ambiguous or pre-existing instance as absent and weaken Start/Reset ownership protection. |
| `scripts/Test-PegasusPlatform.ps1` *(new)* | Exercise the classifier through its existing `-Command` seam with synthetic output and exit codes, including the observed two-line zero-exit missing response with its trailing space. It must not create or remove a database. |
| `.github/workflows/ci.yml` | Give the focused script test an explicit Windows caller. Keep it separate from live lifecycle verification and cloud-dependent lanes. Editing this file also trips the existing build and infrastructure change flags; that is expected, not a reason to add new flags. |
| `scripts/Get-CiChangeFlags.ps1` and `scripts/Test-CiChangeFlags.ps1` *(only if the test is attached to an existing conditional lane)* | Ensure changes to the helper/test actually activate that lane and preserve the classifier's executable contract. An isolated always-run focused job does not need these edits. |

## Context files

| Path | What it tells the implementer |
| --- | --- |
| `scripts/Invoke-LocalDevelopment.ps1` | `Get-RunDatabaseState` delegates to the shared helper; `Test-RunDatabaseExists` treats `Unknown` as existing, and Start/Stop/Reset rely on that conservative ownership rule. Do not add a bypass here. `Stop-RunResources` also refuses `Unknown`, so leftover Failed runs from prior Start attempts become Reset-able only after this classifier is corrected. |
| `scripts/PegasusPlatform.ps1` | Windows and Linux share the four-state contract, but use separate LocalDB and Docker branches. `Get-PegasusDatabaseEngineKind` is OS-based, so focused tests of the Windows branch must run on Windows. The Linux branch is not part of this defect. |
| `scripts/Initialize-LocalDevelopment.ps1` | Default-instance bootstrap still uses `$LASTEXITCODE` after `sqllocaldb info MSSQLLocalDB`. That is a sibling zero-exit assumption on a different instance (`MSSQLLocalDB`), not the per-run classifier. Do not expand this ticket into that path. |
| `scripts/Test-CiChangeFlags.ps1` | Existing standalone PowerShell tests use direct assertions and throw on mismatch; it also guards any change to conditional CI path classification. |
| `docs/runbook.md#offline-development-profile` | The supported no-cloud lifecycle, per-run Windows LocalDB ownership boundary, exact-run Reset rule, and evidence limits. |
| `.github/workflows/ci.yml` | Existing standalone script checks are explicit steps; Windows runners are available. The `documentation` job is always-on Windows but is the Markdown lane; `reference-data` is the precedent for a dedicated always-run Windows scripts job. |
| `PLAT-005 research/files/checklist` | The linked visual-evidence task is blocked on a supported local run and must resume only after this ticket proves Start → Status → Smoke → Reset. |

## Ripple effects

- Every Windows Offline lifecycle caller receives the corrected classification because they share `Get-PegasusDatabaseState`; no application project or deployed runtime changes.
- The conservative `Unknown` result remains the guard against ambiguous output and protects Start and cleanup from acting on an unproved instance.
- CI must invoke the new assertion script on Windows. Conditional placement may ripple into `Get-CiChangeFlags.ps1` and its test; a dedicated focused job avoids that extra classification surface. Editing `ci.yml` still activates the existing build and infrastructure lanes.
- Manual verification creates local ignored run artifacts and one exact per-run LocalDB instance, then removes them through the supported Reset action. It uses no cloud or vendor system.
- A successful lifecycle check unblocks [[PLAT-005]] but does not capture or complete that ticket's screenshots.
- Leftover Failed run directories from earlier [[PLAT-005]] Start attempts can be Reset through the supported action after this fix; they must not be deleted by hand.

## Out of scope

- Any manual database deletion, ownership bypass, LocalDB naming change, or weakening of `Unknown` handling.
- `Initialize-LocalDevelopment.ps1` default `MSSQLLocalDB` bootstrap, Linux/Docker database behavior, migrations, Web/Worker code, production infrastructure, deployment, or product-behavior documentation.
- Completing [[PLAT-005]]'s visual evidence.
