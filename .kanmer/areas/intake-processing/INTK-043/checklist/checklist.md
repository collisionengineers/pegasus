# Checklist

- [x] Create/take an isolated INTK-043 worktree from current `origin/dev`.
- [x] Add low-cardinality intake stage timing and retain dependency/exception telemetry.
- [x] Add the unified typed queue dispatcher and remove the ordinary external-work queue/function/configuration.
- [x] Configure one 2 GiB always-ready unified function in Bicep.
- [x] Update ADR/FRD/PRD/capability and keep deployed-state documents unchanged pending deployment.
- [x] Run Release build, Core tests, full architecture tests, configuration-startup tests, Bicep compilation, and independent simplification review.
- [x] Merge current `origin/dev`, commit, push, and open the PR to `dev`.
- [x] Resolve [[PR-066]] with the required `function:UnifiedWorkFunction` Flex scale-group designation and focused validation.

## Post-merge follow-ups

Deployment measurement and deployed-state documentation belong to verification/release. [[MAIL-013]] owns Graph wake-up and [[INTK-001]] owns truthful sender/state projection.
