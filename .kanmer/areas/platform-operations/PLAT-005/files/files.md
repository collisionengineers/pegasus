# Files — PLAT-005

## Where the change lands

| Path | Why |
| --- | --- |
| `.kanmer/areas/platform-operations/PLAT-005/assets/` | Retain local visual screenshots and a capture manifest as ticket evidence; do not add them to the application repository. |
| `.kanmer/areas/platform-operations/PLAT-005/proof.md` | Later verification links the retained artifacts, routes, viewport, and commands. |

## Context files

| Path | What it tells the implementer |
| --- | --- |
| `docs/runbook.md#offline-development-profile` | The only supported local Offline lifecycle and its no-cloud boundary. |
| `scripts/Invoke-LocalDevelopment.ps1` | Starts, checks, smokes, and stops the owned local Web/worker/database stack. |
| `tests/Pegasus.IntegrationTests/Browser/AccessibilityTests.cs` | Current authenticated routes and viewport/accessibility constraints visual proof must supplement. |
| `src/Pegasus.Web/Properties/launchSettings.json` | Local Development launch URLs and environment setup. |
| `src/Pegasus.Web/Pages/Shared/_Layout.cshtml` | The rail, marks, and absent-count behavior that screenshots must show honestly. |
| `docs/design/README.md` | The binding visual/design authority and approved mark rules. |
| `PLAT-001 proof.md` | The existing browser evidence and the exact missing visual-proof gap this ticket closes. |

## Ripple effects

- The final proof updates PLAT-005 only; [[PLAT-001]] may be linked from the proof as the originating UI evidence.
- A failed visual or browser observation becomes a new ticket; this evidence task does not fix visual defects in place.
- Screenshots must be reviewed for fixture-only content and kept out of application source/build output.

## Out of scope

- Any application, CSS, Razor, Core, Infrastructure, test, or design-authority change.
- Production access, screenshots, telemetry, or deployment.
- A new screenshot-test or visual-regression framework.
