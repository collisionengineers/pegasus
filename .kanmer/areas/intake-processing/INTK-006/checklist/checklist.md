# Checklist — INTK-006

- [x] Create this worktree from the INTK-005 PR branch `intk-005-grouped-upload`; record the branch/base and planned post-review rebase in scratch.
- [ ] Reconcile operator notes, PRD, FRD-01, FRD-02, FRD-06, FRD-12, design, capabilities, and index in the review/rebase workstream; this branch does not invent protected Case semantics.
- [ ] Confirm docs explicitly define Image-Only Case reference, principal, lifecycle, and later resolution.
- [ ] Link the updated governing docs and name the exact existing Case creation use case in the plan.
- [x] Add one canonical Core group-routing policy and exhaustive decision matrix.
- [x] Distinguish detector-empty, recognizer-empty, below-bar, accepted, and technical recognition outcomes.
- [ ] Add non-sensitive per-stage recognition telemetry.
- [x] Change image automation to load the complete INTK-005 group and wait for all terminal member results.
- [ ] Add a unique persisted group routing outcome and replay/concurrency handling.
- [x] Reuse `ImageIntakeCasePairing` as the single eligible-case matcher.
- [ ] Implement the one-existing-Case branch and attach every group member.
- [ ] Implement the Image-Only branch through the documented existing Case owner.
- [ ] Preserve every receipt, original filename, source identity, ordinal, suggestion, and history entry.
- [ ] Add status/history presentation for waiting, associated, Image-Only created, and technical failure.
- [ ] Test accepted VRM + one match associates all images.
- [x] Test readable overview + no-plate close-up associates both.
- [ ] Test zero match creates one Image-Only Case.
- [ ] Test multiple matches create one Image-Only Case and no existing association.
- [ ] Test conflicting VRMs create one Image-Only Case and no existing association.
- [ ] Test all no-readable/below-bar results create one Image-Only Case.
- [ ] Test processing/retryable member prevents premature finalization.
- [ ] Test technical terminal failure follows documented failure/Unidentified semantics.
- [ ] Test replay, reverse completion order, and concurrent finalizers produce exactly one outcome.
- [x] Run `dotnet restore`.
- [x] Run `dotnet build --configuration Release`.
- [x] Run focused recognition and Core tests (19 Core tests, 5 VRM integration tests); persistence/web/migration/browser evidence remains for verification.
- [ ] Run full `dotnet test`.
- [x] Perform and record the dated four-lens simplification pass.
- [ ] Update checklist and post-implementation report with actual evidence.


## Parallel-branch execution note — 2026-08-19

This ticket is intentionally implemented from the INTK-005 PR branch before PR merge. Record the exact base SHA in execution scratch and ticket notes. When INTK-005 is reviewed, rebase this branch onto the reviewed INTK-005 result and resolve any conflicts before its PR is finalized. INTK-005 review/merge coordination is not an execution blocker.


## Execution evidence — 2026-08-19

- Base: `ed04f498` (`intk-005-grouped-upload`, INTK-005 PR #416); worktree `.worktrees/intk-006`.
- `dotnet restore Pegasus.slnx`: passed.
- `dotnet build Pegasus.slnx --configuration Release --no-restore`: passed with 0 warnings and 0 errors.
- Focused Core tests: 19 passed (group routing, automatic image intake, grouped intake).
- VRM integration tests: 5 passed.
- Simplification pass: reuse existing `IImageIntakeCaseCandidates`, `TryRegisterAndAssociateAsync`, and receipt/group ports; no duplicate matcher or direct EF Case write added. The Image-Only Case branch remains intentionally unimplemented until the existing Case owner has an authorized principal/reference contract; this is recorded for INTK-005 review/rebase rather than invented here.
