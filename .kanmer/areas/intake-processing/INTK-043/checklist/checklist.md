# Checklist

- [x] Create/take an isolated INTK-043 worktree from current `origin/dev`.
- [x] Add low-cardinality intake stage timing and retain dependency/exception telemetry.
- [x] Add the unified typed queue dispatcher and remove the ordinary external-work queue/function/configuration.
- [x] Configure one 2 GiB always-ready unified function in Bicep.
- [x] Update ADR/FRD/PRD/capability and keep deployed-state documents unchanged pending deployment.
- [x] Run Release build, Core tests, full architecture tests, configuration-startup tests, Bicep compilation, and simplification review.
- [ ] Obtain independent review, commit, push, open PR to `dev`, and move to Review.
- [ ] After deployment, measure supported input cohorts and update current architecture/operations with the deployed facts.
- [ ] Complete [[MAIL-013]] for Graph wake-up and [[INTK-001]] for truthful sender/state projection.
