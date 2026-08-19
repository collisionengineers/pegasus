# Files — INTK-006

## Where the change lands

| Path | Exact responsibility |
|---|---|
| `docs/operator-notes.md` | Protected business truth: record the operator-confirmed exhaustive grouped outcome and reconcile the old pre-Case statement. Must be changed through kanmer-docs with explicit operator confirmation already present in this ticket. |
| `docs/prd/pegasus-product.md` | State product outcome/boundary for Image-initiated Cases and preserve Unidentified/Triage/Blocked/Audit distinctions. |
| `docs/frd/frd-01-case-identity-and-lifecycle.md` | Define Image-initiated Case reference, principal, lifecycle, conversion/resolution, and immutable-origin behavior. |
| `docs/frd/frd-02-intake-and-source-identity.md` | Define upload group identity and the exhaustive association-or-create algorithm. |
| `docs/frd/frd-06-vehicle-and-engineering-evidence.md` | Define group-wide recognition aggregation, accepted bar, conflict behavior, and detector/reader diagnostic states. |
| `docs/frd/frd-12-operator-experience.md` and `docs/design/README.md` | Define group status and visible Case outcome/next action. |
| `docs/capabilities.md`, `docs/index.md` | Reconcile capability owners and navigation if governing docs change. |
| [[INTK-005]] Core/Infrastructure group files | Required source of stable group membership and member completion state. Do not infer groups independently. |
| `src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs` | Add only recognition/group outcome contracts needed by automation; keep one canonical state/reason vocabulary. |
| `src/Pegasus.Core/ImageIntake/ImageIntakeAutomation.cs` | Replace per-receipt early return with idempotent group orchestration and exhaustive association-or-create behavior. |
| `src/Pegasus.Core/ImageIntake/ImageIntakeCasePairing.cs` | Extract/reuse the exact unique eligible-case selection rule as the single pairing owner. |
| `src/Pegasus.Core/Cases/` existing acceptance/use-case files identified after docs | Extend the sole Case creation owner for the documented Image-initiated Case type; never write Cases directly from image automation. |
| `src/Pegasus.Core/Intake/DurableIntake.cs` | Invoke/re-drive group automation only after group membership and recognition completion conditions are met. |
| `src/Pegasus.Infrastructure/Vision/OnnxVrmRecognitionEngine.cs` | Preserve detector→recognizer sequence and return distinct safe outcomes for no crop versus unreadable crop. |
| `src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs` | Persist/query per-member suggestions and idempotent associations; widen for group lookup only through Core contracts. |
| `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` | Persist one idempotent group routing outcome and any documented Image-initiated fields/constraints. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_GroupedImageRouting.cs` and snapshot | Add outcome/idempotency schema and any Case-type schema required by the governing docs. |
| `src/Pegasus.Web/Pages/UploadStatus.cshtml(.cs)` or INTK-005 group status page | Show waiting-for-group, associated Case, or created Image-initiated Case accurately. |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml(.cs)` | Show every grouped image/origin on the resulting Case using existing Image Intake/case evidence patterns. |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | Add the single operator label for documented Image-initiated Case/outcome if needed. |
| `src/Pegasus.Web/Program.cs` | Register changed ports/use cases through existing scoped conventions. |
| `tests/Pegasus.Core.Tests/ImageIntake/AutomaticImageIntakeTests.cs` | Exhaustive group routing matrix and replay/concurrency logic. |
| `tests/Pegasus.Core.Tests/ImageIntake/ImageIntakeCasePairingTests.cs` | Exact unique-match/no-overlap/conflict policy remains shared. |
| `tests/Pegasus.IntegrationTests/ImageIntakePersistenceTests.cs` | Group outcome uniqueness, member association, sequence/reference, transaction/replay behavior. |
| `tests/Pegasus.IntegrationTests/MultiFormatIntakeWebTests.cs` | End-to-end multi-image group with readable overview plus no-VRM close-up. |
| `tests/Pegasus.IntegrationTests/ImageIntakeWebTests.cs` and browser tests | Visible group outcome, Case link, origins, and safe reasons. |
| `tests/Pegasus.IntegrationTests/VrmRecognitionEngineTests.cs` | Distinguish no plate from unreadable crop and preserve model/version evidence. |

## Required context before editing

- EPIC-007 `context.md`, complete INTK-005/006 folders, and current doc gates.
- All governing docs listed above after kanmer-docs reconciliation.
- `src/Pegasus.Core/Cases` acceptance/allocation contracts and tests; name the exact reuse point in the plan update before coding.
- Migration and runtime-role conventions in the two newest persistence migrations.
- Existing Image Intake reference is not a Case/PO and must not be silently repurposed.

## Ripple effects

- Base the implementation worktree on the INTK-005 PR branch (`intk-005-grouped-upload`) so grouped contracts are available before merge. Rebase onto the reviewed INTK-005 result later; this is planned coordination, not a blocker.
- INTK-007 supplies the eventual Unidentified destination for technical/unreadable material outside the completed vehicle-group rule; do not duplicate its U-reference taxonomy here.
- If recognition has a technical failure, bounded retry/failure must finish before group routing. Do not create a fallback Case while a member is still retryable.
- Group finalization must be transactional/idempotent so two completing workers cannot create two Cases or split associations.
- Existing Case eligibility excludes post-report/closed/inconsistent candidates according to the shared pairing owner.

## Out of scope

- Changing model weights or accepted 0.80 action bar.
- Logging image content, crops, or raw candidates.
- General document grouping behavior beyond the INTK-005 aggregate.
- A parallel Case store/allocator, new runtime, generic workflow engine, or deployment.


## Parallel-branch execution note — 2026-08-19

This ticket is intentionally implemented from the INTK-005 PR branch before PR merge. Record the exact base SHA in execution scratch and ticket notes. When INTK-005 is reviewed, rebase this branch onto the reviewed INTK-005 result and resolve any conflicts before its PR is finalized. INTK-005 review/merge coordination is not an execution blocker.

## Repository conflict and reuse audit — 2026-08-19

The existing implementation already contains the image-first persistence route; it must be reused rather than replaced:

- src/Pegasus.Core/ImageIntake/ImageIntakeLifecycle.cs owns the image-only material predicate and registration validation. It currently calls the result a pre-Case Image intake.
- src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs defines ImageIntakeRecord, ImageIntakeReferenceFormat, IRegisterImageIntake, and IImageIntakeQueries. The reference formatter already produces {normalised VRM}-{sequence:00} (for example AB12ABC-01).
- src/Pegasus.Infrastructure/Persistence/ImageIntakeEntities.cs and EfImageIntakeStore.cs persist immutable image-origin records and allocate a per-VRM sequence atomically, without reuse.
- src/Pegasus.Core/ImageIntake/ImageIntakeAutomation.cs already registers an ImageIntake for a confident usable VRM even when candidate matching returns zero or multiple eligible Cases; it currently leaves that result as an awaiting-instruction ImageIntake rather than an Image-initiated Case.
- src/Pegasus.Core/ImageIntake/ImageIntakeCasePairing.cs already performs the reverse exact-registration pairing when an Instruction-initiated Case is accepted.
- src/Pegasus.Core/Cases/CaseContracts.cs and src/Pegasus.Infrastructure/Persistence/EfCaseAcceptanceStore.cs require a real Principal, normal Case type, and formal Case/PO allocation. They must not be weakened or bypassed for image-first work.
- src/Pegasus.Web/Pages/ImageIntake/Index.cshtml(.cs), Details.cshtml(.cs), and Pages/Intake/Details.cshtml(.cs) expose the current Image intake registered / Awaiting instruction surface and must be reconciled to the Image-initiated Case terminology and lifecycle.
- Existing Box custody is formal-Case-root based. The Image-initiated lifecycle must add or reuse the existing custody boundary so retained images are filed under their immutable VRM reference, without inventing a second custody system.

### Governing-document conflicts to amend

The following canonical documents currently contradict the confirmed model by saying image-led work remains pre-Case or by prohibiting an Image Case:

- docs/operator-notes.md image-only/image-initiated wording.
- docs/prd/pegasus-product.md product boundary and reference-allocation language.
- docs/frd/frd-01-case-identity-and-lifecycle.md Case origins, Image-initiated closure/conversion, history, and Case/PO distinction.
- docs/frd/frd-02-intake-and-source-identity.md image registration, group routing, and source-to-Case relationship.
- docs/frd/frd-06-vehicle-and-engineering-evidence.md VRM detection/reading and conflicting-VRM outcome.
- docs/frd/frd-12-operator-experience.md queues, search, permissions, status, and Operations treatment.
- docs/design/README.md, docs/capabilities.md, docs/index.md, and CONTEXT.md terminology/navigation/counts.
- Accepted docs/adr/0013-qdos-alpha-implementation-contract.md: image-led work remains pre-Case. This decision must be superseded by a new ADR; do not edit the accepted ADR in place.

### Confirmed outcome boundaries

- One usable accepted VRM with no unique Instruction-initiated match creates one Image-initiated Case using the existing ImageIntake aggregate/reference owner.
- Conflicting valid VRMs (for example, two vehicles in one group) enter one INTK-007 Unidentified group with a specific conflicting_vrms reason/marker; no VRM reference is fabricated.
- A group member with no plate can follow a readable sibling in the same upload group.
- Image-initiated Cases are searchable, retain original filenames and all image custody, and use the existing staff permissions boundary.
- On later match, the Image-initiated Case is closed/converted as merged or subsumed into the Instruction-initiated Case; both references and the merge history remain visible from the formal Case. Staff may close an unmatched Image-initiated Case with a reason when instructions never arrive.
- Image-initiated references are separate from formal Case/PO, Audit, and Unidentified U<n> references.

files.md is the authoritative file inventory for this reconciliation. Any new implementation path must be added here before it is edited; the plan must explicitly require this update.
