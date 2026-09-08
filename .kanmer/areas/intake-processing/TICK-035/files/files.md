# Files — TICK-035

## Production change map

| Existing path | Bounded change |
| --- | --- |
| src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosMailRoutePolicy.cs | Generalize/rename this route owner; exact evidenced identities and typed PCH intermediaries; preserve sender/forward consistency and provisional sender helper. No parallel QDOS route. |
| src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosMailClassificationPolicy.cs | Generalize/rename this classification owner; keep QDOS predicates and use evidenced selected-profile current request types for additional principals. Reuse standalone Audit evaluation with actual instruction identity. |
| src/Pegasus.Core/Intake/Classification/MailClassificationContracts.cs | Pass established principal/profile context to the existing classifier contract. |
| src/Pegasus.Core/Intake/InstructionExtractionPolicySelector.cs | Reuse the fifteen profiles; scope real instruction fragments without combining separate document identities into a fabricated profile. |
| src/Pegasus.Core/Intake/ProcessIntake.cs | Replace fixed extraction binding; require route/profile agreement; use selected extraction and classification in the existing assessment/allocation/matching pipeline. |
| src/Pegasus.Core/Intake/IntakeContracts.cs | Only context/port signature changes required for the same existing pipeline. |
| src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosCaseMatchPolicy.cs | Generalize/rename one match-key owner; retain QDOS-specific grammar, preserve full other-principal references from their typed fields. |
| src/Pegasus.Core/Intake/CaseMatching/CaseMatchContracts.cs | Carry provider identity into shared read/write key normalization. |
| src/Pegasus.Core/Intake/CaseMatching/EvaluateIntakeCaseMatch.cs | Adapt calls to generalized key owner; do not redesign the eliminator. |
| src/Pegasus.Infrastructure/Persistence/CaseMatchEntities.cs | Same match-key normalization in current index projector/query owner. |
| src/Pegasus.Infrastructure/Persistence/EfCaseAcceptanceStore.cs | Required match-owner caller adaptation only. |
| src/Pegasus.Infrastructure/Persistence/EfCaseDataStore.cs | Required match-owner caller adaptation only. |
| src/Pegasus.Infrastructure/Persistence/EfCaseWorkspaceStore.cs | Required match-owner caller adaptation only. |
| src/Pegasus.Infrastructure/Persistence/EfLinkedCaseReplacementStore.cs | Required match-owner caller adaptation only. |
| src/Pegasus.Infrastructure/Persistence/EfVehicleWorkflowStore.cs | Required match-owner caller adaptation only. |
| src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs | Rename reference to the same provisional effective-sender owner. |
| src/Pegasus.Infrastructure/DependencyInjection.cs | Replace fixed QDOS compositions with the generalized existing owners; retain all fifteen extraction registrations. Wait for PLAT-028 merge. |
| src/Pegasus.Web/Pages/Administration/Principals/Settings.cshtml.cs | Read activated identity metadata from the one Core owner for the selected customer. Wait for PLAT-028 merge. |
| src/Pegasus.Web/Pages/Administration/Principals/Settings.cshtml | Only metadata label if necessary to correctly show YML's exact address, not a Gmail domain; root owns capture. |
| docs/frd/frd-02-intake-and-source-identity.md | Current route/profile/classification/match behavior and fail-closed boundaries. |
| docs/frd/frd-09-provider-and-intermediary-routes.md | Current evidenced email route activation versus separately credentialed Provider API. |
| docs/current-architecture.md | As-built pipeline composition once wired; no premature live-deployment claim. |

Renamed Core owners stay within existing Intake subdirectories. No new
top-level directory, project, policy framework, package, migration, store,
worker or generic rule editor. Route evidence stays in research/reference;
the active runtime identity list exists exactly once.

## Existing test callers and focused proof

Core: ProcessIntakeTests, DefinitiveIntakeCaseTypeTests,
Intake/Qdos/{QdosMailRoutePolicyTests,QdosMailClassificationPolicyTests,
QdosCaseMatchPolicyTests,QdosInstructionExtractionPolicyTests},
CaseMatching/EvaluateIntakeCaseMatchTests, ReconcileUnidentifiedDestinationsTests,
and InstructionExtractionPolicySelector tests.

Integration: Top15InstructionCorpusTests (reuse hash-bound originals and
expected fields); CaseMatchIntegrationTests; InlineForwardedMailRouteTests;
IntakeWebTestSupport; ProductionCompositionTests; ProviderDomainReferenceIntegrationTests;
QdosAllocationRecoveryTests; QdosEmailCohortTests; QdosExtractionCoverageTests;
QdosIntakeWebTests; QdosMappingExtractionTests;
QdosTriageCaseAssociationIntegrationTests; QdosTriageIntegrationTests;
RetainedMailPersistenceTests; TriageFromIntakeIntegrationTests;
MultiFormatGenuineCorpusWebTests. Mechanical constructor/type rename updates
are allowed; preserve unrelated assertions and do not run all suites merely
because they compile against the changed contract. Add focused actual
destination tests to existing intake integration fixtures rather than a new host.

After PLAT-028 merge, OrganizationDirectoryWebTests consumes activated route
metadata and follows the same single owner. Any Settings HTML change carries
its existing snapshot/catalogue entry under root's scoped capture.

## Preserve / coordinate

Do not edit corpus, provider source originals, operator-notes, migrations,
Worker grants, MailboxIntake, Triage link workflow, engineer workflow, report
stores or PLAT-028's unmerged worktree. INTK-061's merged durability is the
base, not scope to reopen. Root owns all heavy verification and live actions.

## Root-authorized acceptance-caller correction

- src/Pegasus.Infrastructure/Persistence/CaseDataSnapshotFactory.cs: replace
  QDOS display-name joins with current canonical case-field identity; preserve
  typed values, exact source candidates, conflicts, uniqueness and origin hash.
- src/Pegasus.Infrastructure/Persistence/CaseDataFieldNames.cs moved to
  src/Pegasus.Core/Cases/CaseDataFieldNames.cs: move the existing vocabulary,
  not a second copy. Existing consumers receive using-only adaptation if needed.
- src/Pegasus.Core/Intake/IntakeContracts.cs: pure InstructionReviewField
  canonical case-field method; no new JSON member or schema.
- src/Pegasus.Core/Pegasus.Core.csproj: allow the existing IntegrationTests
  assembly to reuse the internal OCR-result read mapper; no public test API.
- tests/Pegasus.IntegrationTests/Top15InstructionCorpusTests.cs: exact original
  MP scan and supplied hash-bound OCR through existing read mapping.
- tests/Pegasus.IntegrationTests/QdosAllocationRecoveryTests.cs: identify the
  principal/delivery/allocation failure and prove the actual accepted source
  facts/provenance for the same four original emails.
- tests/Pegasus.Core.Tests/Intake/InstructionReviewFieldTests.cs: bounded pure
  canonical mapping tests using existing field names, no invented domain data.

No schema, stored JSON migration, alternative provenance, provider call or
OCR-provider implementation change is authorized by this correction.


## Second-attempt and independent-review correction map

Additional changed production file:
- src/Pegasus.Infrastructure/Intake/MimeKitPdfPigOpenXmlIntakeSourceReader.cs:
  replace existing Coverage geometry with installed PdfPig visible bounds,
  normalization and intersection; no threshold or reader-policy duplication.
Existing changed files:
- Core/Intake/Classification/PrincipalMailClassificationPolicy.cs: one current
  content boundary for QDOS as well as generic classification/Audit evidence.
- Infrastructure/Persistence/CaseDataSnapshotFactory.cs: exact selected typed
  phone source determines conflict; unused alternative cannot veto it.
Focused tests:
- tests/Pegasus.IntegrationTests/StructuredIntakeSourceReaderTests.cs: small
  non-domain PdfPig geometry probes for quarter-turns and outside-crop bounds.
- tests/Pegasus.IntegrationTests/CaseDataCompletenessPersistenceTests.cs:
  direct existing factory caller tests for typed PCH alternative selection,
  selected conflict and duplicate equal-source rejection; no new harness.
- tests/Pegasus.Core.Tests/Intake/Qdos/QdosMailClassificationPolicyTests.cs:
  proved original vs arbitrary nested/chaser boundaries, preserving body Triage.
- Existing Top15InstructionCorpusTests same MP scan/OCR path unchanged;
  QdosAllocationRecoveryTests pins inspected4/18/0/0 asset counts and readiness.
Read-only coordination: current dev PdfOcrQualification/Tests (TICK041) are
Type3-only, no scan geometry and no reader-file overlap; no edit to that owner.
