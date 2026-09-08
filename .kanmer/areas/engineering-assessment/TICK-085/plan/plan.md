# Plan — TICK-085: canonical Glass's PDF import

## Objective

Import the five supplied Glass's calculation PDFs through one retained-source
command used by Web and authorized MCP, producing a source-backed Draft or an
honest durable OCR pending/unknown result, never partial lines or silent
provider resubmission.

## Starting state

Evidence: research/research.md@d3759f8ba28174b4; files/files.md@5f18ecab1fd5634d.
Current accepted baseline: dev cc441645b0a62a806e34367ad75e9eaff4df8b11.
TICK-041's d367219669ad26d5f2b727bd10b582330febc906 contract is integrated.
CASE-049 native handoff is Done/closed at 3a5ce645cfc0872d7a4324c6818497360c39cca4.
ENG-041's automatic-custody correction is Done/closed at cc441645b, with
FRD-06 ownership released. Do not use its superseded baafa29e authority behavior.
Execution still waits for TICK-035 to integrate and release Infrastructure DI
and ProductionComposition ownership, and UIIMP-017 to release the generated
snapshot index. The root approved the canonical command and persisted-authority
dispositions below. Ticket remains Preparing, untaken; no source worktree exists.
Historical ticket descriptions of absent Glass XML/launch and no shared command
are stale: current XML and launch stay, current Web bypass is repaired.

## Governing docs

- Meets FRD-06 canonical ordered source-backed Draft, whole-file refusal,
  exact source/Case hash replay and no Current mutation on import.
- Meets FRD-07 external handoff separation; no direct vendor service change.
- Meets FRD-11 existing rate-card, repairer VAT, immutable source evidence and
  Engineer acceptance. Imported PDF hours do not borrow XML gross-time rules.
- Implements current FRD-10 raw retained import under automation.assessment;
  update its typed pending/unknown outcome and link it before execution.
- TICK-041 owns ADR-0040 provider/qualified OCR decision. Read/link accepted
  final decision before execution. No new architecture is proposed here.
- Follows design README's concise controls; ENG-033 owns whole-page drop and
  removal of the existing import dialog, not this ticket.

## Required changes

Extend existing IEstimateDocumentParser with one synchronous read result:
completed ParsedEstimate (including actual route) OR qualified OCR pages.
Its input is bytes plus retained provider-neutral OCR pages when available.
No HTTP, operation-store access or async wait inside a format parser. JSON and
XML adapt the same result; one PDF container reader uses PdfPig words, or only
qualified retained OCR words, then exact Glass/Audatex markers. Zero/multiple
formats and malformed business evidence reject, not OCR.

ImportRawEstimate remains sole orchestrator. Prove actor/mutation and exact
Case/occurrence/version/length/hash before source-hash replay or OCR lookup.
Expose a narrow non-mutating authority check on existing
IRepairSpecificationStore: load the persisted CaseWorkflow and reuse
CaseMutationGuard.Require for current version, holder/token, expiry and mutable
state. Do not call EfRepairSpecificationStore.Guard: it increments version and
clears the lease. Require current authority before any OCR enqueue or replay;
check again in the existing final save transaction. Core still owns the
Engineer/authorized Automation import rules; no new guard/service/permission.
Queue/find deterministic document OCR via TICK-041 BeginDocumentAsync, return
operation ID/state for pending/unknown, and parse retained output only after
Completed and its existing provenance validation. Same source never queues
another operation. No background estimate save or expired human lease use.

Use a narrow method on existing IRepairSpecificationStore for canonically
validated imported documents, sharing its normal transaction/history/line
writer. Ordinary ISaveEstimate must still reject Automation forged-provider
requests; its AiDraft/held-job requirement remains. MCP requires existing
assessment scope, retained document authorization, version and lease. Imported
Automation rows remain unconfirmed Draft evidence; only an Engineer can make
Current. No caller-supplied trusted bool or source enum confers permission.

Web retains through existing custody with the submitted Case version, then
calls canonical import, never a second parser/save path. Distinguish explicit
staff IAddCaseDocument from automatic artifact confirmation: the former's
successful non-replay transaction calls Complete exactly once; the latter
preserves version and lease after ENG-041's correction. For a fresh staff
addition, acquire the import lease only at the exact committed version
(submitted version plus that documented one mutation), refusing any concurrent
change; never adopt a later mutable-read version. On custody replay, do not
reuse the old form's version/lease or assume another increment: use retained-
source completion with freshly submitted authority. Glass callbacks retain
the original live authority through automatic confirmation. Do not alter
custody persistence or introduce a second result/store solely for this flow. Pending sources remain discoverable from
CaseFiles.Live and their estimate-import occurrence identity. The Estimate
section's short Complete import form reuses exact occurrence/version/hash plus
a newly submitted version/lease; it does not upload again or restart unknown
OCR. No new operation-list store or temp-only resume state is needed.

Glass parsing uses coordinate rows and printed section anchors. Preserve
Body/Auxiliary/Paint operations, effective hours, material amounts, descriptions,
guide/part identity, annotations and raw row provenance. Include parent-only
operations without double cost. RP/R/PR/UI/EC and paint levels use the source
legends and existing Core operation mapping. Section and document source totals
must reconcile; Parts/Position appendices are evidence joins, not new charges.
Original bytes retain source headers/totals; no source-evidence schema or
misuse of Pegasus CalculationBreakdownJson. Source rate/VAT never invents a
chosen card or repairer VAT status.

## Expected files

| Action | Repo-root-relative path | Responsibility |
| --- | --- | --- |
| Modify | `src/Pegasus.Core/Assessment/EstimateImport.cs` | Existing canonical command owns retained-source/hash replay, typed pending OCR outcome and completed-format result |
| Modify | `src/Pegasus.Core/Assessment/RepairSpecifications.cs` | Narrow non-mutating authority check and imported-document persistence on existing store; no new store |
| Modify | `src/Pegasus.Core/Assessment/Estimates.cs` | Existing Core import authorization/normalization only; preserve ordinary Automation AiDraft/job rules |
| Add | `src/Pegasus.Infrastructure/Assessment/PdfEstimateDocumentParser.cs` | One PDF container registration, shared coordinate extraction, retained OCR words, explicit format dispatch |
| Add | `src/Pegasus.Infrastructure/Assessment/GlassEstimatePdfParser.cs` | Glass Body/Auxiliary/Paint reader, source identity/notes, reconciliation, whole-file refusal |
| Modify | `src/Pegasus.Infrastructure/Assessment/AudatexEstimatePdfParser.cs` | Reuse existing coordinate table logic under PDF container, no second PDF registration |
| Modify | `src/Pegasus.Infrastructure/Assessment/JsonEstimateParser.cs` | Adapt existing parser result contract |
| Modify | `src/Pegasus.Infrastructure/Glass/GlassEstimateXmlParser.cs` | Adapt existing parser result contract; do not alter XML time semantics |
| Modify | `src/Pegasus.Infrastructure/Glass/GlassRepairEstimateGateway.cs` | Consume explicit canonical imported/pending result; no launch/recovery redesign |
| Modify | `src/Pegasus.Infrastructure/Persistence/EfRepairSpecificationStore.cs` | Reuse persisted edit authority non-mutating before OCR/replay, and share save transaction for validated raw import |
| Modify | `src/Pegasus.Infrastructure/DependencyInjection.cs` | One JSON/XML/PDF parser set |
| Modify | `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` | Existing upload and retained-source completion callers call canonical import with submitted version/lease; no direct parse/save policy |
| Modify | `src/Pegasus.Web/Pages/Cases/Shared/_CaseEstimate.cshtml` | Only source auto-detection/pending completion UI and expected-version fields |
| Modify | `src/Pegasus.Web/Presentation/CaseWorkspaceLabels.cs` | Existing centralized concise labels |
| Modify | `src/Pegasus.Web/Mcp/AssessmentMcpTools.cs` | Real raw import and typed pending/unknown response under existing assessment scope |
| Modify | `tests/Pegasus.Core.Tests/Assessment/EstimateTests.cs` | Existing ImportRawEstimate tests: canonical source, pending/replay and unauthorized/forged-source cases; ordinary Automation SaveEstimate remains job-bound |
| Add | `tests/Pegasus.IntegrationTests/GlassEstimatePdfParserTests.cs` | Hash-bound genuine PDF/oracle lane plus isolated malformed/ambiguous evidence tests |
| Modify | `tests/Pegasus.IntegrationTests/AudatexEstimatePdfParserTests.cs` | Preserve genuine Audatex parsing under shared container |
| Modify | `tests/Pegasus.IntegrationTests/JsonEstimateParserTests.cs` | Adapt contract without changing JSON behavior |
| Modify | `tests/Pegasus.IntegrationTests/GlassEstimateXmlParserTests.cs` | Adapt contract without changing XML behavior |
| Modify | `tests/Pegasus.IntegrationTests/GlassRepairEstimateGatewayTests.cs` | Existing canonical importer fake/result adaptation |
| Modify | `tests/Pegasus.IntegrationTests/AssessmentEstimateImportWebTests.cs` | Prove actual Web caller, pending completion, immutable source, no duplicate source/draft |
| Modify | `tests/Pegasus.IntegrationTests/AssessmentPersistenceIntegrationTests.cs` | Actual runtime-role canonical import; replay and forged source refusal |
| Modify | `tests/Pegasus.IntegrationTests/AutomationAssessmentIngressTests.cs` | Real Automation import caller, scope/lease/source negatives, not fake-only proof |
| Modify | `tests/Pegasus.IntegrationTests/ProductionCompositionTests.cs` | Exactly one PDF container and real parser reachability |
| Modify | `docs/frd/frd-06-vehicle-and-engineering-evidence.md` | Raw PDF behavior, preserved source facts, whole-file refusal and pending completion |
| Modify | `docs/frd/frd-10-mcp-automation-and-actor-boundary.md` | Explicit pending/unknown import result; no acceptance authority change |
| Modify | `docs/design/test-ui/pages/case-details--*.html` | Root-generated routed UI snapshot artifacts |
| Modify | `docs/design/test-ui/index.html` | Root-generated snapshot index |

## Do not modify

- docs/operator-notes.md
- corpus/**
- src/Pegasus.Core/Intake/**
- src/Pegasus.Infrastructure/Intake/**
- src/Pegasus.Infrastructure/Persistence/EfIntakeOcrOperationStore.cs
- src/Pegasus.Infrastructure/Persistence/Migrations/**
- infra/**
- src/Pegasus.Web/wwwroot/js/**

## Constraints

No new package, runtime, queue, orchestration service, source schema, glyph
codec or generic parser framework. Genuine local PDFs and OCR fixtures remain
ignored evidence, not committed test data. No live provider/cloud calls in this
lane. Root is the sole heavy verifier; no author builds/tests or duplicate CI
runs. Preserve ENG-041, DOCS-020 and all unrelated work.

## Ordered steps

1. Extend the existing parser contract and reuse Audatex's coordinate reader
   under one PDF container; add the Glass reader and positive format routing.
   Name actual source route in completed result. Use TICK-041's helper only
   for its positively qualified pages. Unknown format, ambiguous identity,
   money/time/mapping omissions and internal source disagreement reject whole.
2. Wire pending/completed OCR into canonical ImportRawEstimate and its typed
   outcome. Add the narrow existing-store imported-document entry using the
   same save transaction and provenance writer. Preserve normal SaveEstimate
   restrictions, lease/CAS, hash replay and Engineer-only Current acceptance.
3. Replace Web direct parse/save with canonical retained-source import and
   durable-source Complete import action. Adapt Glass XML completion and MCP
   to typed outcomes. Prove actual authorization and forged-source negatives;
   no fake importer alone can demonstrate MCP delivery.
4. Assemble local hash-checked full-line oracles from all five originals and
   the captured retained YL OCR result. Add focused Core/SQL/Web/format tests;
   prove net-hour and repeated-table hazards and root-generated minimal Case
   snapshots. Update governing behavior docs and report every attempt.

## Acceptance checks

- Four text PDFs produce complete reviewed rows without OCR; YL's retained
  result produces its reviewed fifth oracle. False font/format triggers refuse
  or parse normally, never charge unnecessary OCR.
- The four independently checked full-row oracles are indexed at
  pegasus_pack/current/glass-row-oracles-readable.md; compare all main rows,
  included operations, contextual parts joins and source identity, not totals
  alone. The older section-level JSON is context, not a competing oracle.
- Different Case/occurrence/version/hash/length, wrong scope/actor, stale lease,
  forged provider save, ambiguous PDF, malformed amount and low-confidence
  required evidence all fail closed without partial Draft rows.
- Pending/Unknown reload and completion preserve the same source and operation;
  no second custody artifact, OCR request, estimate or old edit is produced.
- Actual Web and MCP callers use the same Core command; existing Glass XML,
  JSON and Audatex imports remain valid, as do Engineer-only Current rules.
- Source-evidence bytes/hash remain immutable; original totals do not replace
  selected card/VAT arithmetic. No source schema or live provider action.

## Source-oracle dispositions

The four readable oracles cover 244 main rows (58 included/unpriced) and 65
parts rows across 17 pages, independently inspected before parser implementation.
Preserve printed blanks as blanks, genuine zero rates as zero and the PDF's
already-net hours. Do not deduplicate LT72PYX's separately charged B74/B82 camera
reset rows, invent the clipped end of LG73ZCJ A40, or equate a guide code with
a manufacturer part number. Retain the original source for Engineer review.

VX21TZD A02 has different printed guide IDs in the main and position appendices.
Do not silently substitute one for the other or fabricate an authoritative join.
The main-table observation and original appendix both remain source evidence;
if the existing line representation requires choosing a conflicting identity,
surface that bounded source issue before code-ready instead of hiding it in a
passing oracle. Parts appendices supplement rows only through unambiguous
ordered/contextual joins and never add a second charge.

YL69YFO's real retained OCR and reviewed fifth oracle remain acceptance work.
The four readable parsers and canonical pending/completion callers may be
implemented while PLAT-065 activation is pending, but no all-five/provider PASS
or Done claim is permitted before the actual source-backed result is checked.

## Commands

Author: git diff --check only, scoped read-only inspections and local fixture
hash census. Root uses the established locked restore/Release build lane once.
Suggested focused tests (existing classes plus new Glass PDF class):
FullyQualifiedName~Pegasus.Core.Tests.Assessment.EstimateTests
FullyQualifiedName~GlassEstimatePdfParserTests|FullyQualifiedName~AudatexEstimatePdfParserTests|FullyQualifiedName~JsonEstimateParserTests|FullyQualifiedName~GlassEstimateXmlParserTests|FullyQualifiedName~AssessmentEstimateImportWebTests|FullyQualifiedName~AutomationAssessmentIngressTests|FullyQualifiedName~ProductionCompositionTests
Select new canonical-import methods from AssessmentPersistenceIntegrationTests;
reuse existing Glass gateway capture/result cohort where its contract changed.
Root selects minimal case-details default/unavailable/conflict capture cases,
then Update-TestUiSnapshots -Verify -SkipCapture -Scope case-details and
Test-UiCatalogue. Full-line genuine fixture lane is explicit, bounded to five
PDFs, and cannot report a missing local source as PASS.

## Failure and deviation rules

Stop for missing exact TICK-041 API/ADR, mismatched source hash, unresolved required source identity,
an incomplete oracle for a claimed passing sample, failing tests, new schema/permissions/dependencies or overlapping
unmerged UI changes. Record non-PASS; never weaken a money/provenance assertion
or call every unreadable parse an OCR case. New work belongs to its owner.

## Stop condition

After source contract and ENG-041 integration are resolved, obtain a fresh
execution packet/worktree. Author then stops at code-ready handoff before
PR/Review until root supplies focused runtime evidence. No self-review, merge,
deployment or subsequent ticket work is authorized by this plan.
