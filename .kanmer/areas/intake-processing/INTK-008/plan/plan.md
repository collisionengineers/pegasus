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
