# Files — UIIMP-001

## Where the change lands

| Path | Why |
|---|---|
| `scripts/Invoke-LocalDevelopment.ps1` | Add the validated UI mode, preserve the existing Live branch byte-for-behaviour, and open the Test catalogue without creating owned resources. Argument validation and cross-platform launching are the main risks. |
| `docs/runbook.md` | Document the two modes, their commands, and the fact that only Live owns lifecycle actions and runtime evidence. |
| `README.md` | Keep the short local-development entry point aligned with the supported launcher interface. |
| `scripts/Test-UiModes.ps1` | Add focused, non-cloud checks for default/Live routing, Test catalogue resolution, invalid combinations, and absence of application startup in Test mode. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `scripts/PegasusPlatform.ps1` | Existing platform detection and process conventions; reuse these rather than adding an unrelated OS abstraction. |
| `scripts/Initialize-LocalDevelopment.ps1` | Live initialization is an exact-source prerequisite and must not become a Test UI prerequisite. |
| `scripts/Build-ReleaseArtifacts.ps1` | The exact Web/Worker publish boundary that Test UI must remain outside. |
| `docs/design/test-ui/index.html` | The fixed Test-mode target delivered by [[UIIMP-002]]; its existence is the launch precondition. |
| `AGENTS.md` | Simplicity, no parallel runtime, documentation placement, and release-boundary constraints. |

## Ripple effects

Launcher help and validation change; documentation examples must stay synchronized. CI should run the focused script check. No Web, Worker, database, or production configuration changes follow.

## Out of scope

Creating prototype pages, changing Razor pages, adding application routes, adding runtime feature flags, changing manifests, and changing deployment infrastructure.
