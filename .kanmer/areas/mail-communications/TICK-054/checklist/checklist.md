# Checklist — MAIL-13

- [ ] Revalidate prerequisites and the exact existing Core/Infrastructure/Web helpers to reuse.
- [ ] Implement the minimal Core contract/policy with fail-closed validation.
- [ ] Implement the mailbox-scoped persistence/projection/adapter boundary with idempotency and durable evidence.
- [ ] Wire the real caller without duplicating business rules.
- [ ] Add focused acceptance tests for rights, confirmation, idempotency, stale state, adapter failure and recovery semantics.
- [ ] Run `dotnet restore` and `dotnet build --configuration Release`.
- [ ] Run focused tests and the relevant full suite.
- [ ] Run and record the four-lens simplification pass.
- [ ] Update governing/current-state documentation only to the evidence tier actually reached.
- [ ] Write the post-implementation report with commands, results, residual risks and deployment qualification.

- [ ] Immediately before live verification, record exact approval for the disposable mailbox message, folder/category targets, and reversible operations; capture immutable identity and initial state.
- [ ] Run and evidence read/unread, category add/remove, flag/unflag, folder move, Deleted Items deletion, and restoration with state/history after each step.
- [ ] After proving restoration, obtain fresh exact confirmation and permanently delete only that disposable message where supported; abort on identity/version mismatch.
