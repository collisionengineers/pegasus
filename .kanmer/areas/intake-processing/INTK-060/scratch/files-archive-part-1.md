# Archived original files document — part 1 of 3

Original ticket document: `files/files.md`
Original SHA-256: `4e9a7be093f1f8d708a0264ca206a10017dd99e7b807b78524fec570dcbd1058`
Character range: 0–33999 of 100662
Reconstruction: concatenate the payload sections from parts 1, 2 and 3 in order.

## Payload

# File ownership

The complete authoritative file manifest is supplied in pegasus_pack/astra_output/v1_implementation_plans/registers/file-ownership.csv and .json. Exact file then deepest prefix wins; ties are defects. Stream C may edit only its assigned files. A authors all Foundation files and explicit contract-only pre-F exceptions; B/C domain behavior remains with B/C.

Context: COORDINATION.md supplies common commit topology, old PR preservation and three-PR stop. SHARED-CONTRACTS.md resolves B/C request aliases. handoffs/A-foundation-requirements.json, B-foundation-requirements.json and C-foundation-requirements.json supply minimal shapes. Stream plan below carries exact existing owners, production callers, tests, proposed paths and residual acceptance:

# Stream C: intake, directories and shared operator surfaces

## Authority and boundary

This stream implements intake correctness, source-provenance, the top-15
principal instruction profiles, third-party report extraction, current
principal/directory administration, pre-case Image Intake and Triage, Inbox,
Search, Work Centre, the application shell and shared Web assets. It follows
[the final decisions](../DECISIONS.md), the frozen
[shared contracts](../SHARED-CONTRACTS.md), and the execution order and file
ownership in [coordination](../COORDINATION.md).

Stream A alone owns global EF model configuration, migrations, the model
snapshot, composition/DI, shared test support, Graph mail runtime, storage and
MCP. Stream B owns Case pages, Core engineering decisions, report generation
and delivery, and Glass's. Stream C owns its Core policies, adapter/store method
implementations and the C-owned Razor surfaces. C has single ownership of the
outer shell, navigation and shared CSS/JS/icon assets; Stream B owns Case-only
partials and Case-specific assets. The exact requests that C needs from A and
the contracts C exposes to B are in
[C foundation requirements](../handoffs/C-foundation-requirements.json).

The implementation baseline is commit
`3284f93fc3ea9fd3bbbea9405ec92dc7818378f2`. Before editing, re-resolve every
anchor against the final integration base and stop on a material contract or
ownership change. Reuse these production paths before adding anything:

- `ProcessIntake`, `DurableIntake`, `ReconcileUnidentifiedDestinations`,
  `IIntakeSourceReader`, `IInstructionExtractionPolicy`,
  `InstructionFieldExtraction`, `QdosMailRoutePolicy`,
  `QdosMailClassificationPolicy` and `QdosInstructionExtractionPolicy`;
- `MimeKitPdfPigOpenXmlIntakeSourceReader`, `IIntakeReceiptStore`,
  `IIntakeMutationStore`, `IUnidentifiedStore`, `IImageIntakeStore`,
  `IProviderReferenceCatalog`, `IOrganizationAdministrationQueries` and
  `IOrganizationAdministrationStore`;
- `Ext18InspectionAddressPolicy`, `InspectionAddressResolutionPolicy`,
  `InspectionAddressResolutionStore` and `ProviderInspectionModePolicy`;
- the existing `/`, `/Inbox`, `/Search`, `/Triage`, `/Unidentified`,
  `/VehicleImages/{id:guid}`, `/Administration/Organizations` and
  `/Administration/Principals` Razor Pages; and
- `_Layout.cshtml`, `_ShellDialogs.cshtml`, `_EvidenceViewer.cshtml`,
  `_Provenance.cshtml`, `_StatusChip.cshtml`, `site.css`, `site.js` and the
  existing Lucide sprite.

Do not add a generic workflow engine, rules-builder UI, duplicate principal
list, separate case-policy owner, new top-level project, a second OCR vendor or
OCR runtime, or prerelease compatibility path. The one approved OCR boundary is
Azure Document Intelligence `prebuilt-layout` through the existing Worker and
external-work conventions. One-off customers and spreadsheet-driven
recipient/package/chase/garage procedures are deferred. Location choices,
address suggestions, all top-15 profiles and the Pegasus-owned report workflow
remain included; EVA is an optional downstream action.

## Evidence and extraction invariants

- The immutable local corpus is evidence. Never edit, rename, deduplicate in
  place, upload or publish an original. A test manifest records occurrence,
  hash and relationship even when execution deduplicates identical bytes.
- All 81 instruction originals referenced by the 15 existing method files, all
  29 report PDFs in the third-party inventory, and all 14 EVA source workbooks
  are locally available and hash-verifiable. The EVA report's `evacases.xlsx`
  is the matching local `principal-and-repairer-info/every_eva_case.xlsx`.
- The pack-root `providers-worked-on.xlsx` matches the EVA report hash; the
  pinned source snapshot contains a different version. Tests and seeds use the
  explicitly hashed pack-root source and never silently substitute the copy.
- The EVA planning evidence is the
  [reassessment](../../../more_docs/eva_data_export/EVA_Pegasus_Reassessment.md),
  [aggregate findings](../../../more_docs/eva_data_export/Aggregate_findings.json),
  [source inventory](../../../more_docs/eva_data_export/Source_inventory.csv),
  [candidate workflow rules](../../../more_docs/eva_data_export/Candidate_workflow_rules.csv)
  and [derived-date review](../../../more_docs/eva_data_export/Derived_date_review.csv).
  The workflow rows are historical candidates and the date rows are review
  evidence; neither authorizes automation or rewriting the fresh target data.
- The Box-linked E01-E28 originals have no local filename/hash manifest. They
  may explain a proposed rule but cannot supply executable proof, a fixture, an
  activation decision or a claimed pass. A rule supported only by E01-E28 stays
  inactive until the exact original and hash are available.
- Keep raw source text and the smallest useful layout locator with every
  candidate: source/asset hash, occurrence, document role, page, table/cell or
  PDF form-field/region when present, source label, raw value and parser/policy
  version. Normalization never destroys the source value.
- Principal, document issuer, transport sender, effective sender,
  intermediary, claimant, repairer, storage business, third-party engineer,
  report addressee and outgoing recipient remain separate roles.
- Bounded label/cell/region rules may normalize punctuation and whitespace.
  They do not use a confidence score, arbitrary priority or first-match winner.
  Multiple supported interpretations are `Ambiguous`; absent evidence is
  unavailable. Unknown material remains retained for staff review.
- Do not guess make/model from token position, VRM, filename or a short make
  list. Preserve the full vehicle description; split only when labels/layout
  or a separately accepted lookup proves the parts.
- A report deadline, email arrival, enclosing forward or today's date is not
  an instruction or inspection date. Unknown VAT is not `No`. A footer address
  is not an inspection location. A repairer address does not prove a physical
  inspection.
- Extracted candidates never overwrite confirmed staff/Engineer facts.
  Conflicts remain visible and source-linked. Third-party outcomes and amounts
  remain third-party evidence until the existing Engineer-owned command accepts
  them.

## Model allocation

Fable 5.1 orchestrates the stream and owns sequencing, evidence retention and
handoffs; it does not implement a delegated slice. Use Opus 5 for C01-C05 and
C07, where reconciliation, structured extraction, role boundaries and pre-case
identity are correctness-sensitive. Use Sonnet 5 for C06 and C08 after their
contracts are frozen. Do not ask a second model to edit the same slice. Only
after C01-C08 are integrated and their focused checks pass, start a fresh Fable
5.1 context for the whole-stream review in C09.

## Exact repository file map

Paths are repository-root-relative. `Existing` means extend the named file on
the integration base. `Proposed` is the exact new file to create if the
corresponding behavior is still absent after rebasing. C never edits the A/B
handoff files listed here.

### C01 files

**Existing C files:**

- `src/Pegasus.Core/Intake/DurableIntake.cs`
- `src/Pegasus.Core/Intake/ProcessIntake.cs`
- `src/Pegasus.Core/Intake/CaseMatching/EvaluateIntakeCaseMatch.cs`
- `src/Pegasus.Core/Intake/IntakeContracts.cs`
- `src/Pegasus.Core/Intake/ReconcileUnidentifiedDestinations.cs`
- `src/Pegasus.Core/Intake/Unidentified/UnidentifiedContracts.cs`
- `src/Pegasus.Infrastructure/Persistence/EfUnidentifiedStore.cs`
- `src/Pegasus.Web/Pages/Intake/Details.cshtml.cs`
- `tests/Pegasus.Core.Tests/Intake/ReconcileUnidentifiedDestinationsTests.cs`
- `tests/Pegasus.Core.Tests/Intake/CaseMatching/EvaluateIntakeCaseMatchTests.cs`
- `tests/Pegasus.IntegrationTests/UnidentifiedReconciliationTests.cs`
- `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs`
- `tests/Pegasus.IntegrationTests/ProviderApiSubmissionTests.cs`

**A-owned read-only dependencies:**
`src/Pegasus.Infrastructure/Persistence/UnidentifiedEntities.cs` and
`src/Pegasus.Worker/IntakeFunctions.cs`. C supplies the field and caller
inventory; A performs entity/model/migration/grant/DI/host-entrypoint edits.

**Proposed C files:**

- `src/Pegasus.Core/Intake/AnalyzeRetainedInstruction.cs`
- `src/Pegasus.Infrastructure/Persistence/EfRetainedInstructionAnalysisStore.cs`
- `tests/Pegasus.Core.Tests/Intake/AnalyzeRetainedInstructionTests.cs`
- `tests/Pegasus.IntegrationTests/RetainedInstructionAnalysisTests.cs`
- `tests/Pegasus.IntegrationTests/PrincipalSourceManifestTests.cs`, which
  validates the resolved local hash manifest and reports unavailable sources
  without embedding or changing corpus files

**A handoff:** `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs`, one
new migration and
`src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs`
for C-F01; A also updates `tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs`
and `scripts/Test-MigrationGrants.ps1`.

### C02 files

**Existing C files:**

- `src/Pegasus.Core/Intake/IntakeContracts.cs`
- `src/Pegasus.Core/Intake/InstructionFieldExtraction.cs`
- `src/Pegasus.Core/Intake/ProcessIntake.cs`
- `src/Pegasus.Core/Intake/DurableIntake.cs`
- `src/Pegasus.Infrastructure/Intake/MimeKitPdfPigOpenXmlIntakeSourceReader.cs`
- `src/Pegasus.Infrastructure/Intake/MimeKitPdfPigOpenXmlIntakeSourceReader.DocMsg.cs`
- `src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs`
- `src/Pegasus.Infrastructure/Persistence/EfIntakeMutationStore.cs`
- `src/Pegasus.Web/Pages/Shared/_Provenance.cshtml`
- `src/Pegasus.Web/Pages/Shared/_ProvenancePanel.cshtml`
- `tests/Pegasus.Core.Tests/Intake/InstructionEvidenceImagesTests.cs`
- `tests/Pegasus.IntegrationTests/MultiFormatIntakeWebTests.cs`
- `tests/Pegasus.IntegrationTests/MultiFormatGenuineCorpusWebTests.cs`

**A-owned read-only router:**
`src/Pegasus.Core/Custody/ExternalWorkProcessing.cs` and its shared
`tests/Pegasus.IntegrationTests/ExternalWorkProcessingTests.cs`. C supplies the
typed OCR handler/test inventory; A performs the coordinated router/DI/host and
shared-test edit.

**B-owned read-only dependency:**
`src/Pegasus.Core/Vehicle/LookupContracts.cs`. C's finite OCR-VRM candidate
adapter calls the frozen B lookup port; C does not edit or duplicate that
contract or its external lookup implementation.

**Proposed C files:**

- `src/Pegasus.Core/Intake/InstructionExtractionPolicySelector.cs`
- `src/Pegasus.Core/Intake/IntakeOcr.cs`
- `src/Pegasus.Infrastructure/Intake/AzureDocumentIntelligenceOcr.cs`
- `tests/Pegasus.Core.Tests/Intake/InstructionFieldExtractionTests.cs`
- `tests/Pegasus.Core.Tests/Intake/InstructionExtractionPolicySelectorTests.cs`
- `tests/Pegasus.Core.Tests/Intake/IntakeOcrTests.cs`
- `tests/Pegasus.IntegrationTests/StructuredIntakeSourceReaderTests.cs`
- `tests/Pegasus.IntegrationTests/AzureDocumentIntelligenceOcrTests.cs`
- `tests/Pegasus.IntegrationTests/OcrIntakeRecoveryTests.cs`

**Foundation handoff:** C-F02 supplies the OCR operation/result mapping inside
the existing external-work persistence and C-F03 supplies DI/Worker routing in
`src/Pegasus.Infrastructure/DependencyInjection.cs` and
`src/Pegasus.Worker/IntakeFunctions.cs`. Foundation owns optional use of an
existing-estate Document Intelligence resource plus configuration, managed-
identity permission and later deployment/activation. Shared fixture changes,
if required, remain in `tests/Pegasus.IntegrationTests/IntakeWebTestSupport.cs`.

### C03 files

**Existing C files:**

- `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosInstructionExtractionPolicy.cs`
- `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosMailRoutePolicy.cs`
- `src/Pegasus.Core/Intake/InstructionFieldExtraction.cs`
- `tests/Pegasus.Core.Tests/Intake/Qdos/QdosInstructionExtractionPolicyTests.cs`
- `tests/Pegasus.IntegrationTests/QdosExtractionCoverageTests.cs`
- `tests/Pegasus.IntegrationTests/PrincipalIdentificationCorpusEvidenceTests.cs`
- `reference/workproviders-and-repairers/principal-identification-corpus.v1.json`

**Proposed C profile files:**

- `src/Pegasus.Core/Intake/DirectProviders/Pch/PchInstructionExtractionPolicy.cs`
- `src/Pegasus.Core/Intake/DirectProviders/Ax/AxInstructionExtractionPolicy.cs`
- `src/Pegasus.Core/Intake/DirectProviders/Fw/FwInstructionExtractionPolicy.cs`
- `src/Pegasus.Core/Intake/DirectProviders/Qcl/QclInstructionExtractionPolicy.cs`
- `src/Pegasus.Core/Intake/DirectProviders/Oak/OakInstructionExtractionPolicy.cs`
- `src/Pegasus.Core/Intake/DirectProviders/Sbl/SblInstructionExtractionPolicy.cs`
- `src/Pegasus.Core/Intake/DirectProviders/Black/BlackInstructionExtractionPolicy.cs`
- `src/Pegasus.Core/Intake/DirectProviders/Rjs/RjsInstructionExtractionPolicy.cs`
- `src/Pegasus.Core/Intake/DirectProviders/Dfd/DfdInstructionExtractionPolicy.cs`
- `src/Pegasus.Core/Intake/DirectProviders/Kbs/KbsInstructionExtractionPolicy.cs`
- `src/Pegasus.Core/Intake/DirectProviders/Mp/MpInstructionExtractionPolicy.cs`
- `src/Pegasus.Core/Intake/DirectProviders/Yml/YmlInstructionExtractionPolicy.cs`
- `src/Pegasus.Core/Intake/DirectProviders/Als/AlsInstructionExtractionPolicy.cs`
- `src/Pegasus.Core/Intake/DirectProviders/Bc/BcInstructionExtractionPolicy.cs`

**Proposed C profile and test files:**

- `tests/Pegasus.Core.Tests/Intake/Pch/PchInstructionExtractionPolicyTests.cs`
- `tests/Pegasus.Core.Tests/Intake/Ax/AxInstructionExtractionPolicyTests.cs`
- `tests/Pegasus.Core.Tests/Intake/Fw/FwInstructionExtractionPolicyTests.cs`
- `tests/Pegasus.Core.Tests/Intake/Qcl/QclInstructionExtractionPolicyTests.cs`
- `tests/Pegasus.Core.Tests/Intake/Oak/OakInstructionExtractionPolicyTests.cs`
- `tests/Pegasus.Core.Tests/Intake/Sbl/SblInstructionExtractionPolicyTests.cs`
- `tests/Pegasus.Core.Tests/Intake/Black/BlackInstructionExtractionPolicyTests.cs`
- `tests/Pegasus.Core.Tests/Intake/Rjs/RjsInstructionExtractionPolicyTests.cs`
- `tests/Pegasus.Core.Tests/Intake/Dfd/DfdInstructionExtractionPolicyTests.cs`
- `tests/Pegasus.Core.Tests/Intake/Kbs/KbsInstructionExtractionPolicyTests.cs`
- `tests/Pegasus.Core.Tests/Intake/Mp/MpInstructionExtractionPolicyTests.cs`
- `tests/Pegasus.Core.Tests/Intake/Yml/YmlInstructionExtractionPolicyTests.cs`
- `tests/Pegasus.Core.Tests/Intake/Als/AlsInstructionExtractionPolicyTests.cs`
- `tests/Pegasus.Core.Tests/Intake/Bc/BcInstructionExtractionPolicyTests.cs`
- `tests/Pegasus.IntegrationTests/Top15InstructionCorpusTests.cs`
- `src/Pegasus.Core/Intake/MachineReadRegistrationResolution.cs`
- `src/Pegasus.Infrastructure/Intake/VehicleRegistrationCandidateLookup.cs`
- `tests/Pegasus.Core.Tests/Intake/MachineReadRegistrationResolutionTests.cs`
- `tests/Pegasus.IntegrationTests/MachineReadRegistrationLookupTests.cs`

These are the explicit fourteen new profile suites. The existing QDOS suite
stays in place, and the integration suite consumes the one profile registry.

**A handoff:** register the selector and fifteen policies only in
`src/Pegasus.Infrastructure/DependencyInjection.cs`. A does not copy profile
codes or criteria into composition.

### C04 files

**Existing C files:**

- `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosMailClassificationPolicy.cs`
- `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosInstructionExtractionPolicy.cs`
- `src/Pegasus.Core/Intake/ProcessIntake.cs`
- `src/Pegasus.Core/Triage/TriageContracts.cs`
- `src/Pegasus.Infrastructure/Persistence/EfTriageStore.cs`
- `src/Pegasus.Infrastructure/Persistence/InspectionAddressResolutionStore.cs`
- `src/Pegasus.Web/Pages/Intake/Details.cshtml`
- `src/Pegasus.Web/Pages/Intake/Details.cshtml.cs`
- `tests/Pegasus.Core.Tests/Intake/Qdos/QdosMailClassificationPolicyTests.cs`
- `tests/Pegasus.Core.Tests/Intake/DefinitiveIntakeCaseTypeTests.cs`
- `tests/Pegasus.IntegrationTests/QdosTriageIntegrationTests.cs`
- `tests/Pegasus.IntegrationTests/QdosTriageReplayIntegrationTests.cs`
- `tests/Pegasus.IntegrationTests/TriageFromIntakeIntegrationTests.cs`

**Proposed C test:**
`tests/Pegasus.IntegrationTests/QdosAttachmentTriageIntegrationTests.cs` owns
the empty-current-body, duplicate PDF/DOC, quoted-message and mixed-category
cases. C-F04 supplies the A-owned T-reference schema/migration/snapshot.

### C05 files

**Existing C files:**

- `src/Pegasus.Core/Documents/DocumentContracts.cs`
- `src/Pegasus.Core/Intake/IntakeContracts.cs`
- `src/Pegasus.Core/Intake/ProcessIntake.cs`
- `src/Pegasus.Infrastructure/Intake/MimeKitPdfPigOpenXmlIntakeSourceReader.cs`
- `src/Pegasus.Web/Pages/Shared/_EvidenceViewer.cshtml`
- `src/Pegasus.Web/Pages/Shared/_Provenance.cshtml`
- `tests/Pegasus.IntegrationTests/MultiFormatGenuineCorpusWebTests.cs`

**Proposed C files:**

- `src/Pegasus.Core/Intake/ThirdPartyReports/ThirdPartyReportContracts.cs`
- `src/Pegasus.Core/Intake/ThirdPartyReports/ThirdPartyReportExtraction.cs`
- `src/Pegasus.Core/Intake/ThirdPartyReports/ThirdPartyReportProfiles.cs`
- `src/Pegasus.Core/Intake/ThirdPartyReports/ThirdPartyReportValidation.cs`
- `tests/Pegasus.Core.Tests/Intake/ThirdPartyReports/ThirdPartyReportExtractionTests.cs`
- `tests/Pegasus.IntegrationTests/ThirdPartyReportCorpusTests.cs`
- `tests/Pegasus.IntegrationTests/ThirdPartyReportProvenanceWebTests.cs`

**A handoff:** C-F02 persistence/DI only. **B handoff:** C-B02 is consumed in
B-owned `src/Pegasus.Core/Assessment`, `src/Pegasus.Core/Reports` and Case
partials; C does not edit those trees.

### C06 files

**Existing C files:**

- `src/Pegasus.Core/Cases/OrganizationAdministration.cs`
- `src/Pegasus.Core/Address/InspectionAddressResolution.cs`
- `src/Pegasus.Core/Cases/ProviderInspectionModePolicy.cs`
- `src/Pegasus.Infrastructure/Persistence/EfOrganizationAdministration.cs`
- `src/Pegasus.Infrastructure/Persistence/EfProviderReferenceCatalog.cs`
- `src/Pegasus.Infrastructure/Persistence/InspectionAddressChoicesQueries.cs`
- `src/Pegasus.Infrastructure/Persistence/InspectionAddressResolutionStore.cs`
- `src/Pegasus.Infrastructure/Persistence/ReferenceData/provider-domains.v1.json`
- `src/Pegasus.Web/Pages/Administration/Organizations/Index.cshtml`
- `src/Pegasus.Web/Pages/Administration/Organizations/Index.cshtml.cs`
- `src/Pegasus.Web/Pages/Administration/Organizations/Edit.cshtml`
- `src/Pegasus.Web/Pages/Administration/Organizations/Edit.cshtml.cs`
- `src/Pegasus.Web/Pages/Administration/Principals/Index.cshtml`
- `src/Pegasus.Web/Pages/Administration/Principals/Index.cshtml.cs`
- `src/Pegasus.Web/Pages/Administration/Principals/Create.cshtml`
- `src/Pegasus.Web/Pages/Administration/Principals/Create.cshtml.cs`
- `src/Pegasus.Web/Pages/Administration/Principals/EvaSubmission.cshtml`
- `src/Pegasus.Web/Pages/Administration/Principals/EvaSubmission.cshtml.cs`
- `tests/Pegasus.Core.Tests/Cases/OrganizationAdministrationTests.cs`
- `tests/Pegasus.Core.Tests/Address/InspectionAddressResolutionPolicyTests.cs`
- `tests/Pegasus.IntegrationTests/OrganizationAdministrationPersistenceTests.cs`
- `tests/Pegasus.IntegrationTests/OrganizationAdministrationWebTests.cs`
- `tests/Pegasus.IntegrationTests/InspectionAddressChoicesPersistenceTests.cs`
- `tests/Pegasus.IntegrationTests/Browser/InspectionAddressChoiceBrowserTests.cs`

**Proposed C files:**

- `src/Pegasus.Core/Cases/OrganizationDirectory.cs`
- `src/Pegasus.Core/Cases/ClaimSourceAdministration.cs`
- `src/Pegasus.Infrastructure/Persistence/EfOrganizationDirectory.cs`
- `src/Pegasus.Infrastructure/Persistence/EfClaimSourceAdministration.cs`
- `src/Pegasus.Web/Pages/Administration/ClaimSources/Index.cshtml`
- `src/Pegasus.Web/Pages/Administration/ClaimSources/Index.cshtml.cs`
- `src/Pegasus.Web/Pages/Administration/ClaimSources/Edit.cshtml`
- `src/Pegasus.Web/Pages/Administration/ClaimSources/Edit.cshtml.cs`
- `tests/Pegasus.Core.Tests/Cases/OrganizationDirectoryTests.cs`
- `tests/Pegasus.Core.Tests/Cases/ClaimSourceAdministrationTests.cs`
- `tests/Pegasus.IntegrationTests/OrganizationDirectoryPersistenceTests.cs`
- `tests/Pegasus.IntegrationTests/OrganizationDirectoryWebTests.cs`
- `tests/Pegasus.IntegrationTests/ClaimSourceAdministrationTests.cs`
- `tests/Pegasus.IntegrationTests/InspectionAddressSuggestionTests.cs`

**A handoff:** C-F05 owns entity declarations/configuration, migration,
snapshot, grants, DI and host entrypoint edits. A removes the automatic-EVA
column and composition/Worker registrations only. **B handoff:** B consumes
C-B03 from its Case location picker and C-B06 for manual-only EVA, removing
automatic policy/work-item use from
`src/Pegasus.Core/Eva/EvaSubmissionPolicy.cs`,
`src/Pegasus.Core/Eva/EvaSubmissionWorkItem.cs`,
`src/Pegasus.Core/Eva/EvaApiContracts.cs` and
`src/Pegasus.Infrastructure/Persistence/EfAutomaticEvaSubmissionStore.cs`,
`src/Pegasus.Infrastructure/Persistence/EfEvaSubmissionModeStore.cs` and
`src/Pegasus.Infrastructure/Persistence/EfEvaSubmissionWorkStore.cs`. C removes
only its Principal-page controls and its administration command/query fields.

### C07 files

**Existing C files:**

- `src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs`
- `src/Pegasus.Core/ImageIntake/ImageIntakeLifecycle.cs`
- `src/Pegasus.Core/Triage/TriageContracts.cs`
- `src/Pegasus.Core/Documents/RequestUploadPolicy.cs`
- `src/Pegasus.Core/Intake/IntakeContracts.cs`
- `src/Pegasus.Core/ProviderApi/ProviderSubmission.cs`
- `src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs`
- `src/Pegasus.Infrastructure/Persistence/EfTriageStore.cs`
- `src/Pegasus.Infrastructure/Persistence/EfDocumentRequestStore.cs`
- `src/Pegasus.Web/ProviderApi/ProviderApiEndpoints.cs`
- `src/Pegasus.Web/Pages/ImageIntake/Details.cshtml`
- `src/Pegasus.Web/Pages/ImageIntake/Details.cshtml.cs`
- `src/Pegasus.Web/Pages/Triage/Index.cshtml`
- `src/Pegasus.Web/Pages/Triage/Index.cshtml.cs`
- `src/Pegasus.Web/Pages/Triage/Details.cshtml`
- `src/Pegasus.Web/Pages/Triage/Details.cshtml.cs`
- `tests/Pegasus.Core.Tests/ImageIntake/ImageIntakeLifecycleTests.cs`
- `tests/Pegasus.Core.Tests/Triage/TriageReplayTests.cs`
- `tests/Pegasus.IntegrationTests/ImageIntakePersistenceTests.cs`
- `tests/Pegasus.IntegrationTests/ImageIntakeWebTests.cs`
- `tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs`
- `tests/Pegasus.Core.Tests/Intake/IntakeEnvelopeLimitsTests.cs`
- `tests/Pegasus.Core.Tests/Qdos/QdosBoundaryContractTests.cs`
- `tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs`
- `tests/Pegasus.IntegrationTests/DocumentCustodyDurabilityTests.cs`
- `tests/Pegasus.IntegrationTests/ProviderApiSubmissionTests.cs`

**A-owned read-only contract:**
`src/Pegasus.Core/Custody/CustodyContracts.cs` (`ICaseArtifactCustody`).

**Proposed C files:**

- `src/Pegasus.Core/Intake/RetainIncomingArtifact.cs`
- `tests/Pegasus.Core.Tests/Intake/RetainIncomingArtifactTests.cs`
- `tests/Pegasus.IntegrationTests/TriageReferenceAllocationTests.cs`
- `tests/Pegasus.IntegrationTests/PublicUploadSessionTests.cs`
- `tests/Pegasus.IntegrationTests/IncomingArtifactCustodyTests.cs`

A owns the C-F04/C-F06 entity declarations/mappings, migrations, snapshot,
grants, DI/host entrypoint edits and A04 content/custody adapter. F owns the
exact host request limits/configuration/deployment. B alone edits
`src/Pegasus.Web/Pages/Cases/Index.cshtml.cs` to consume C-B04.

### C08 files

**Existing C files:**

- `src/Pegasus.Web/Pages/Index.cshtml`
- `src/Pegasus.Web/Pages/Index.cshtml.cs`
- `src/Pegasus.Web/Pages/Mail/Index.cshtml`
- `src/Pegasus.Web/Pages/Mail/Index.cshtml.cs`
- `src/Pegasus.Web/Pages/Mail/Message.cshtml`
- `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs`
- `src/Pegasus.Web/Pages/Uploads/Request.cshtml`
- `src/Pegasus.Web/Pages/Uploads/Request.cshtml.cs`
- `src/Pegasus.Web/Pages/Search/Index.cshtml`
- `src/Pegasus.Web/Pages/Search/Index.cshtml.cs`
- `src/Pegasus.Web/Pages/Search/_CasePreview.cshtml`
- `src/Pegasus.Web/Pages/Upload.cshtml`
- `src/Pegasus.Web/Pages/Upload.cshtml.cs`
- `src/Pegasus.Web/Pages/UploadStatus.cshtml`
- `src/Pegasus.Web/Pages/UploadStatus.cshtml.cs`
- `src/Pegasus.Web/Pages/UploadGroupStatus.cshtml`
- `src/Pegasus.Web/Pages/UploadGroupStatus.cshtml.cs`
- `src/Pegasus.Web/Pages/Intake/Source.cshtml`
- `src/Pegasus.Web/Pages/Intake/Source.cshtml.cs`
- `src/Pegasus.Web/Pages/Intake/Asset.cshtml`
- `src/Pegasus.Web/Pages/Intake/Asset.cshtml.cs`
- `src/Pegasus.Web/Pages/Intake/Image.cshtml`
- `src/Pegasus.Web/Pages/Intake/Image.cshtml.cs`
- `src/Pegasus.Web/Pages/Unidentified/Index.cshtml`
- `src/Pegasus.Web/Pages/Unidentified/Index.cshtml.cs`
- `src/Pegasus.Web/Pages/Unidentified/Details.cshtml`
- `src/Pegasus.Web/Pages/Unidentified/Details.cshtml.cs`
- `src/Pegasus.Core/Operations/OperationsSnapshot.cs`
- `src/Pegasus.Core/Operations/DashboardCounts.cs`
- `src/Pegasus.Web/Pages/Shared/_Layout.cshtml`
- `src/Pegasus.Web/Pages/Shared/_LayoutAuth.cshtml`
- `src/Pegasus.Web/Pages/Shared/_LayoutExternal.cshtml`
- `src/Pegasus.Web/Pages/Shared/_ShellDialogs.cshtml`
- `src/Pegasus.Web/Pages/Shared/_ErrorSummary.cshtml`
- `src/Pegasus.Web/Pages/Shared/_EvidenceViewer.cshtml`
- `src/Pegasus.Web/Pages/Shared/_FreshnessBanner.cshtml`
- `src/Pegasus.Web/Pages/Shared/_ImageGallery.cshtml`
- `src/Pegasus.Web/Pages/Shared/_InstructionDraftFields.cshtml`
- `src/Pegasus.Web/Pages/Shared/_LucideSprite.cshtml`
- `src/Pegasus.Web/Pages/Shared/_MetricCard.cshtml`
- `src/Pegasus.Web/Pages/Shared/_PageHeader.cshtml`
- `src/Pegasus.Web/Pages/Shared/_Provenance.cshtml`
- `src/Pegasus.Web/Pages/Shared/_ProvenancePanel.cshtml`
- `src/Pegasus.Web/Pages/Shared/_ReasonDialog.cshtml`
- `src/Pegasus.Web/Pages/Shared/_StatusChip.cshtml`
- `src/Pegasus.Web/Pages/Shared/_UploadOutcome.cshtml`
- `src/Pegasus.Web/Pages/Administration/Shared/_AdminNav.cshtml`
- `src/Pegasus.Web/Presentation/OperatorLabels.cs`
- `src/Pegasus.Web/wwwroot/css/site.css`
- `src/Pegasus.Web/wwwroot/js/site.js`
- `src/Pegasus.Web/wwwroot/favicon.ico`
- `src/Pegasus.Web/wwwroot/fonts/inter/InterVariable.woff2`
- `src/Pegasus.Web/wwwroot/fonts/inter/InterVariable-Italic.woff2`
- `src/Pegasus.Web/wwwroot/fonts/inter/LICENSE.txt`
- `src/Pegasus.Web/wwwroot/images/logo_no_margin.png`
- `src/Pegasus.Web/wwwroot/images/lucide-sprite.svg`
- `src/Pegasus.Web/wwwroot/images/marks/access.png`
- `src/Pegasus.Web/wwwroot/images/marks/accounts.png`
- `src/Pegasus.Web/wwwroot/images/marks/automation.png`
- `src/Pegasus.Web/wwwroot/images/marks/checkmark.png`
- `src/Pegasus.Web/wwwroot/images/marks/configuration.png`
- `src/Pegasus.Web/wwwroot/images/marks/mailboxes.png`
- `src/Pegasus.Web/wwwroot/images/marks/organisations.png`
- `src/Pegasus.Web/wwwroot/images/marks/pegasus-lockup.png`
- `src/Pegasus.Web/wwwroot/images/marks/principals.png`
- `src/Pegasus.Web/wwwroot/images/marks/roles.png`
- `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs`
- `tests/Pegasus.IntegrationTests/Browser/MailWorkspaceBrowserTests.cs`
- `tests/Pegasus.IntegrationTests/DashboardCountersWebTests.cs`
- `tests/Pegasus.IntegrationTests/WorkCentreLabelTests.cs`
- `tests/Pegasus.IntegrationTests/ShellAndStatusPageWebTests.cs`
- `tests/Pegasus.IntegrationTests/UploadConfirmationWebTests.cs`
- `tests/Pegasus.IntegrationTests/Browser/UploadDropzoneBrowserTests.cs`
- `tests/Pegasus.IntegrationTests/Browser/UploadRowsBrowserTests.cs`
- `tests/Pegasus.IntegrationTests/Browser/UploadStatusRefreshBrowserTests.cs`
- `tests/Pegasus.IntegrationTests/QdosCustodialWebTests.cs`

**Proposed C files:**

- `src/Pegasus.Core/Search/RetainedMaterialSearch.cs`
- `src/Pegasus.Infrastructure/Persistence/EfRetainedMaterialSearch.cs`
- `src/Pegasus.Web/Pages/Search/_RetainedMaterialPreview.cshtml`
- `src/Pegasus.Web/Pages/Mail/Compose.cshtml`
- `src/Pegasus.Web/Pages/Mail/Compose.cshtml.cs`
- `tests/Pegasus.Core.Tests/Search/RetainedMaterialSearchTests.cs`
- `tests/Pegasus.IntegrationTests/RetainedMaterialSearchWebTests.cs`
- `tests/Pegasus.IntegrationTests/Browser/OuterShellBrowserTests.cs`
- `tests/Pegasus.IntegrationTests/StaffCorrespondenceWebTests.cs`

**A handoff:** C-F07 query mapping/index if measured, C-F08 Graph-retained-mail
and general staff-send contracts, C-F09 shared support and DI. C reads the
A-owned `src/Pegasus.Core/Operations/StaffMailSend.cs` contract but does
not edit it. B consumes shared shell/assets but owns
every file under `src/Pegasus.Web/Pages/Cases/Shared` plus the Case-only
`src/Pegasus.Web/Pages/Shared/_EditFinishConfirm.cshtml` and
`src/Pegasus.Web/Pages/Shared/_EditHeartbeat.cshtml`.

### C09 files

C09 creates no production file. It reviews the integrated C-owned diff and
updates only existing tests or implementation files named above when an
in-scope finding requires it. Evidence output uses the repository's existing
ignored `artifacts/evaluation` convention. Governing documentation changes are
returned to the root/Foundation owner; C does not create new Markdown in the
repository.

## C01 - freeze evidence and absorb the PR 639 and PR 646 corrections

**Owner/model:** Fable 5.1 orchestrates; Opus 5 implements and self-checks.

**Inputs and dependencies:** final integration base; the locally resolvable
principal/report/EVA sources; PR 639 branch
`task/intk-048-unidentified-manual-link` at recorded tip `51e7306c`; PR 646 at
recorded tip `32a5a62ce4f13baba45a0bad06df5498f38dcd19`; and the independently
reviewed PR-069 correction evidence. No schema work starts until Stream A
accepts the recheck-watermark and retained-analysis requests in the handoff JSON.

**Production callers:** `DurableIntake` invokes destination synchronization
during receipt processing and `Pegasus.Worker/IntakeFunctions.cs` invokes the
existing periodic reconciliation sweep. The `/Received/{id:guid}` supported link,
unlink and Triage actions must surface operation conflicts rather than return a
false success. `/Received/{id:guid}` invokes `AnalyzeRetainedInstruction` for
initial analysis and re-evaluation; that command resolves the immutable logical
source/version through A04, calls the existing reader and C profile selector,
and persists candidates without allocating a principal or Case.

**Change:**

1. Generate a read-only execution manifest for the 81 instruction originals,
   29 report PDFs and 14 EVA workbooks from their recorded hashes. Keep the
   manifest under the repository's existing ignored evaluation-artifact area;
   do not add corpus binaries to Git.
2. Diff PR 639 against the integration base by behavior, not by blindly
   merging its stale branch. Port only the final reviewed rule: an Unidentified
   resolution follows the receipt's effective destination across link, unlink,
   Triage creation and relink; completed rechecks leave the candidate page;
   each reopen/re-resolve operation key is unique per transition and stable on
   replay.
3. Preserve `ReconcileUnidentifiedDestinations` as the one Core owner. Extend
   the existing `IUnidentifiedStore` and EF adapter methods; do not create a
   second reconciler or infer state in Web/Worker.
4. Ask Stream A for the single nullable recheck watermark, mapping, migration,
   runtime grant and snapshot. C implements the Core and store behavior around
   that frozen schema.
5. Keep PR 639's unrelated stale test churn and generated migration files out.
   Record a line-by-line preservation table for every retained behavior before
   the old PR is later closed as superseded.
6. Diff PR 646 by behavior and absorb its create-only Provider API residual:
   reuse `EvaluateIntakeCaseMatch.ExecuteDeclaredAsync` and the provider's
   existing normalization/eliminator; a unique or ambiguous existing-Case match
   terminates with `provider_existing_case_match`, mutates no existing Case and
   allocates no duplicate. Keep its documentation/shared-fixture edits with the
   owning streams and record a hunk disposition before root closeout.
7. Implement `AnalyzeRetainedInstruction` as the one production path for
   unresolved retained material. It reads A04's logical document/version,
   identifies a document profile independently of route identity, extracts and
   persists C-B01 candidates, and returns `Analyzed`, `NoProfile`, `Ambiguous`,
   `SourceUnavailable` or `Conflict`. A document match proposes a principal;
   only a staff confirmation or separately accepted versioned route may
   authorize normal Case/PO allocation. Re-evaluation calls the same command
   with expected receipt version and an idempotent operation key.

**Tests and expected outputs:**

- Core: link Case A -> resolve A -> unlink -> reopen -> link Case B -> resolve B
  yields exactly one open/current queue at every stage; replay makes no new
  history; operation conflicts propagate.
- Real SQL: a completed no-change recheck is absent from
  `Li
