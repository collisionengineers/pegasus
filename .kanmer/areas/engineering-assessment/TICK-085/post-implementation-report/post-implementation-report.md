# Post-implementation report — TICK-085

## State

Source frozen after the approved four-failure fixture correction on
2026-09-08. Partial runtime evidence exists below; not fully verified or delivered.
Branch TICK-085-glass-pdf-import; worktree .worktrees/tick-085.
HEAD/base cdaa02584c38ecc27d3bd24784f59da189138bc1.
No author build, test, capture, commit, PR, provider or cloud call.
Plan 1432dbe470b90de2; files 53c037ca57c47e28, both read back.
29 source/doc/test paths: 26 modified, 3 added; generated UI still owed root.

## Implementation

- One PDF container reuses PdfPig coordinates and TICK-041's positive Type3
  qualification, then distinguishes Glass/Audatex by actual printed markers.
  JSON/XML use the same completed-or-qualified-pages contract.
- Glass reader preserves ordered main/included rows, net hours, parts joins,
  guide identities, notes and full section/document reconciliation. The
  immutable four-oracle files provide every expected row independently.
- ImportRawEstimate alone proves actor, current persisted version/lease and
  exact retained tuple before source-hash replay or OCR. Pending/Unknown
  returns one deterministic existing operation. Completed output is validated
  and parsed without HTTP/provider access in a format reader.
- Existing repair-specification store shares its guarded transaction/history
  and writer. Imported rows are unconfirmed Drafts; Current is untouched.
  Ordinary Automation SaveEstimate remains AiDraft/held-job restricted.
  Existing Engineer Use estimate remains the acceptance action.
- Web retains before canonical import. Fresh confirmed staff addition uses
  exactly submitted version + 1; pending custody/replayed upload does not
  revive old authority. Confirmed retained sources offer Complete import.
  Real MCP and Glass completion consume the same canonical result.
- Approved metadata correction adds only occurrence.VersionId == version.Id
  to EfDocumentCustodyStore's metadata query. Correctly paired historical
  versions remain allowed. No custody mutation or IsCurrent narrowing.
- FRD-06/10 describe actual behavior and preserve provider/live evidence limits.

## Focused coverage authored

- Core source/format/actor checks, persisted-authority-before-replay seam,
  Pending/Unknown durable identity, completed/missing-page protocol.
- Four genuine hash-bound full ordered oracles: 244 main rows including 58
  included rows; 65 appendix part joins. Existing Core totals verify EC labour
  and no appendix double-charge using source rates solely as test evidence.
- Genuine YL69YFO only proves six positive qualified pages and refusal of
  missing OCR pages; no fifth parse or OCR result is fabricated.
- Actual MCP -> Core -> real EF metadata/estimate store. Only external source
  bytes are substituted using the existing XML fixture. Includes scope, stale
  version/token, foreign occurrence/hash and same-document tuple mismatch;
  correctly paired historical/current metadata are explicit positive checks.
- Real Web runtime-role store checks nonmutating authority, imported
  unconfirmed Draft, stale replay refusal, valid replay preserving live lease,
  no duplicate Draft, non-Engineer refusal and existing Engineer acceptance.
- Actual Web upload/canonical parser path plus bounded pending UI transport
  reload/completion; existing parser/editor/Glass callers adapted.

## Verification attempts

Author ran git -c core.safecrlf=false diff --check: exit 0, no findings.
Read-only source/fixture inspections only; no application PASS claim.
Root verification, fresh Case captures and snapshot/catalogue evidence remain
required. All future non-PASS attempts must stay in this record.

## Root commands

Workstation: Windows PowerShell 7, exact worktree above.
Use established locked restore/Release build once; no parallel author run.

Core filter:

```text
FullyQualifiedName~Pegasus.Core.Tests.Assessment.EstimateTests
```

Focused integration filter (explicit genuine-corpus inclusion):

```text
FullyQualifiedName~GlassEstimatePdfParserTests|FullyQualifiedName~AudatexEstimatePdfParserTests|FullyQualifiedName~JsonEstimateParserTests|FullyQualifiedName~GlassEstimateXmlParserTests|FullyQualifiedName~AssessmentEstimateImportWebTests|FullyQualifiedName~AssessmentPersistenceIntegrationTests.ImportedDocumentStorePreservesAuthorityOnReplayAndRequiresEngineerAcceptance|FullyQualifiedName~AutomationAssessmentIngressTests.CanonicalEstimateImportThroughMcpPersistsUnconfirmedSourceBackedRowsAndRejectsForeignOrStaleAuthority|FullyQualifiedName~AutomationAssessmentIngressTests.EstimateImportInvokesCanonicalTypedBoundaryAndSurfacesPendingOcr|FullyQualifiedName~AutomationAssessmentIngressTests.EstimateSaveRequiresTheHeldEstimateJobAndLandsAsAnUnconfirmedAiDraft|FullyQualifiedName~ProductionCompositionTests.CasePageAndCanonicalImportResolveTheirEstimateParsersAndGlassSessions|FullyQualifiedName~GlassRepairEstimateGatewayTests.ACompletedCallbackRetainsBothArtifactsAndLandsOneDraft|FullyQualifiedName~GlassRepairEstimateGatewayTests.AnIdenticalCallbackReplayReturnsWhatTheFirstOneProduced|FullyQualifiedName~GlassRepairEstimateGatewayTests.TheImportIsNamedByTheOccurrenceCustodyMinted
```

Set PEGASUS_REFERENCE_PACK_ROOT to the existing ignored pegasus_pack root,
which contains glasses-integration/glass_ref_docs and current/full-row oracles.
Do not count a missing-root skipped case as acceptance.
Use --configuration Release --no-build with per-run unique TRX names.
Root may split fast parser/corpus and SQL/Web cohorts without repeating them.
UI scope is case-details. Existing canonical default/unavailable/conflict
capture cohort, followed by scoped snapshot update/verify and catalogue.
No visual, CI, integrated or deployed claim exists.

## Remaining acceptance

Actual retained Azure YL69YFO output and independently reviewed fifth full-row
oracle remain root-coordinated release acceptance. No numeric confidence
threshold: complete attributable evidence and arithmetic are required, and
import completion never confirms rows or promotes Current. No all-five/OCR
provider PASS, Review/Done, self-review, merge or deployment is authorized
before root disposition and evidence.


## Pre-runtime review findings and authorized correction — 2026-09-08

Root approved two bounded corrections within the existing file map before any
runtime attempt. Preserve the validated submitted browser token after a
completed canonical import; the existing redirected GET clears it only when
the persisted lease was consumed. Source-hash replay otherwise keeps valid
server authority but incorrectly asks the browser to Recover. The existing Web
fixture now models imported mutation and lease consumption; a completion replay
must keep one Draft, the same workflow version and fresh lease, without another
writer/history side effect. Fresh import must still clear authority on GET.

Second finding confirmed and now corrected below. Canonical import must also
reuse AssessmentAccessPolicy on the persisted workflow before bytes/OCR/hash replay and again in the imported-save transaction.
CaseMutationGuard alone is insufficient: it permits nonterminal pre-handoff
states which the assessment policy makes read-only. Use the existing policy,
not a second lifecycle list. Retain the just-accepted Review MCP setup as a
no-bytes/no-OCR/no-Draft negative, then perform native Engineer handoff and
acquire fresh authority for its positive path. The existing SQL store test must
also refuse direct imported save in read-only states. No unrelated writer,
schema, route, contract flag, Core vocabulary or test framework is authorized.


Third pre-runtime finding (minor), root approved: the Web raw-import dialog
uses the design authority's provider-plus-sequence default, without an initial
Name field or handler parameter. Pass an empty name into the existing canonical
command. Keep Core/MCP explicit names and the ordinary estimate-name editor.
Update the initial Web expectation to Audatex 1, not an arbitrary supplied name.


## Corrected source freeze — 2026-09-08

All three root-confirmed pre-runtime findings are corrected within the mapped
28 paths. No compiler/runtime attempt has run or been claimed.

1. Browser completion now stores the canonical command's validated submitted
   authority and lets existing RestoreLeaseState clear it against persisted
   state on GET. CompletingAnImportedSourceAgainKeepsFreshBrowserAuthorityWithoutAnotherMutation
   exercises real Core hash replay through HTTP, one recorded writer/Draft,
   unchanged modeled workflow version and active lease, and a redirected form
   carrying lease-2 without Recover editing. The fresh-import test still proves
   lease-1 is absent and Edit Case is offered after consumed authority. This Web
   fixture models persistence; actual history/count assertions remain in the
   mapped real MCP and EF tests, not claimed from the fake.
2. EfRepairSpecificationStore invokes one private RequireAssessmentEditable
   helper in both persisted pre-read authority and imported-save transaction.
   It delegates to AssessmentAccessPolicy.IsReadOnly; no duplicate state list.
   The real MCP test first proves Review refuses with no bytes, OCR operation
   or Draft, then performs IAssignCaseEngineer native handoff and acquires new
   MCP authority for import/replay. The SQL test explicitly exercises Review,
   NotReady and Held refusal at both entry points with no version/history/Draft
   mutation. FRD-06 now states the same existing engineering-state requirement.
3. The raw Web Name field/handler argument are removed; upload/completion pass
   empty name for canonical provider-plus-sequence naming. Initial Web import
   asserts Audatex 1, and the source-selector negative also proves import-name
   absent. Core/MCP explicit names and ordinary editor rename are unchanged.

Existing root filters above already cover all new assertions; no additional
cohort or full-suite duplication is needed. Latest bounded git diff --check
passed exit 0. Source is frozen again for one combined root runtime attempt.


## First root runtime attempt — retained FAIL

Root session 80403 exited 1 before any test ran. Locked restore of all seven
projects passed (maximum reported project time 918 ms). Whole-solution Release
build failed after 58.02 seconds, zero warnings and five test compile/analyzer
errors. No TRX or capture was created; Core and Integration cohorts are still
unrun. This failure is retained, not converted into a product/runtime failure.

- AssessmentPersistenceIntegrationTests lines 922/923 and
  AutomationAssessmentIngressTests line 34 referenced unqualified GlassExport.
  The real existing fixture is nested in GlassEstimateXmlParserTests; both
  callers now qualify that existing type. No source evidence was fabricated.
- AssessmentEstimateImportWebTests line 1368 passed retained Documents in
  constructor argument 7 (RequestUploadLinks). They now occupy the actual
  CaseDetails.Documents argument 4; ActiveLease remains argument 3.
- ProductionCompositionTests line 108 triggered xUnit2031. It now uses
  Assert.Single(parsers, predicate), preserving the exactly-one-parser assertion.

Only those four mapped test files changed in this correction. No production,
contract, parser, source fixture or assertion meaning changed. Bounded git diff
--check passes exit 0. Source frozen for root incremental Integration build
and the first still-unrun Core/Integration cohorts, using the same filters.

## Second root runtime attempt — retained partial PASS and FAIL

Root session 24236 exited 1. After the fixture-only compiler correction,
`dotnet build ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj
--configuration Release --no-restore` passed in 17.71 seconds, zero warnings
or errors. The preceding session 80403 used
`dotnet restore ./Pegasus.slnx --locked-mode` (PASS), then
`dotnet build ./Pegasus.slnx --configuration Release --no-restore` (FAIL).
No first-attempt tests ran, and its five errors remain above.

The second attempt ran the exact Core and Integration filters already recorded
under Root commands, with --configuration Release --no-build and the existing
PEGASUS_REFERENCE_PACK_ROOT pointing to ignored pegasus_pack. Unique TRXs were
read from their real project TestResults directories without alteration:

Root confirmed the actual session 24236 test invocations: dotnet test used
./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj or
./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj respectively,
--configuration Release --no-build, --filter with the exact corresponding
Core/Integration filter under Root commands, and respectively
--logger 'trx;LogFileName=tick-085-core-compile-corrected.trx' or
--logger 'trx;LogFileName=tick-085-integration-compile-corrected.trx'. There was
no --results-directory argument; per-project TestResults is the actual default.


| Artifact | Actual result | UTC run start / finish |
| --- | --- | --- |
| tests/Pegasus.Core.Tests/TestResults/tick-085-core-compile-corrected.trx | 61 executed, 61 passed, 0 failed, 0 skipped; root reported 186 ms | 2026-09-08T04:22:16.7076980Z / 04:22:18.3287734Z |
| tests/Pegasus.IntegrationTests/TestResults/tick-085-integration-compile-corrected.trx | 117 executed, 113 passed, 4 failed, 0 skipped; root reported 1m58s | 2026-09-08T04:22:19.8336705Z / 04:24:20.2772707Z |

SHA-256, in the same order:

```text
E75A52450E4A99CEFEBA665D6C4B21B291FA55E5AB4E72E90314E3C4C6658FB2
2AE32A27F7B0E8B526D5D199C2FC89278C46B3423884DA6B779A94125A74194D
```

The actual passing TRX entries include all four
EveryReadableOriginalMatchesItsIndependentFullOrderedRowOracle cases:
LT72PYX 102/45, ML23OXR 46/8, LG73ZCJ 66/9 and VX21TZD 30/3 main/parts rows.
All 244 main rows and 65 parts entries passed their independently authored
hash-bound oracles. TheFifthOriginalPositivelyQualifiesItsSixPagesWithoutFabricatedOcrCompletion
also passed: this proves qualification only, not retained OCR or fifth parsing.
No snapshot, manual visual, CI, merged or deployed evidence is claimed.

### Consolidated four-failure disposition

1. CanonicalEstimateImportThroughMcpPersistsUnconfirmedSourceBackedRowsAndRejectsForeignOrStaleAuthority
   and EstimateSaveRequiresTheHeldEstimateJobAndLandsAsAnUnconfirmedAiDraft
   stopped at AutomationMcpTestSupport.SeedAcceptedCaseAsync: expected
   CaseCreated, actual NeedsSorting. The old minimal fabricated QDOS email
   lacks current work-type evidence. Do not alter intake policy or invent an
   instruction to make MCP setup succeed. Root approved reuse of unchanged
   AllocationTestData.StoreDefinitiveReceiptAsync through real
   IIntakeReceiptStore in the same host, retaining the CaseCreated assertion,
   real IAcceptIntake, version 0, SeedPrincipalAsync, AB12CDE and completeness
   override. This is processed-receipt/allocation setup, not classification or
   source-byte/custody evidence. The actual MCP authority/native-handoff/import
   assertions remain unchanged. No corpus dependency, new helper or host.
2. EstimateImportInvokesCanonicalTypedBoundaryAndSurfacesPendingOcr used
   GetProperty for a null estimateId. Existing MCP 1.4.0 default options use
   WhenWritingNull (official installed package XML, McpJsonUtilities.DefaultOptions).
   The assertion now requires omission through TryGetProperty == false.
   Operation ID, Unknown state and exactly two canonical calls remain checked.
3. OnlyAnEngineerCanImport expected an old import-specific refusal string.
   It now checks the reused guard's existing Only an Engineer can change an
   estimate wording. Redirect, zero added documents and zero saved estimates
   remain checked; no authorization or response production code changed.

Only AutomationMcpTestSupport.cs was added to the scope map, before edits.
The batch changes that file and the already-mapped
AutomationAssessmentIngressTests.cs / AssessmentEstimateImportWebTests.cs.
The exact 17 helper consumers across five classes are recorded in files.md.
Root approved checking those consumers plus the two assertion-only methods;
unchanged Core 61 and 113 passing Integration results are retained, not rerun.
The three-file correction is frozen; no author build/test/capture/PR occurred.

### Exact correction filter for root (19 methods)

```text
FullyQualifiedName=Pegasus.IntegrationTests.AutomationMcpIngressTests.CaseGetUsesTheBoundedHeaderAndCursorSubLists|FullyQualifiedName=Pegasus.IntegrationTests.AutomationAdministrationWebTests.ActivityRendersCaseReferencesAndNoFilterNarration|FullyQualifiedName=Pegasus.IntegrationTests.AutomationDocumentIngressTests.AddAndDownloadOverHttpReplayAndAttributeHistory|FullyQualifiedName=Pegasus.IntegrationTests.AutomationDocumentIngressTests.ExportRefusesWhenTheCaseIsNotInReview|FullyQualifiedName=Pegasus.IntegrationTests.AutomationDocumentIngressTests.ExportSucceedsAfterReturnToReview|FullyQualifiedName=Pegasus.IntegrationTests.AutomationAiJobIngressTests.MarketResearchCompletesOverHttpWithCaseLeaseDocumentValuationAndActorHistory|FullyQualifiedName=Pegasus.IntegrationTests.AutomationAiJobIngressTests.MarketResearchCompletionSucceedsWhileAutomationIsSwitchedOff|FullyQualifiedName=Pegasus.IntegrationTests.AutomationAiJobIngressTests.MarketResearchCompletionReplaySurvivesStaffConfirmation|FullyQualifiedName=Pegasus.IntegrationTests.AutomationAiJobIngressTests.MarketResearchCompletionRefusesAMissingCaseLeaseWithoutChangingTheJob|FullyQualifiedName=Pegasus.IntegrationTests.AutomationAssessmentIngressTests.CanonicalEstimateImportThroughMcpPersistsUnconfirmedSourceBackedRowsAndRejectsForeignOrStaleAuthority|FullyQualifiedName=Pegasus.IntegrationTests.AutomationAssessmentIngressTests.AssessmentUpdateRejectsDirectWritesToDerivedImpactFields|FullyQualifiedName=Pegasus.IntegrationTests.AutomationAssessmentIngressTests.AssessmentUpdateOverHttpMutatesUnderLeaseWithCorrelatedAttribution|FullyQualifiedName=Pegasus.IntegrationTests.AutomationAssessmentIngressTests.CaseUpdateDetailsOverHttpMutatesUnderLeaseWithLoggingParityAndReopensCompleteness|FullyQualifiedName=Pegasus.IntegrationTests.AutomationAssessmentIngressTests.AStaffHeldLeaseRefusesAutomationBeginWriteAndEndOverHttp|FullyQualifiedName=Pegasus.IntegrationTests.AutomationAssessmentIngressTests.AnAutomationHeldLeaseRefusesTheStaffClaimAndLeavesTheWorkspaceReadOnly|FullyQualifiedName=Pegasus.IntegrationTests.AutomationAssessmentIngressTests.CaseUpdateDetailsRefusesAMissingEditLeaseWithFailedHistoryAndNoTokenDisclosed|FullyQualifiedName=Pegasus.IntegrationTests.AutomationAssessmentIngressTests.EstimateSaveRequiresTheHeldEstimateJobAndLandsAsAnUnconfirmedAiDraft|FullyQualifiedName=Pegasus.IntegrationTests.AutomationAssessmentIngressTests.EstimateImportInvokesCanonicalTypedBoundaryAndSurfacesPendingOcr|FullyQualifiedName=Pegasus.IntegrationTests.AssessmentEstimateImportWebTests.OnlyAnEngineerCanImport
```

Root uses the incremental Integration-project Release --no-restore build,
then the same project with --configuration Release --no-build, the exact
filter above and a new uniquely named TRX. This correction run is not yet
executed. Fresh scoped Case captures/snapshot verification and the genuine
fifth OCR acceptance remain outstanding. Author git -c core.safecrlf=false
diff --check passed exit 0 after the batch; no other source correction.
