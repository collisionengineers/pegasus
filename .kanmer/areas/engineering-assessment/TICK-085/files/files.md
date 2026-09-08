# Files — TICK-085

## Where the change lands

| Path | Why |
| --- | --- |
| src/Pegasus.Core/Assessment/EstimateImport.cs | Existing canonical command owns retained-source/hash replay, typed pending OCR outcome and completed-format result |
| src/Pegasus.Core/Assessment/RepairSpecifications.cs | Narrow non-mutating persisted authority check and imported-document persistence on existing store; no new store |
| src/Pegasus.Core/Assessment/Estimates.cs | Existing Core import authorization/normalization only; preserve ordinary Automation AiDraft/job rules |
| src/Pegasus.Infrastructure/Assessment/PdfEstimateDocumentParser.cs | One PDF container registration, shared coordinate extraction, retained OCR words, explicit format dispatch |
| src/Pegasus.Infrastructure/Assessment/GlassEstimatePdfParser.cs | Glass Body/Auxiliary/Paint reader, source identity/notes, reconciliation, whole-file refusal |
| src/Pegasus.Infrastructure/Assessment/AudatexEstimatePdfParser.cs | Reuse existing coordinate table logic under PDF container, no second PDF registration |
| src/Pegasus.Infrastructure/Assessment/JsonEstimateParser.cs | Adapt existing parser result contract |
| src/Pegasus.Infrastructure/Glass/GlassEstimateXmlParser.cs | Adapt existing parser result contract; do not alter XML time semantics |
| src/Pegasus.Infrastructure/Glass/GlassRepairEstimateGateway.cs | Consume explicit canonical imported/pending result; no launch/recovery redesign |
| src/Pegasus.Infrastructure/Persistence/EfRepairSpecificationStore.cs | Reuse CaseMutationGuard.Require without mutation before OCR/replay; share existing final save transaction and unconfirmed Automation row writer |
| src/Pegasus.Infrastructure/DependencyInjection.cs | One JSON/XML/PDF parser set |
| src/Pegasus.Web/Pages/Cases/Details.cshtml.cs | Existing upload and retained-source completion callers call canonical import with submitted version/lease; no direct parse/save policy |
| src/Pegasus.Web/Pages/Cases/Shared/_CaseEstimate.cshtml | Only source auto-detection/pending completion UI and expected-version fields |
| src/Pegasus.Web/Presentation/CaseWorkspaceLabels.cs | Existing centralized concise labels |
| src/Pegasus.Web/Mcp/AssessmentMcpTools.cs | Real raw import and typed pending/unknown response under existing assessment scope |
| tests/Pegasus.Core.Tests/Assessment/EstimateTests.cs | Existing ImportRawEstimate tests: canonical source, pending/replay and unauthorized/forged-source cases; ordinary Automation SaveEstimate remains job-bound |
| tests/Pegasus.IntegrationTests/GlassEstimatePdfParserTests.cs | Hash-bound genuine PDF/oracle lane plus isolated malformed/ambiguous evidence tests |
| tests/Pegasus.IntegrationTests/AudatexEstimatePdfParserTests.cs | Preserve genuine Audatex parsing under shared container |
| tests/Pegasus.IntegrationTests/JsonEstimateParserTests.cs | Adapt contract without changing JSON behavior |
| tests/Pegasus.IntegrationTests/GlassEstimateXmlParserTests.cs | Adapt contract without changing XML behavior |
| tests/Pegasus.IntegrationTests/GlassRepairEstimateGatewayTests.cs | Existing canonical importer fake/result adaptation |
| tests/Pegasus.IntegrationTests/AssessmentEstimateImportWebTests.cs | Prove actual Web caller, pending completion, immutable source, no duplicate source/draft |
| tests/Pegasus.IntegrationTests/AssessmentPersistenceIntegrationTests.cs | Actual runtime-role canonical import; replay and forged source refusal |
| tests/Pegasus.IntegrationTests/AutomationAssessmentIngressTests.cs | Real Automation import caller, scope/lease/source negatives, not fake-only proof |
| tests/Pegasus.IntegrationTests/ProductionCompositionTests.cs | Exactly one PDF container and real parser reachability |
| docs/frd/frd-06-vehicle-and-engineering-evidence.md | Raw PDF behavior, preserved source facts, whole-file refusal and pending completion |
| docs/frd/frd-10-mcp-automation-and-actor-boundary.md | Explicit pending/unknown import result; no acceptance authority change |
| docs/design/test-ui/pages/case-details--*.html | Root-generated routed UI snapshot artifacts |
| docs/design/test-ui/index.html | Root-generated snapshot index |

## Context files

| Path | What it tells the implementer |
| --- | --- |
| src/Pegasus.Core/Intake/IntakeOcr.cs | TICK-041 owns BeginDocumentAsync/source-context/result contract and queue/reconcile policy |
| src/Pegasus.Infrastructure/Intake/AzureDocumentIntelligenceOcr.cs | TICK-041 adapter; parser must not submit, poll or parse raw provider JSON |
| src/Pegasus.Infrastructure/Persistence/EfIntakeOcrOperationStore.cs | Existing durable source/page identity, completed evidence and no duplicate work |
| src/Pegasus.Infrastructure/Persistence/AssessmentEntities.cs | No source-header JSON; CalculationBreakdownJson is Pegasus pricing, not provider evidence |
| src/Pegasus.Core/Documents/DocumentContracts.cs | Staff Add completes exactly one mutation on non-replay; replay needs fresh submitted authority, unlike version-neutral automatic confirmation |
| docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md | Rate-card, repairer VAT and Engineer Current acceptance remain independent |
| docs/design/README.md | Whole-page drop owed ENG-033; TICK-085 must not duplicate that interaction lane |
| pegasus_pack/current/glass-row-oracles-readable.md | Independently inspected full-row evidence for four readable PDFs, exact hashes and unresolved source-identity observations |
| pegasus_pack/current/glass-pdf-research.json | Genuine original paths/hashes and historical section observations; not the full-row oracle |

## Ripple effects

Changing IEstimateDocumentParser and IImportRawEstimate result contracts requires
all known parser and canonical-import fakes to change in the same diff. Root alone builds/captures. No provider secrets or genuine source artifacts are
copied into tracked tests; local corpus lane reads a supplied root and verifies
hashes. Full line oracles and recorded OCR output remain local evidence.

## Out of scope

TICK-041 OCR/provider/worker/font-helper source; PLAT-065 provisioning/live canary;
ENG-041 Glass session recovery and estimate edit replay; ENG-033 whole-page drop
interaction/removal; rate/VAT policy redesign; source-evidence schema with no
current query consumer; report implementation; new queue, dispatcher, runtime,
provider, package or generic parser framework.
