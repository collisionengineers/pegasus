# Checklist — INTK-006

- [x] Create this worktree from the INTK-005 PR branch `intk-005-grouped-upload`; record the branch/base and planned post-review rebase in scratch.
- [x] Record all governing-document conflicts and exact amendment paths in files.md/plan.md; canonical document and superseding ADR edits are delegated to [[INTK-008]].
- [x] Confirm the required Image-initiated reference/lifecycle contract and hand it to [[INTK-008]]; its implementation is not claimed by INTK-006.
- [x] Name the existing ImageIntake registration/reference owner and formal Case acceptance boundary in files.md/plan.md.
- [x] Add one canonical Core group-routing policy and exhaustive decision matrix.
- [x] Distinguish detector-empty, recognizer-empty, below-bar, accepted, and technical recognition outcomes.
- [ ] Add non-sensitive per-stage recognition telemetry.
- [x] Change image automation to load the complete INTK-005 group and wait for all terminal member results.
- [x] Group identity/replay boundary is recorded; durable Image-initiated lifecycle outcome persistence is delegated to [[INTK-008]].
- [x] Reuse `ImageIntakeCasePairing` as the single eligible-case matcher.
- [ ] Implement the one-existing-Case branch and attach every group member.
- [x] Confirm the existing ImageIntake owner is the Image-initiated branch; lifecycle implementation is delegated to [[INTK-008]].
- [ ] Preserve every receipt, original filename, source identity, ordinal, suggestion, and history entry.
- [ ] Add grouped status/history presentation in [[INTK-008]] (follow-on).
- [ ] Test accepted VRM + one match associates all images.
- [x] Test readable overview + no-plate close-up associates both.
- [ ] Test one usable VRM with zero match creates the ImageIntake/Image-initiated Case outcome in [[INTK-008]].
- [ ] Test one usable VRM with multiple matches creates one ImageIntake/Image-initiated Case and no existing association in [[INTK-008]].
- [ ] Test conflicting VRMs enter one INTK-007 Unidentified group with conflicting_vrms marker in [[INTK-007]].
- [ ] Test all no-readable/below-bar results enter one INTK-007 Unidentified group in [[INTK-007]].
- [ ] Test processing/retryable member prevents premature finalization.
- [ ] Test technical terminal failure follows the INTK-007/INTK-008 documented outcome.
- [ ] Test replay, reverse completion order, and concurrent finalizers produce exactly one outcome.
- [x] Run `dotnet restore`.
- [x] Run `dotnet build --configuration Release`.
- [x] Run focused recognition and Core tests (19 Core tests, 5 VRM integration tests); persistence/web/migration/browser evidence remains for verification.
- [ ] Run full `dotnet test`.
- [x] Perform and record the dated four-lens simplification pass.
- [x] Update checklist and post-implementation report with the narrowed INTK-006 scope and follow-on boundaries.


## Parallel-branch execution note — 2026-08-19

This ticket is intentionally implemented from the INTK-005 PR branch before PR merge. Record the exact base SHA in execution scratch and ticket notes. When INTK-005 is reviewed, rebase this branch onto the reviewed INTK-005 result and resolve any conflicts before its PR is finalized. INTK-005 review/merge coordination is not an execution blocker.


## Execution evidence — 2026-08-19

- Base: `ed04f498` (`intk-005-grouped-upload`, INTK-005 PR #416); worktree `.worktrees/intk-006`.
- `dotnet restore Pegasus.slnx`: passed.
- `dotnet build Pegasus.slnx --configuration Release --no-restore`: passed with 0 warnings and 0 errors.
- Focused Core tests: 19 passed (group routing, automatic image intake, grouped intake).
- VRM integration tests: 5 passed.
- Simplification pass: reuse existing `IImageIntakeCaseCandidates`, `TryRegisterAndAssociateAsync`, and receipt/group ports; no duplicate matcher or direct EF Case write added. The Image-initiated Case branch remains intentionally unimplemented until the existing Case owner has an authorized principal/reference contract; this is recorded for INTK-005 review/rebase rather than invented here.


## Clarification recorded — 2026-08-19

- [x] Added the two Case-origin model to ticket scope: Instruction-initiated (formal/main, may lack images) and Image-initiated (secondary/pre-instruction, VRM-sequenced reference, no Case/PO).
- [ ] Reconcile the current authoritative docs, which still describe image-only material as pre-Case only, with this clarified product model before completing the Image-initiated persistence and UI implementation.

## Scope split and completion boundary — 2026-08-19

- [x] INTK-006 owns grouped recognition, detector/recognizer diagnostics, stable group aggregation, and unique eligible Instruction-initiated Case association.
- [x] files.md contains the full repository conflict audit and is referenced by plan.md.
- [x] INTK-007 owns grouped Unidentified work and the conflicting_vrms marker.
- [x] INTK-008 owns ImageIntake-as-Image-initiated Case lifecycle, search/history, Box custody presentation, staff closure, and merge/subsumption into an Instruction-initiated Case.
- [ ] Do not claim INTK-006 complete until its PR review is passed; INTK-008 remains a follow-on ticket, not an INTK-005 dependency blocker.
