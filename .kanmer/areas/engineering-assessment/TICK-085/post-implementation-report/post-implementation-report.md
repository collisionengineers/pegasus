# Post-implementation report — TICK-085

## State

Source frozen for root verification on 2026-09-08, not verified or delivered.
Branch TICK-085-glass-pdf-import; worktree .worktrees/tick-085.
HEAD/base cdaa02584c38ecc27d3bd24784f59da189138bc1.
No author build, test, capture, commit, PR, provider or cloud call.
Plan 16b065e44461cc55; files 43b95919921e9630, both read back.
28 source/doc/test paths: 25 modified, 3 added; generated UI still owed root.

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

## Focused coverage authored, not yet run

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
