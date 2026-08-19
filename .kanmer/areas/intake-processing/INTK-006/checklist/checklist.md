# Checklist — INTK-006

- [ ] Create this worktree from the INTK-005 PR branch `intk-005-grouped-upload`; record the branch/base and planned post-review rebase in scratch.
- [ ] Run kanmer-docs and reconcile operator notes, PRD, FRD-01, FRD-02, FRD-06, FRD-12, design, capabilities, and index.
- [ ] Confirm docs explicitly define Image-Only Case reference, principal, lifecycle, and later resolution.
- [ ] Link the updated governing docs and name the exact existing Case creation use case in the plan.
- [ ] Add one canonical Core group-routing policy and exhaustive decision matrix.
- [ ] Distinguish detector-empty, recognizer-empty, below-bar, accepted, and technical recognition outcomes.
- [ ] Add non-sensitive per-stage recognition telemetry.
- [ ] Change image automation to load the complete INTK-005 group and wait for all terminal member results.
- [ ] Add a unique persisted group routing outcome and replay/concurrency handling.
- [ ] Reuse `ImageIntakeCasePairing` as the single eligible-case matcher.
- [ ] Implement the one-existing-Case branch and attach every group member.
- [ ] Implement the Image-Only branch through the documented existing Case owner.
- [ ] Preserve every receipt, original filename, source identity, ordinal, suggestion, and history entry.
- [ ] Add status/history presentation for waiting, associated, Image-Only created, and technical failure.
- [ ] Test accepted VRM + one match associates all images.
- [ ] Test readable overview + no-plate close-up associates both.
- [ ] Test zero match creates one Image-Only Case.
- [ ] Test multiple matches create one Image-Only Case and no existing association.
- [ ] Test conflicting VRMs create one Image-Only Case and no existing association.
- [ ] Test all no-readable/below-bar results create one Image-Only Case.
- [ ] Test processing/retryable member prevents premature finalization.
- [ ] Test technical terminal failure follows documented failure/Unidentified semantics.
- [ ] Test replay, reverse completion order, and concurrent finalizers produce exactly one outcome.
- [ ] Run `dotnet restore`.
- [ ] Run `dotnet build --configuration Release`.
- [ ] Run focused recognition, Core, persistence, web, migration, and browser tests.
- [ ] Run full `dotnet test`.
- [ ] Perform and record the dated four-lens simplification pass.
- [ ] Update checklist and post-implementation report with actual evidence.


## Parallel-branch execution note — 2026-08-19

This ticket is intentionally implemented from the INTK-005 PR branch before PR merge. Record the exact base SHA in execution scratch and ticket notes. When INTK-005 is reviewed, rebase this branch onto the reviewed INTK-005 result and resolve any conflicts before its PR is finalized. INTK-005 review/merge coordination is not an execution blocker.
