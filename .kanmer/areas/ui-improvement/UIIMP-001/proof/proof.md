# Proof — UIIMP-001

## Verified merged result

Verified the exact merge commit `93060b619ca92c2f6b3675ddba025abb724c0aa1` on `dev`.

PR: https://github.com/collisionengineers/pegasus/pull/559  
Merged: 2026-08-26T13:52:04Z

## Evidence

- `pwsh -NoProfile -File scripts/Test-UiModes.ps1` — passed: `Live/Test UI launcher checks passed.`
- `pwsh -NoProfile -File scripts/Test-UiCatalogue.ps1` — passed: 52 routed sources, 60 prototypes, 0 broken local references.
- `dotnet build Pegasus.slnx --configuration Release --no-restore` — passed with 0 warnings and 0 errors.
- Published `src/Pegasus.Web/Pegasus.Web.csproj` and `src/Pegasus.Worker/Pegasus.Worker.csproj` in Release mode, then scanned relative publish paths and text assets — 637 files, 0 Test UI path hits, 0 catalogue marker hits.
- `pwsh -NoProfile -File scripts/Test-DocumentationLinks.ps1` — one upstream failure: `.grok/skills/kanmer-setup/SKILL.md` links to missing `docs/manual/greenfield.md`. The file has identical blob `dd006a1c6eeb1742b84b5be08b7efe80d483d149` at the merge and its first parent, and PR #559 did not change it. This is unrelated to UIIMP-001.

## Result

Live UI remains the default launcher path. Test UI is validated as an isolated local catalogue path, and its files are absent from deployable Web and Worker output. No live deployment is required or performed.
