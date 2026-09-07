# Archived original plan — part 2 of 4

Original ticket document: `plan/plan.md`
Original SHA-256: `62649b22a7e43d771820d36c4126a65867fc38d99b636c54a20cc5a6468f3a95`
Character range: 30000–59999 of 115556
Reconstruction: concatenate the payload sections from parts 1–4 in order.

## Payload

cyTests.cs`
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
  `ListResolutionsToRecheckAsync`; with page size one, the next stale row becomes
  visible; a second sweep returns `Candidates=0`, `Corrected=0`, `Failures=0`.
- Architecture: Worker and `DurableIntake` still call the one reconciler; no
  direct Web state derivation appears.
- Provider API: the first declared instruction creates one Case; an identical
  second submission ends with `provider_existing_case_match`, one Case and one
  link remain, and ambiguous matches also allocate nothing.
- Each of all 15 genuine profile samples reaches extraction through
  `AnalyzeRetainedInstruction`; no-route samples persist candidates but create
  zero Cases, multiple profiles return `Ambiguous`, replay writes no duplicate,
  and staff confirmation is required before allocation.
- Evidence-manifest check reports `81/81`, `29/29`, and `14/14` hash matches,
  identifies the two differently hashed `providers-worked-on.xlsx` copies, and
  reports E01-E28 as `unavailable`, never `passed`.

**Focused commands:**

```powershell
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ReconcileUnidentifiedDestinationsTests"
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~UnidentifiedReconciliationTests|FullyQualifiedName~ProviderApiSubmissionTests|FullyQualifiedName~RetainedInstructionAnalysisTests"
pwsh -File ./scripts/Test-MigrationGrants.ps1
```

Stop if the corrected lifecycle requires another state owner, more than the one
watermark, an invented source fixture, or a swallowed conflict.

## C02 - preserve structured source provenance through the intake pipeline

**Owner/model:** Fable 5.1 orchestrates; Opus 5 implements.

**Production callers:** `AnalyzeRetainedInstruction` and queued intake consume
the reader result; `QdosInstructionExtractionPolicy` and every new profile emit
candidates; `EfIntakeReceiptStore`/`EfIntakeMutationStore` persist them; the
existing instruction draft and provenance partials display them. When the
reader identifies eligible pages, the existing external-work Worker route calls
`IProcessIntakeOcr`, which uses A04 to reopen the exact logical source/version,
calls the C Azure adapter, persists the result, and re-enters
`AnalyzeRetainedInstruction` once.

**Change:**

1. Extend the existing `IntakeContentFragment`/candidate path with one minimal
   structured locator capable of page, table/cell, PDF form field and bounded
   text region. Preserve raw text, role, source label, source/asset hash,
   occurrence and reader/policy version. Do not introduce a parallel document
   model.
2. Extend `MimeKitPdfPigOpenXmlIntakeSourceReader` and its DOC/MSG partial to
   emit structure already exposed by PDFPig/Open XML. Keep EML/MSG outer
   transport, proved original sender, current message body, quoted history and
   attachments distinguishable. Its positive scan-like or unusable-text-map
   result supplies exact page numbers; corrupt, encrypted, non-renderable or
   merely ambiguous documents are refused without OCR.
3. Extend `InstructionFieldExtraction` to bound values by label, sibling cell,
   form field or region. Return every viable source candidate and explicit
   `Missing`/`Ambiguous` outcomes. Do not collapse role conflicts.
4. Hand Stream B the frozen candidate/provenance projection. B remains the
   owner of accepting values into Case/assessment/report aggregates.
5. Add one provider-neutral page OCR contract in `IntakeOcr.cs`: exact logical
   source/version and SHA-256, qualified page list, operation ID, result state,
   provider/model/API version, response hash, page text, words/lines/tables with
   coordinates/confidence, and typed failure/retry metadata. Preserve
   `Pending`, `Processing`, `Completed`, `RetryScheduled`, `Failed` and
   `Unknown`; an uncertain provider side effect stays `Unknown` until the
   recorded operation is reconciled and is never blindly resubmitted.
6. Implement `AzureDocumentIntelligenceOcr` with the existing `HttpClient` and
   `Azure.Identity`, no package, against Azure Document Intelligence
   `prebuilt-layout`, REST `api-version=2024-11-30`. Submit only the qualified
   pages through the documented `pages` parameter, poll the returned operation
   location within the existing bounded external-work attempt, validate the
   operation identity and response, and map coordinates without accepting a
   field from confidence alone. The REST contract is the official
   [Analyze Document API](https://learn.microsoft.com/en-us/rest/api/aiservices/document-models/analyze-document?view=rest-aiservices-v4.0+%282024-11-30%29).
7. Reuse the existing external-work outbox, dispatcher, retry/recovery and
   attribution path. Store source/page/operation identity before the HTTP call;
   completion stores response hash/version and page output atomically before
   re-analysis. Timeout/throttle/outage schedules only a safe retry; malformed,
   low-confidence, missing-structure or inconsistent output fails closed to
   staff review. Do not add another queue, Worker, OCR vendor or runtime.

**Tests and expected outputs:**

- Reader fixtures retain outer sender and current/quoted message bodies as
  separate fragments, preserve attachment identity, page/cell/form locators
  and byte hash, and replay identically.
- OAK header labels bind to their aligned cells; ALS paired columns retain the
  correct party; DFD form fields retain field identity; flattened neighboring
  text cannot swap their values.
- Two different candidates for one field return `Ambiguous` with both raw
  values and locators. Missing model/VAT/date returns unavailable. No test
  expects a guessed value.
- Existing QDOS corpus output stays unchanged except for the explicit C03/C04
  corrections.
- Embedded-text pages result in OCR calls `0`; a scan-like genuine local sample
  and genuine stored YL69YFO provider output, if present, retain page coordinates,
  confidence, response hash and `2024-11-30` provenance. If genuine stored
  output is absent, automated adapter tests use a structural non-domain fake and
  provider correctness remains `INCONCLUSIVE` pending operator activation; no
  OCR text/result is fabricated as evidence.
- Timeout, throttling, restart after submit, ambiguous operation lookup and
  replay recover one durable operation without a second side effect. Corrupt,
  encrypted, non-renderable, low-confidence and ambiguous pages create no
  accepted candidate or Case.

**Focused commands:**

```powershell
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~InstructionFieldExtraction"
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~MultiFormatGenuineCorpusWebTests|FullyQualifiedName~AzureDocumentIntelligenceOcrTests|FullyQualifiedName~OcrIntakeRecoveryTests"
```

Stop if a profile must depend on a global positional line number, Infrastructure
would own a business mapping, the reader cannot retain the required layout, or
the adapter needs another OCR package/vendor/runtime.

## C03 - implement all fifteen instruction profiles

**Owner/model:** Fable 5.1 orchestrates batches; Opus 5 implements. Build in
volume order: QDOS/PCH, AX/FW/QCL/OAK, then SBL/BLACK/RJS/DFD/KBS/MP/YML/ALS/BC.
Do not activate a sender route merely because its document profile passes.

Every profile uses [the common method and ranking](../../reports/principals/top-15-principals.md),
its linked immutable samples, and the later local delta map in
[PRINCIPAL_DOCUMENT_MAPS](../../../more_docs/PRINCIPAL_DOCUMENT_MAPS.md).
The delta map's E01-E28-only statements remain guarded and inactive.

| Profile and source | Individual bounded rule | Required corpus-test output |
| --- | --- | --- |
| [QDOS](../../reports/principals/QDOS/method.md) | Extend the existing QDOS policy. Scope `Our Client`, `Our Ref`, `Registration`, `Our Client's Vehicle`, accident/date, explicit Mileage/Speedo, location and circumstances. Keep damage, pre-existing damage, driveability, third-party facts and requested work separate. The principal default is Image Based Assessment even when a repairer address is extracted; a staff repairer-location override records reason and keeps both sources. | Each of five originals emits the exact labelled claimant/reference/VRM and all usable optional fields with locators. Full vehicle text survives; make/model split only with labelled evidence. Damage never appears inside accident circumstances. Missing lower-page evidence is withheld. Existing confirmed data wins a conflict. |
| [PCH](../../reports/principals/PCH/method.md) | Distinguish audit from credit-repair forms; policyholder from driver; principal claim from insurer claim; repairer/storage and hire/rates appendix. Connexus roles remain explicit. Performance/Parkhouse, Lawshield and Everywhen signatures are separate until evidenced. | Five originals produce policyholder, correct claim role, VRM, separately labelled make/model, incident date, explicit location/repairer and available rates. Driver/Connexus/footer values do not replace policyholder/principal. Unproved variants remain unmatched. |
| [AX](../../reports/principals/AX/method.md) | Scope Name/VRM inside Client Details even when Bodyshop Details comes first. Keep bodyshop distinct from inspection location. `Report Due on` is a deadline, never inspection date; tolerate observed section-order variants. | Five originals emit client, AX reference, VRM, labelled vehicle, accident date/circumstances and role-specific bodyshop/location. Deadline is retained with its role and inspection date is unavailable unless explicitly appointed/completed. |
| [FW](../../reports/principals/FW/method.md) | Support a current inline email instruction and attachments. Exclude quoted earlier instructions. Scope insured, third party and inspection-location blocks separately. | Five MSG originals produce insured/reference/VRM/make-model/date/location/circumstances from the current instruction. Quoted values and third-party vehicle/name do not become claimant facts; conflicting current values are ambiguous. |
| [QCL](../../reports/principals/QCL/method.md) | QC Law is issuer; Complex Reports may be sender/intermediary. Support tab/concatenated labels without losing boundaries. Keep Box reference and report-due date roles distinct. | Five DOCX originals emit claimant, `Our Ref`, VRM, explicit make/model, accident date and location. `Report Due on` never fills inspection date; missing model/reference stays unavailable rather than borrowed from metadata. |
| [OAK](../../reports/principals/OAK/method.md) | Bind header-table labels to aligned values; preserve Source/Introducer separately; a generic total-loss request is requested work, not an accepted outcome. | Five DOC originals return the aligned `Our Ref` and instruction date, client/VRM/model/address/circumstances with cell locators. Sequential flattened values fail closed; total-loss wording does not emit a report verdict. |
| [SBL](../../reports/principals/SBL/method.md) | Parse paired sections and route roles, international registration/address shapes,
