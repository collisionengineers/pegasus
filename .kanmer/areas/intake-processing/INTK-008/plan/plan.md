# Plan — INTK-008

## Outcome

Make ImageIntake the explicit Image-initiated Case projection. A readable VRM gets the existing immutable VRM-sequenced reference. The record is searchable and grouped, remains Awaiting instruction until a unique formal match, then becomes MergedIntoInstructionCase; staff may instead StaffClose it with a required reason. No formal Cases row, Case/PO, Principal, Audit, or Unidentified reference is created.

## Governing docs

- PRD pegasus-product.md: reconcile product origin terminology and outcome.
- FRD-01: retain formal Principal/Case/PO allocator; add separate Image-initiated terminal merge/closure projection and history.
- FRD-02: define source identity, VRM reference, exact matching, immutable origins, and replay/CAS.
- FRD-05: define Box custody under VRM reference while preserving approved-root, staging, and local-alpha rules.
- FRD-06: connect usable VRM registration to Image-initiated reference; leave no-readable/conflicting handling to INTK-007.
- FRD-12/design README: use Image-initiated labels, searchable states, history, and reasoned action.
- ADR-0013 is accepted and must not be edited. Create ADR-0029 as the one technical decision: Image-initiated Case is a lifecycle projection over ImageIntake and uses a VRM-keyed Box custody target through the existing adapter. Mark ADR-0013 superseded and update the index.
- Amend CONTEXT.md, capabilities.md, and docs/index.md so no stale “pre-Case only” statement conflicts with the decision.

## Ordered implementation steps

1. Re-read files.md before each code batch. Confirm no new formal Case store, allocator, runtime, or Box client is introduced.
2. Add Core lifecycle vocabulary and commands in ImageIntakeContracts.cs:
   - states AwaitingInstruction, MergedIntoInstructionCase, StaffClosed;
   - detail/summary fields for state, merge target, closed actor/reason/time;
   - append-only history record;
   - Merge request and StaffClose request with ActionActor, operation key, expected version, and reason;
   - query/store ports for transition and history.
3. Add ImageIntakeLifecycle.cs policy:
   - validate staff casework actor and bounded reason;
   - allow only AwaitingInstruction → MergedIntoInstructionCase or StaffClosed;
   - require a formal Case id/reference for merge and no Case id for staff closure;
   - return replay for the same operation fingerprint;
   - reject terminal transitions and conflicting operation keys;
   - keep Image Intake Reference immutable.
4. Extend ImageIntakeEntity and DbContext:
   - current state, state version, merged Case id/reference, closure fields;
   - ImageIntakeLifecycleEvent entity with event type, actor, reason, operation key, target, timestamp;
   - unique operation key and state/reference indexes;
   - additive migration with AwaitingInstruction backfill.
5. Implement EfImageIntakeStore transitions in one serializable transaction:
   - load row and current version;
   - validate persisted origin/association;
   - CAS expected version;
   - append event and update projection;
   - return replay for exact operation and conflict for divergent reuse;
   - keep existing registration/reference and association projections unchanged.
6. Update ImageIntakeCasePairing:
   - after AutoLinkAsync succeeds, invoke the lifecycle merge operation;
   - treat merge failure as recoverable/non-blocking to formal Case acceptance;
   - ensure replay cannot duplicate history.
7. Add the custody seam:
   - define ImageIntakeCustodyRoot/target in Core, using immutable Image Intake Reference;
   - extend the existing Box/local custody adapters and DI rather than creating a client;
   - use a distinct binding file/name from formal Case custody;
   - preserve root fencing, lease guards, source hash checks, and local-alpha non-mutating behaviour;
   - record custody state/history without treating custody as formal Case allocation.
8. Update Web:
   - Index filters All/Awaiting instruction/Merged/Staff-closed and labels;
   - exact reference and VRM search continue to return Image-initiated records;
   - Details displays state, VRM reference, origin filename/group evidence, merge target, history, and Box custody state;
   - add anti-forgery StaffClose form requiring reason and existing PerformCasework authorization;
   - terminal records are read-only;
   - Cases/Index and Cases/Details retain searchable Image-initiated rows and show merge history beside formal Case history.
9. Amend governing docs and ADR-0029 in the same branch. Do not edit ADR-0013 body; set only its superseded frontmatter/index relationship per repository convention.
10. Add Core tests for policy, transition matrix, replay/conflict, history, and reverse pairing. Add integration tests for migration, persistence, search, merge and staff close, authorization, and no formal Case row. Add architecture/composition tests for one custody boundary.
11. Run the simplification pass over the branch diff. Record reuse, duplication, efficiency, and altitude findings in the checklist.
12. Run restore, Release build, focused Core tests, ImageIntake SQL/web integration tests, architecture tests, and full test. Record exact outputs in the post-implementation report.
13. Update ticket commits/PR, open the PR targeting dev, and move only to Review.

## Acceptance evidence

- One registered VRM record appears as Awaiting instruction with reference AB12ABC-01 and all group/original-filename evidence.
- A unique non-overlapping formal match produces one merge event, terminal Image-initiated state, formal Case history link, and no Case/PO change.
- Staff closure requires a reason, is idempotent, terminal, and visible in history.
- Terminal transition/replay/concurrency tests pass.
- Search returns Awaiting, Merged, and Staff-closed Image-initiated records.
- Box custody uses the VRM reference through the existing guarded adapter; no real external mutation is performed in local tests.
- Conflicting/no-readable groups remain INTK-007 Unidentified and do not receive an Image-initiated reference.

## Takeover — 2026-08-19 (claude-code, DELIV-012, operator decision)

Step 7 of the ordered implementation steps above ("Add the custody seam") is
**reversed**, not completed: a repo-wide search found no application caller
for `IImageIntakeCustody`/`CreateOrGetRootAsync` anywhere. Wiring one with
real integration coverage is feature-sized work, not a bug fix belonging to
this takeover's blocker list, so the seam (interface, both adapter
implementations, DI registration) was removed instead of shipped dark.
`git diff origin/dev -- src/Pegasus.Core/Custody src/Pegasus.Infrastructure/Custody
src/Pegasus.Infrastructure/DependencyInjection.cs` is empty as a result. The
matching custody claims were removed from ADR-0029's Decision section,
FRD-05, and design/README.md's Image-initiated Case surface note (all in the
docs commit). If a future ticket needs Image-initiated custody, it should
design the caller first and let the adapter follow it, per the repo's
"no abstraction without a caller" rail.

## Simplification pass — 2026-08-19 (takeover)

Reuse/duplication/efficiency/altitude review over the branch's own diff
(`git diff origin/dev...HEAD`), done by hand plus an independent
`code-simplifier` agent pass restricted to `src/`+`tests/`.

1. **Duplication removed — lifecycle-merge triggering.** Only the reverse
   pairing path attempted the merge transition before this takeover. Added
   one method, `ImageIntakeCasePairing.SyncMergeAfterLinkAsync`, that the
   automatic forward path (`ImageIntakeAutomation`), the automatic reverse
   path (`PairAcceptedCaseAsync`), and the manual staff path (`LinkIntake`)
   all call — one owner for the concept instead of three copies.
2. **Duplication removed — lifecycle state labels.** `Index.cshtml.cs` had
   its own literal switch over `ImageInitiatedCaseState`; reduced to compose
   from the single `OperatorLabels.ImageIntakeLifecycleState` mapping.
3. **Reuse — request-fingerprint replay.** The new `RequestFingerprint`
   column/comparison on `ImageIntakeLifecycleEvents` mirrors the existing
   pattern on `ImageIntakes.RequestFingerprint` and `IntakeMutationHistory`
   exactly — the established convention, not a new one.
4. **Reuse/simplification — case reference resolution.** Dropped
   `CaseReference` from `MergeImageInitiatedCaseRequest`; the store now
   resolves it once from `context.Cases` inside the transition transaction,
   removing a field every caller had to source and validate (and that the
   manual-link path had no cheap way to obtain).
5. **Efficiency — accepted, not fixed.** `PairAcceptedCaseAsync` lists all
   Image intakes and filters to `AwaitingInstruction` in memory rather than
   pushing the filter into SQL. At this product's stated scale (OPS-20: ~8
   concurrent staff, ~2,000 new cases/month) this is not a real cost; a
   store-level state filter would be premature optimisation here.
6. **Altitude — accepted.** The `RequestFingerprint` column's
   Designer.cs/ModelSnapshot.cs entries were hand-edited to match the
   existing generated shape rather than regenerated via `dotnet ef
   migrations remove`/`add`, to avoid an unrelated EF-tooling diff across
   migration history. The build and every migration-touching integration
   test exercising the generated snapshot pass, which is the real proof.

Full disposition of the 13 Codex PR review comments, the custody
remove-vs-wire reasoning, the QdosAllocationRecoveryTests/CASE-005 evidence,
and the release-route census verification method (including why
`Test-MigrationGrants.ps1` was not imported) are recorded in the checklist
document's dated sections rather than duplicated here.
