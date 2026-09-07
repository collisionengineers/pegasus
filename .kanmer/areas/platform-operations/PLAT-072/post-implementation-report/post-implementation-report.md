# Post-implementation report — PLAT-072

## Summary

Source frozen on PLAT-072-remove-staff-confirmation, base
baafa29e0f7002b8235aa43bf333f5d9bb172828. Not yet built, tested, reviewed,
merged or deployed. Root owns all heavy verification.

## Changes

Core CaseCompleteness now carries InstructionComplete and ImagesComplete only.
Existing policy and production acceptance/custody callers retain the same
two-fact rule; unused automaticallyDefinitive parameters and stale waiver
comments are gone. StandaloneAuditEvidence.ConfirmedByStaffId is unchanged.

Create loses two obsolete checkboxes/bound properties. Details loses two
posted arguments and hidden inputs. Real completeness controls, authenticated
actor, version, lease, reason, and all handoff/Glass/report actions stay intact.

Existing EF owners no longer read/write the retired properties. Acceptance
command material drops them and advances its existing SchemaVersion from4to5;
there is no legacy fingerprint adapter. Permanent history is retained.

Normal EF migration 20260907221500_RemoveCaseStaffConfirmation drops exactly
four columns (two on Cases, two on IntakeAllocationAttempts). Down restores
their boolean shape with false defaults, not discarded values. Existing
table-level grants remain sufficient; no new permission/schema owner.
The current snapshot loses only four properties. New designer target body
exactly matches that model after line-ending normalization.

All 16 observed current SQL INSERT statements remove each column together
with its matching value; other positional values remain. All affected
constructors/form/entity fixtures use the two-fact contract. Historical
migrations/designers and the three explicit historical-schema seed fixtures
remain unchanged. Both exact migration inventories retain the DOCS-020
permission migration and include the new drop migration.

Focused existing tests now assert complete/missing evidence without review
state, absence of both Create controls with an actual successful post, and
migration Down/Up preserving current Case identity, origin, factual
completeness, workflow/version and history. Existing lease/version/replay
assertions remain. No new fixture framework or package.

## Governing docs

FRD-01 and FRD-12 already retire the staff-review fields. Their factual
completeness requirements are implemented without changing operator-notes.
EPIC-014/current user and root clearance govern this lane. CASE-049 owns
native handoff behavior; this diff deliberately does not absorb it.

## Static checks and attempts

- git -c core.safecrlf=false diff --check: exit0.
- Case-insensitive symbol/caller scan: no retired flags or unused parameter
  remain in production outside migration history. Remaining test names are
  intentional absence/schema checks or historical-schema seeds.
- All meaningful assertion lines outside the focused test changes are
  unchanged; 16 SQL column/value alignments inspected.
- Initial patch application refused overlapping context, writing no files;
  corrected non-overlapping hunks applied. A bulk read was truncated and
  reread per-file; one tool-script syntax attempt ran no command.
- Initial designer/snapshot raw comparison exited1 because CRLF and LF
  differed. Normalized model-body comparison exited0; no model changes were
  made to obtain the pass. This is not runtime EF proof.
- No build/test/capture/cloud command has run in this lane.

## Root verification hand-off

Worktree: .worktrees/plat-072. One locked restore/Release build, then:

Core filter:
```text
FullyQualifiedName~Pegasus.Core.Tests.Cases.AutomaticCaseReadinessTests|FullyQualifiedName~Pegasus.Core.Tests.Cases.CaseDataOperationsTests
```

Integration filter (includes four requested capture states and representative
current SQL/allocation replay callers):
```text
FullyQualifiedName~CaseCreateWebTests|FullyQualifiedName~CaseDataCompletenessPersistenceTests|FullyQualifiedName~CaseAcceptanceReplayTests|FullyQualifiedName=Pegasus.IntegrationTests.IntakePersistenceIntegrationTests.CommittedMigrationCreatesTheSqlServerSchema|FullyQualifiedName=Pegasus.IntegrationTests.CaseWorkflowMigrationTests.CustodyEvidenceOrdinalsAndOperationsMigrateFromPreviousSchemaWithoutIdentityLoss|FullyQualifiedName=Pegasus.IntegrationTests.QdosAllocationRecoveryTests.InterruptedPendingOperationResumesThroughIdempotentAtomicAcceptance|FullyQualifiedName=Pegasus.IntegrationTests.RetainedMailPersistenceTests.ASucceededAllocationAttemptResolvesTheCaseWithoutALinkRow|FullyQualifiedName~RailCountsWebTests|FullyQualifiedName=Pegasus.IntegrationTests.ImageIntakeWebTests.ConfidentReadAutoRegistersAndAutoAssociatesTheUnambiguousCase|FullyQualifiedName=Pegasus.IntegrationTests.CaseDetailsWebTests.HoldingTheEditLeaseRendersEverySectionAndDefersNone|FullyQualifiedName=Pegasus.IntegrationTests.CaseDetailsWebTests.ARefusedCompletenessChangeKeepsUncheckedProposalsBesideTheCurrentValues|FullyQualifiedName=Pegasus.IntegrationTests.TestUiFocusedRenderTests.CaseUnavailableAndErrorStatesRenderThroughRazor
```

Set PEGASUS_TEST_UI_CAPTURE_DIR to artifacts/test-ui-capture and
PEGASUS_TEST_UI_SCOPE to case-create,case-details for that same integration
run; leave PEGASUS_TEST_UI_MODE unset. Then reuse those responses:

```powershell
pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -SkipCapture -Scope case-create,case-details
pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture -Scope case-create,case-details
pwsh -NoProfile -File ./scripts/Test-UiCatalogue.ps1
pwsh -NoProfile -File ./scripts/Test-MigrationGrants.ps1
```

Expected: focused tests pass; no pending-model drift; migration inventory and
four-column absence/preservation checks pass. Snapshot routes:
case-create--default, case-details--default, case-details--conflict,
case-details--unavailable. No manual browser visual pass is claimed.
Further unrelated/broad suites belong to root's single final release check.
Proof comes after independent review and exact merge to board integration dev.
