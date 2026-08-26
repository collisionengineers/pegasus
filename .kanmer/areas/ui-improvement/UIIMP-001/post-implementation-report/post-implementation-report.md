# Post-implementation report — UIIMP-001

## Summary

The supported local-development launcher now has an explicit `Live|Test` UI mode. Live remains the default and reaches the existing lifecycle dispatcher unchanged. Test is Start-only, opens the tracked static catalogue through the Windows shell or Linux `xdg-open`, and returns before lifecycle mutex acquisition or initialization.

## Changes

| File | Change | Why |
|---|---|---|
| `scripts/Invoke-LocalDevelopment.ps1` | Modified | Adds validated mode selection, Test-only argument validation, fixed catalogue resolution, cross-platform opening, and the early return. |
| `scripts/Test-UiModes.ps1` | Added | Checks the parameter contract, early branch ordering, both supported opener branches, invalid combinations, missing catalogue failure, resolved file URI, and no local-development state mutation. |
| `README.md` | Modified | Names Live as the default and gives the Test command. |
| `docs/runbook.md` | Modified | Defines which mode owns runtime lifecycle actions and documents Test as dependency-free static browsing. |

## Governing docs

This chore has no linked PRD, FRD, or ADR and changes no product behavior. It updates the existing local-development procedure in `docs/runbook.md` and keeps Test UI outside Web, Worker, and deployment composition.

## Risks / follow-ups

Linux opener selection is source-checked here because verification ran on Windows; merged-main verification should exercise the Test command on Linux CI or a supported Linux workstation when available. The full non-Corpus suite did not complete locally and exposed two unrelated worker-release fixture failures; the focused UI checks, Release build, and publish boundary passed.

## Verification hand-off

On merged `dev` run:

- `pwsh ./scripts/Test-UiModes.ps1` — expect `Live/Test UI launcher checks passed.`
- `pwsh ./scripts/Test-UiCatalogue.ps1` — expect 52 routed sources, 60 prototypes, and zero broken references.
- `dotnet restore ./Pegasus.slnx --locked-mode`
- `dotnet build ./Pegasus.slnx --configuration Release --no-restore` — expect zero warnings and errors.
- Publish Web and Worker to a temporary directory, then assert no `docs/design/test-ui` path and no `route-inventory` marker exists.
- Manually run `pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Start -UiMode Test`; confirm the catalogue opens and `artifacts/local-development` is unchanged.
- On Linux, run the same Test command and confirm `xdg-open` opens the catalogue.
