# Checklist — TICK-197

- [x] Create the isolated branch/worktree from current `origin/dev` after overlap checks
- [x] Add complete infrastructure path detection and exported CI signal
- [x] Add the credential-free, path-scoped infrastructure validation job
- [x] Add regression coverage for infrastructure path classification
- [x] Run focused validation, Local Bicep validation, and final scope inspection

## Progress notes

- Created `task/infra-validation-lane` at current `origin/dev` (47086670); confirmed UI-revamp paths remain outside the ticket diff.
- Current dev already contains the corrected one-warm-replica validator assertion from DELIVE-001, so no redundant validator edit was needed.
- Added a reusable two-signal classifier, direct regression checks, and a Windows Local-mode infrastructure job with no login, OIDC, azd, or deployment step.
- Verification passed: classifier regression suite, `Test-AzureDeploymentPlan.ps1 -Mode Local`, documentation links (215 files), YAML parse, and `git diff --check`.
