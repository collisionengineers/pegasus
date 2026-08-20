# Files — PR-023

## Where the change lands

| Path | Why |
|---|---|
| `scripts/Test-PegasusPlatform.ps1` | Reset the test fixture's process-global native exit status after the intentional non-zero case so a fully passing dot-sourced script exits successfully. Risk: resetting it before the final assertion could hide a broken fixture; the reset must be in the success epilogue only. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `scripts/PegasusPlatform.ps1` | The production four-state classifier and its non-zero → Missing behavior are already correct and must not change. |
| `.github/workflows/ci.yml` | The new Windows job intentionally invokes the standalone script through GitHub Actions' PowerShell shell; it needs no registration change. |
| `PLAT-014 post-implementation-report.md` | Records the intended classifier contract, CI caller, and evidence that PR-023 must preserve. |
| [Failed GitHub Actions job](https://github.com/collisionengineers/pegasus/actions/runs/32364388605/job/96410637569) | Shows a passed message followed by process exit 1, which identifies the runner-visible contract to repair. |

## Ripple effects

- A push to the existing PLAT-014 branch re-runs its full pull-request workflow; the new local-development job must pass before independent re-review.
- No production code, lifecycle caller, product document, workflow structure, or external system changes.
- The final deliberate non-zero fixture stays as regression coverage; only the script's post-assertion host status changes.

## Out of scope

- Changing `Get-PegasusDatabaseState`, weakening `Unknown` handling, removing the non-zero fixture, or changing the always-run CI job.
- Merging PR #471, moving PLAT-014, executing PLAT-005, or promoting anything to `main`.
