# Checklist

- [x] Confirm INTK-041 and INTK-003 are merged and INTK-040 overlap is clear; refresh the ticket worktree from `origin/dev`.
- [x] Move the shared queue sender adapters into Infrastructure and remove superseded Worker-only copies.
- [x] Add Core exact-ID post-commit publishers reusing claim/enqueue/mark/release for intake and external work.
- [x] Invoke intake publication through the shared manual/grouped/mailbox receipt path and external publication from committed case, vehicle, and image custody work.
- [x] Preserve truthful committed outcomes on recoverable queue failure and a one-minute recovery sweep.
- [x] Change timer-first schedules/comments to recovery-only cadence.
- [x] Add Web sender-only configuration/RBAC without deploying.
- [ ] Complete focused integration validation where the local SQL test host is responsive; update the implementation report.
- [ ] Run the simplification pass, commit, push, open the PR to `dev`, and move to Review.
