# Checklist

- [ ] Confirm INTK-041 and INTK-003 are merged and INTK-040 overlap is clear; create/take the worktree from refreshed `origin/dev`.
- [ ] Move the shared queue sender/configuration into Infrastructure and remove superseded Worker-only copies.
- [ ] Add one Core immediate post-commit publisher reusing claim/enqueue/mark/release for intake and relevant custody work.
- [ ] Invoke it after both email/manual receipt and relevant committed custody-producing use cases.
- [ ] Preserve truthful committed outcomes on queue failure and one-minute recovery.
- [ ] Change timer-first schedules/comments to recovery-only cadence.
- [ ] Add Web sender-only configuration/RBAC without deploying.
- [ ] Add focused Core/EF/Web/Worker/infrastructure tests and update as-built docs.
- [ ] Run Release verification and the four simplification lenses.
- [ ] Report, commit, push, open the PR to `dev`, and move to Review.
