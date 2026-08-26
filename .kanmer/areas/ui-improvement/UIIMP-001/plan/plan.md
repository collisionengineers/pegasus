# Plan — UIIMP-001: Add Live UI and Test UI local-development modes

## Approach

Extend the existing lifecycle launcher with one validated `UiMode` parameter. `Live` remains the default and enters the current code path unchanged; `Test` resolves and opens [[UIIMP-002]]'s tracked catalogue before any initialization, mutex, manifest, database, storage, Web, or Worker work. This is smaller and safer than a second launcher or a development-only application route, and it keeps Test UI outside the deployable host.

## Governing docs

This chore has no linked PRD, FRD, or ADR and changes no product behavior or architecture. It follows the existing local-development boundary in `docs/runbook.md`; that downstream procedure and `README.md` will be updated to describe the new launcher option. No ADR is needed because no runtime, deployment unit, project, or application boundary is added.

## Steps

1. After [[UIIMP-002]] supplies `docs/design/test-ui/index.html`, add `[ValidateSet('Live','Test')]` `UiMode` to `Invoke-LocalDevelopment.ps1`, defaulting to `Live`; reject Test for non-Start actions and reject Live-only failure controls in Test mode.
2. Preserve `Start + Live` as the existing lifecycle path. For `Start + Test`, validate the fixed catalogue path, convert it to a local file URI, open it through the supported Windows shell or Linux `xdg-open`, return the selected mode/path, and create no mutex, manifest, process, port, database, or artifact state.
3. Add `scripts/Test-UiModes.ps1` to exercise parameter/default validation, missing/invalid combinations, platform opener selection, catalogue resolution, and the absence of Live initialization calls in Test mode.
4. Update `README.md` and `docs/runbook.md` with the two commands, the Live default, the Test dependency-free boundary, and the fact that Status/Smoke/Stop/Reset describe only Live owned runs.
5. Run the focused script checks, PowerShell parser validation, canonical Release build/tests required by the runbook, and a publish-content inspection proving `docs/design/test-ui` is absent.

## Verification

Retain command output from `scripts/Test-UiModes.ps1`, the canonical restore/build/focused tests, and a Release publish file scan. Manually invoke both `Start` defaults and `-UiMode Test`: Live must retain its current initialization contract; Test must open the catalogue without creating or changing `artifacts/local-development`.

## Risks / open questions

- Cross-platform shell opening can drift; use the existing platform detector and cover Windows/Linux branches explicitly.
- A Test branch placed too late could create runtime state; tests and review must confirm the branch precedes initialization and lifecycle mutex acquisition.
- [[UIIMP-002]] blocks this ticket because the launcher must not ship with a dead Test target.
