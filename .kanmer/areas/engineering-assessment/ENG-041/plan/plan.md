# Plan — ENG-041 post-merge custody correction

## Objective

Restore the normal Glass's callback to one Completed session and one imported
Draft, without consuming the Engineer's authority while retaining its source
documents, while keeping report snapshots safe against concurrent source changes.

## Starting state

Status Implementing, retained author branch ENG-041-glass-recovery and worktree
.worktrees/eng-041. Root prepares the resume/base refresh from accepted dev
19e6f523bf6760cab39104b4dca3674b0ac8a512; validate the exact resume packet before
any Git/source action. Original author head is
8bbceb4fd190ae80a8b656540fd0ae5973f49895; merged PR683 is
baafa29e0f7002b8235aa43bf333f5d9bb172828.

Evidence: files/files.md@1d4c177e23c8d8b9;
proof/proof.md@73d3327f6364834c;
post-implementation-report/post-implementation-report.md@f437cfd8a750979b;
prior plan/plan.md@f387a6a84dfa1f21. The prior implementation and review rounds
remain historical evidence, not permission to edit their eighteen files again.

Exact merged verification: locked restore/build PASS, Core 56 PASS, Integration
157 PASS / 2 FAIL. Both failures are actual Glass callback journeys expecting
Completed but receiving AwaitingImport. All prior failures and passes remain
in the existing proof/report; this plan is not new verification evidence.

Confirmed cause: DOCS-020's source-confirmation helper calls
CaseMutationGuard.Complete for each confirmed XML/PDF, incrementing the Case
version and clearing its live lease even when no report exists. Glass correctly
retains its original launch version/token, so the subsequent import refuses.
The gateway, guard and failing tests are unchanged between author and merge.

EPIC-014 context binds scope and root-only heavy verification. Live sources
resolution declared no external sources. This is existing-source inspection,
not a cloud/provider investigation.

## Governing docs

Meets linked FRD-06: an Engineer-owned Glass estimate lands as a source-labelled
Draft, with custody and replay retained and no duplicate external calculation.
Meets FRD-11 immutable/current report inputs and atomic stale-generation rules.
FRD-11 stays unchanged: root confirmed its current wording requires source
freshness/invalidation, not Case-version advancement or clearing a staff lease.
Under the current v1 remediation authority, clarify only the already linked
FRD-06 Glass's section: own returned artifact retention preserves still-valid
Engineer authority; genuine staff edits or expired leases still defer import.
Record the source-recheck mechanism in existing code comments and the ticket
report, not a new governing document. No ADR is needed. Preserve operator notes.

## Required changes

1. In EfCaseArtifactCustody.RecordConfirmedSourceChangeAsync, remove the
   workflow load and CaseMutationGuard.Complete. Keep SourceDocumentChangedAsync
   and SaveChanges in the same existing serializable confirmation transaction;
   both immediate confirmation and pending reconciliation use this one helper.
   Preserve exact replay, atomic stale history, pending/retry semantics, and
   generated-artifact operation-identity exemption. Do not modify explicit
   staff document addition/removal, which remain real Case mutations.
2. Extract the current confirmed-document query and its existing two mappings
   from EfAssessmentReportProjectionSource to internal static helpers in that
   class. Retain the existing ConfirmedDocumentRow, exposing it internally only
   if required by these two callers; add no DTO/interface/store. Preserve the
   exact occurrence/version join, matching DocumentId, CaseId scope, IsCurrent,
   not-removed, Confirmed, actual GeneratedCaseArtifact operation exclusion, and
   occurrence-ordinal ordering. Projection uses the same helpers.
3. FreezeAsync uses that query in its existing serializable transaction after
   the existing authorization/version guards and before readiness, generation
   lookup/reuse or writes. Compare the complete mapped ordered Sources and the
   full occurrence-keyed ConfirmedImageSources dictionary with the captured
   inputs. Both contain all confirmed rows in the production source, not only
   prepared photos. Compare count, occurrence membership and existing mapped
   identities/metadata: document/version IDs, logical version, filename, media
   type, content length, hash, Box file/version, currentness and custody status.
   Dictionary order is not identity; compare by key and record value. A mismatch
   throws an ordinary content-safe InvalidOperationException instructing retry,
   like the existing signatory-race refusal, and writes no generation/artifact.
   Do not manufacture a CaseVersionConflict when the Case version did not change.
4. Preserve outside Case-version guards for staff edits, current signatory
   recheck, image preparation/version guards and genuine lost/foreign/expired
   lease refusal. Never refresh provider version/token, bypass a guard,
   automatically acquire a lease, special-case Glass source labels, or suppress
   source invalidation. Report rendering and bytes stay outside the transaction.

## Expected files

| Action | Repo-root-relative path | Responsibility |
| --- | --- | --- |
| Modify | `src/Pegasus.Infrastructure/Custody/EfCaseArtifactCustody.cs` | Preserve staff authority during atomic source confirmation. |
| Modify | `src/Pegasus.Infrastructure/Persistence/EfAssessmentReportProjectionSource.cs` | One shared confirmed-source query and mappings. |
| Modify | `src/Pegasus.Infrastructure/Persistence/EfCaseReportGenerationStore.cs` | Transaction-bound full source-census refusal before freeze. |
| Modify | `tests/Pegasus.IntegrationTests/GlassRepairEstimateCallbackWebTests.cs` | Actual callback Completed/import/custody/replay assertions. |
| Modify | `tests/Pegasus.IntegrationTests/CaseArtifactCustodyRecoveryTests.cs` | Immediate/recovered custody and replay preserve live authority. |
| Modify | `tests/Pegasus.IntegrationTests/Reports/CaseReportGenerationPersistenceTests.cs` | Real-shaped source fixture and focused source-race/invalidation evidence. |
| Modify | `docs/frd/frd-06-vehicle-and-engineering-evidence.md` | Existing Glass's authority/retention section only. |

## Do not modify

- `src/Pegasus.Infrastructure/Persistence/EfDocumentCustodyStore.cs`
- `src/Pegasus.Infrastructure/Persistence/CaseMutationGuard.cs`
- `src/Pegasus.Infrastructure/Glass/GlassRepairEstimateGateway.cs`
- `src/Pegasus.Infrastructure/Persistence/EfRepairSpecificationStore.cs`
- `src/Pegasus.Web/Pages/Cases/**`
- `src/Pegasus.Web/Presentation/**`
- `src/Pegasus.Core/**`
- `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md`
- `docs/operator-notes.md`
- `docs/design/test-ui/**`

## Constraints

No new dependency, schema, grants, runtime, provider API, source identity,
framework, generic comparison layer, UI change or generated snapshot edit.
Use existing fixture documents and identities; no fabricated domain corpus.
Root-only heavy checks. Preserve the exact-merge FAIL proof and both TRXs.

Current scope removes prior Case Details/assessment edit ownership. Live
CASE-049 files/files.md@4689efb462748ddf has no overlap with these seven
current correction files. FRD-11 and CASE-049 native access text stay untouched.
DOCS-020 is Done and its claim is released.

## Ordered steps

1. After root approval, validate/reuse the fresh resume packet and exact retained
   branch/worktree, integrate the root-approved accepted base without rewriting
   earlier commits, and refresh the correction checklist. If ownership or merge
   differs, stop before source changes.
2. Extract/reuse the source query/mappings and add the transaction-bound
   complete-census check, then remove only automatic custody's version/lease
   mutation while preserving its atomic invalidation and replay.
3. Update the existing report SQL fixture to capture all three seeded confirmed
   rows through the production query/mappings, not its old PDF-only Sources and
   photo-only readiness dictionary. Capture the census before race mutation;
   do not make a supposedly stale fixture reread current sources at assertion
   time. Keep explicit source/hash/currentness assertions independently of the
   reused mapping, and preserve the existing stale staff-version negative.
4. Add/adjust the focused assertions below and clarify only the FRD-06 Glass's
   authority paragraph. Freeze source and return exact filters/files to root; no author
   build/test. After root evidence, the author updates the report and follows
   the approved new-PR review route for this post-merge correction.

## Acceptance checks

- Existing actual callback tests
  TheProvidersReturnLandsTheDraftKeepsBothDocumentsAndCompletesTheSession and
  TheSameReturnDeliveredTwiceRecordsNothingASecondTime pass unchanged in meaning:
  Completed, one Draft, both retained documents, no duplicate session/import.
- Actual immediate and recovered automatic custody preserve existing Case
  version and exact live holder/token/expiry; replay preserves the same document
  identities and does not invalidate or consume authority again. Subsequent
  authorized import still consumes authority through its normal Case mutation.
- Retaining a non-report source invalidates any current report and prepared
  delivery atomically; generated report/fee-note outputs do not invalidate their
  own generation. Web and Worker runtime-role checks still exercise the real
  helper. Replace their arbitrary version-2 expectation with exact unchanged
  version/live-lease plus meaningful staleness/history assertions.
- Deterministic pre-freeze races add or remove a confirmed source between captured
  inputs and transaction without relying on a changed Case version; no generation
  or artifact is written. Current-version/current-lease still pass the outside
  guard, proving refusal comes from source currentness.
- Bounded theory cases cover same-count occurrence replacement and changed
  source identity/hash/filename/media/length/Box identity/current eligibility;
  no subset-only comparison can pass. Unchanged complete sources pass.
- Retain existing readiness, source addition/removal, stale staff-version,
  signatory-race and generated-output exemption assertions. Genuine Case edits,
  expired/foreign leases, archive/terminal refusal and provider replay are not
  relaxed. No transport/network provider write is permitted in local evidence.

## Commands

Author: read-only source/caller checks and `git diff --check` only.
Root at the retained correction worktree, PowerShell 7, performs the build
needed for focused checks once and records exact switches/results. Proposed
focused Integration filter (author confirms new methods remain in these classes):

`FullyQualifiedName~GlassRepairEstimateCallbackWebTests.TheProvidersReturnLandsTheDraftKeepsBothDocumentsAndCompletesTheSession|FullyQualifiedName~GlassRepairEstimateCallbackWebTests.TheSameReturnDeliveredTwiceRecordsNothingASecondTime|FullyQualifiedName~CaseArtifactCustodyRecoveryTests.FailedWriteLeavesOnePendingIntentAndReplayUsesTheSameVersionIdentity|FullyQualifiedName~CaseArtifactCustodyRecoveryTests.AutomaticCustodyPreservesLiveCaseAuthority|FullyQualifiedName~Pegasus.IntegrationTests.Reports.CaseReportGenerationPersistenceTests`

Use the existing Integration project command with --configuration Release,
--no-build after root's successful build, and a uniquely named correction TRX.
No broad rerun of the already passing 157 cases solely for this fix. New source
needs independent exact-head review and focused exact-follow-up merge proof.
The original merge proof's missing three default/conflict/unavailable capture
inputs remain a separate root-owned evidence obligation, using the actual
PEGASUS_TEST_UI_CAPTURE_DIR variable; no UI change/recapture in this author batch.

## Failure and deviation rules

Do not weaken Completed, freshness, source identity or authority assertions to
pass. A missing mapped source input, permission difference, additional caller,
overlapping active file, package/schema need or scope expansion is a stop and
report, not a bypass. Retain every failed attempt and do not infer deployed
status from an integrated correction.

## Stop condition

Root approved plan f0aa4318d6dca111 and files 1d4c177e23c8d8b9 in full and
assigned /root/principal_delivery_audit as correction author. Implement these
seven files only, then freeze for root's focused verification. No author
build/test/provider/cloud call, self-review, merge or deployment. Root later
authorizes report/commit/push and a NEW dev-targeting follow-up PR because
PR683 is already merged; the previous review attestation remains historical.
