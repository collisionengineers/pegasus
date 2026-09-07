# Post-implementation report — PLAT-072

## Summary

Source frozen on PLAT-072-remove-staff-confirmation, base
baafa29e0f7002b8235aa43bf333f5d9bb172828. Root's focused verification
passed; independent review and exact merged proof remain. Not merged or
deployed. No application source changed after root verification began.

## Changes

Core CaseCompleteness now carries InstructionComplete and ImagesComplete only.
Existing policy and production acceptance/custody callers retain the same
two-fact rule; unused automaticallyDefinitive parameters and stale waiver
comments are gone. StandaloneAuditEvidence.ConfirmedByStaffId is unchanged.

Create loses two obsolete checkboxes/bound properties. Details loses two
posted arguments and hidden inputs. Real completeness controls, authenticated
actor, version, lease, reason, and all handoff/Glass/report actions stay intact.

Existing EF owners no longer read/write the retired properties. Acceptance
command material drops them and advances its existing SchemaVersion from 4 to 5;
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
- The author ran no build/test/capture/cloud commands; root executed the
  bounded verification below as sole heavy verifier.

## Executed verification — root, Windows PowerShell 7

All commands ran in .worktrees/plat-072. Locked restore and Release solution
build passed (133.38 seconds, zero warnings/errors):

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter 'FullyQualifiedName~Pegasus.Core.Tests.Cases.AutomaticCaseReadinessTests|FullyQualifiedName~Pegasus.Core.Tests.Cases.CaseDataOperationsTests' --logger 'trx;LogFileName=plat-072-core.trx' --results-directory ./artifacts/verification
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter 'FullyQualifiedName~CaseCreateWebTests|FullyQualifiedName~CaseDataCompletenessPersistenceTests|FullyQualifiedName~CaseAcceptanceReplayTests|FullyQualifiedName=Pegasus.IntegrationTests.IntakePersistenceIntegrationTests.CommittedMigrationCreatesTheSqlServerSchema|FullyQualifiedName=Pegasus.IntegrationTests.CaseWorkflowMigrationTests.CustodyEvidenceOrdinalsAndOperationsMigrateFromPreviousSchemaWithoutIdentityLoss|FullyQualifiedName=Pegasus.IntegrationTests.QdosAllocationRecoveryTests.InterruptedPendingOperationResumesThroughIdempotentAtomicAcceptance|FullyQualifiedName=Pegasus.IntegrationTests.RetainedMailPersistenceTests.ASucceededAllocationAttemptResolvesTheCaseWithoutALinkRow|FullyQualifiedName~RailCountsWebTests|FullyQualifiedName=Pegasus.IntegrationTests.ImageIntakeWebTests.ConfidentReadAutoRegistersAndAutoAssociatesTheUnambiguousCase|FullyQualifiedName=Pegasus.IntegrationTests.CaseDetailsWebTests.HoldingTheEditLeaseRendersEverySectionAndDefersNone|FullyQualifiedName=Pegasus.IntegrationTests.CaseDetailsWebTests.ARefusedCompletenessChangeKeepsUncheckedProposalsBesideTheCurrentValues|FullyQualifiedName=Pegasus.IntegrationTests.TestUiFocusedRenderTests.CaseUnavailableAndErrorStatesRenderThroughRazor' --logger 'trx;LogFileName=plat-072-integration.trx' --results-directory ./artifacts/verification
```

Core: 24/24 PASS, no skipped tests. Integration: 35/35 PASS, no skipped
tests, 119 seconds. Real Create posts and migration Down/Up assertions
passed, including current identity, factual completeness and history.

For the integration run, PEGASUS_TEST_UI_CAPTURE_DIR was the absolute
worktree artifacts/test-ui-capture path; PEGASUS_TEST_UI_SCOPE was
case-create,case-details; PEGASUS_TEST_UI_MODE was unset.

Initial scoped Update-TestUiSnapshots -SkipCapture attempt FAILED exit 1
(1 PASS/1 FAIL): the chosen cohort supplied no Review/edit-lease default
capture. No product assertion failed. Root reused the known existing route,
with capture scope case-details and the same directory/mode:

```powershell
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter 'FullyQualifiedName=Pegasus.IntegrationTests.CaseDetailsWebTests.CustodyRetryAndExportRoutesBindAntiforgeryHumanActorLeaseWorkflowVersionReasonAndKey' --logger 'trx;LogFileName=plat-072-review-capture.trx' --results-directory ./artifacts/verification
pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -SkipCapture -Scope 'case-create,case-details'
pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture -Scope 'case-create,case-details'
pwsh -NoProfile -File ./scripts/Test-UiCatalogue.ps1
pwsh -NoProfile -File ./scripts/Test-MigrationGrants.ps1
git diff --check
```

Correction capture: 1/1 PASS, no skipped tests, 33 seconds. Snapshot update
2/2 PASS and verification 2/2 PASS. Catalogue: 60 routed sources, 67
prototypes, zero broken references. Migration-grant check: 102 files PASS.
Diff check PASS. No rerun of the 35 passing integration cases.

Four states were captured through actual Razor responses: Create default,
Details default/conflict/unavailable. Only Create default and Details
conflict change tracked bytes; index/default/unavailable normalize unchanged.
The conflict snapshot now comes from the existing refused-completeness
scenario, preserving unchecked proposed values. No manual visual pass is
claimed.

Author independently read the three TRX counters and hashes (all exit 0).
Artifacts remain under artifacts/verification:

| TRX | UTC start / finish | SHA256 |
| --- | --- | --- |
| plat-072-core.trx | 23:44:37.767 / 23:44:39.597 | 0953D97040D7340F01A3139A5F742CAD52DC2CC381B18C0C54E744D03D67BBB0 |
| plat-072-integration.trx | 23:44:41.294 / 23:46:42.932 | 15EF71924555698EF79B51BCE1E16EC0FA135E8C897696036B3E71B0F68A1E65 |
| plat-072-review-capture.trx | 23:48:45.267 / 23:49:21.098 | 007BF5269FB3E1A880B4EB9A96CA056BAFAA3FECFDCA82849FD9827637167FA0 |

All instants above are 2026-09-07 UTC. scratch/verify version
191ade7f170d1bbd retains root's attempts, including initial missing capture.

## Changed files

The Core/EF/Web files below remove the retired fields and callers; migration,
designer and snapshot establish the four-column drop. Existing test files
reconcile constructors, posted fields and aligned SQL fixtures; focused
readiness/Create/persistence tests add the assertions described above. The
two snapshot files retain the actual affected rendered responses.

```text
docs/design/test-ui/pages/case-create--default.html
docs/design/test-ui/pages/case-details--conflict.html
src/Pegasus.Core/Cases/CaseContracts.cs
src/Pegasus.Core/Cases/CaseDataOperations.cs
src/Pegasus.Core/Intake/AcceptIntake.cs
src/Pegasus.Core/Intake/IntakeAllocation.cs
src/Pegasus.Infrastructure/Persistence/EfCaseAcceptanceStore.cs
src/Pegasus.Infrastructure/Persistence/EfCaseDataStore.cs
src/Pegasus.Infrastructure/Persistence/EfCaseWorkspaceStore.cs
src/Pegasus.Infrastructure/Persistence/EfIntakeAllocationStore.cs
src/Pegasus.Infrastructure/Persistence/EfLinkedCaseReplacementStore.cs
src/Pegasus.Infrastructure/Persistence/EfQueuedCustodyProcessor.cs
src/Pegasus.Infrastructure/Persistence/IntakeAllocationEntities.cs
src/Pegasus.Infrastructure/Persistence/Migrations/20260907221500_RemoveCaseStaffConfirmation.Designer.cs
src/Pegasus.Infrastructure/Persistence/Migrations/20260907221500_RemoveCaseStaffConfirmation.cs
src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs
src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs
src/Pegasus.Web/Pages/Cases/Create.cshtml
src/Pegasus.Web/Pages/Cases/Create.cshtml.cs
src/Pegasus.Web/Pages/Cases/Details.cshtml.cs
src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkflow.cshtml
tests/Pegasus.Core.Tests/AiWork/AiWorkTests.cs
tests/Pegasus.Core.Tests/Cases/AutomaticCaseReadinessTests.cs
tests/Pegasus.Core.Tests/Cases/CaseDataOperationsTests.cs
tests/Pegasus.Core.Tests/Cases/ImmediateExternalPublicationTests.cs
tests/Pegasus.Core.Tests/Reports/CaseReportDeliveryPreparationTests.cs
tests/Pegasus.IntegrationTests/AssessmentPersistenceIntegrationTests.cs
tests/Pegasus.IntegrationTests/AssessmentVehiclePrefillWebTests.cs
tests/Pegasus.IntegrationTests/AssessmentWorkspaceTestData.cs
tests/Pegasus.IntegrationTests/AutomaticVehicleLookupTests.cs
tests/Pegasus.IntegrationTests/AutomationDocumentIngressTests.cs
tests/Pegasus.IntegrationTests/AutomationMcpTestSupport.cs
tests/Pegasus.IntegrationTests/Browser/LayoutIntegrityTests.cs
tests/Pegasus.IntegrationTests/Browser/OperatorJourneyTests.cs
tests/Pegasus.IntegrationTests/CaseAcceptanceReplayTests.cs
tests/Pegasus.IntegrationTests/CaseCreateWebTests.cs
tests/Pegasus.IntegrationTests/CaseDataCompletenessPersistenceTests.cs
tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs
tests/Pegasus.IntegrationTests/CaseMatchIntegrationTests.cs
tests/Pegasus.IntegrationTests/CaseTaskArchivePersistenceTests.cs
tests/Pegasus.IntegrationTests/CaseWorkflowMigrationTests.cs
tests/Pegasus.IntegrationTests/CaseWorkflowPersistenceTests.cs
tests/Pegasus.IntegrationTests/ConcurrencyTokenPersistenceTests.cs
tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs
tests/Pegasus.IntegrationTests/DueChaserSweepPersistenceTests.cs
tests/Pegasus.IntegrationTests/GlassRepairEstimateCallbackWebTests.cs
tests/Pegasus.IntegrationTests/ImageCaseCustodyIntegrationTests.cs
tests/Pegasus.IntegrationTests/ImageIntakePersistenceTests.cs
tests/Pegasus.IntegrationTests/ImageIntakeWebTests.cs
tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs
tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs
tests/Pegasus.IntegrationTests/OrganizationAdministrationPersistenceTests.cs
tests/Pegasus.IntegrationTests/ProviderApiCaseDataSnapshotPersistenceTests.cs
tests/Pegasus.IntegrationTests/ProviderApiSubmissionTests.cs
tests/Pegasus.IntegrationTests/ProviderInspectionModeAcceptanceTests.cs
tests/Pegasus.IntegrationTests/QdosAllocationRecoveryTests.cs
tests/Pegasus.IntegrationTests/QdosTriageIntegrationTests.cs
tests/Pegasus.IntegrationTests/RailCountsWebTests.cs
tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs
tests/Pegasus.IntegrationTests/SendToAiIntegrationTests.cs
tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs
tests/Pegasus.IntegrationTests/UnidentifiedReconciliationTests.cs
tests/Pegasus.IntegrationTests/VehicleLookupBackfillTests.cs
tests/Pegasus.IntegrationTests/VehicleLookupGapFillTests.cs
tests/Pegasus.IntegrationTests/VehicleWorkflowTerminalTests.cs
```

## Remaining delivery work

Independent review owns PR disposition. Exact merged verification belongs on
the configured integration branch dev and should reuse these bounded filters
and immutable input comparisons rather than repeat unrelated suites. The
single final converged release gate/CI remains owed; root authorized the
[skip ci] implementation commit, which never bypasses a required check.
No cloud write, production migration, deployed claim, or CASE-049 handoff
behavior is included.
