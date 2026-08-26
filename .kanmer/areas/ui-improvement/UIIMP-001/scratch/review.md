## Independent review — 2026-08-26

Reviewer: independent subagent `/root/uiimp_001_review`.

### Changes
- `scripts/Invoke-LocalDevelopment.ps1`: adds validated `Live|Test` selection, with Live default and a Test-only branch before lifecycle mutex/initialization; Test validates Start-only inputs, resolves the fixed catalogue, opens it through the supported platform path, returns the local-file contract, and exits.
- `scripts/Test-UiModes.ps1`: adds focused contract, ordering, validation, state-isolation, missing-catalogue, and platform-opener checks.
- `README.md` and `docs/runbook.md`: document the Live default, Test command, and lifecycle boundary.

### Comments and disposition
- Blocking: none.
- Non-blocking: the GitHub `documentation` job fails on an upstream `.grok/skills/kanmer-setup/SKILL.md` link to absent `docs/manual/greenfield.md`. This file is present unchanged on `origin/dev`, is outside UIIMP-001's four-file diff, and the PR's own documentation links pass locally. Disposition: won't-do-because unrelated upstream repository drift; no nested review ticket created for this UI task.
- Non-blocking: the implementation report accurately discloses the previously observed full-suite worker fixture failures and does not claim a full-suite pass. Disposition: accepted as unrelated evidence, with focused UI validation and Release build independently repeated.

### Verification
- Open questions: none.
- Report matches the four-file diff and the file map.
- No governing PRD/FRD/ADR applies; runbook changes remain procedural.
- `origin/dev` merged into the task branch before final checks; the resulting PR diff remains exactly the four planned files and `git diff --check` passes.
- `pwsh ./scripts/Test-UiModes.ps1`: passed.
- `pwsh ./scripts/Test-UiCatalogue.ps1`: passed (52 routed sources, 60 prototypes, zero broken references).
- Locked restore and Release build: passed, zero warnings/errors.
- Independent Web/Worker Release publish inspection: 637 files, zero Test UI paths or `route-inventory` markers.
- GitHub checks applicable to this change: changes, local-development-scripts, and reference-data passed. Downstream application test jobs were correctly skipped by change classification.

### Simplification
The plan's simplification disposition is honest: the product change is one parameter and one early branch in the existing launcher, reusing existing platform detection and adding no runtime route, feature flag, service, project, or abstraction.

### Verdict
Pass. Correctness, security boundary, focused tests, deployment exclusion, documentation, and proportionality satisfy UIIMP-001. The unrelated upstream documentation-link failure does not arise from or change this PR's behavior.
