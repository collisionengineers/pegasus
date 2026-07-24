# Parser boundary and version provenance

**Operator decision:** Rejected as v2 architecture and dealt with on 2026-07-24. The useful principles are already required by current v2; the predecessor service and engine-ownership mechanics are not adopted.

**Legacy sources dealt with:** [ADR-0004](../dealt-with/rejected/0004-parser-as-azure-function-inline.md), [ADR-0018](../dealt-with/rejected/0018-cedocumentmapper-dual-target-vendored-engine.md), and [ADR-0035](../dealt-with/rejected/0035-cedocumentmapper-engine-repository-consolidation.md).

[ADR-0022](../dealt-with/rejected/0022-retroactive-case-reconstruction.md) was subsequently rejected and dealt with as a separate predecessor workflow decision. It is not an approved parser caller or case-creation path.

## Current v2 position

### Accepted architecture

- The [.NET modular-monolith ADR](../../../architecture/decisions/ADR-0002-dotnet-modular-monolith-on-azure.md#repository-and-dependency-boundaries) assigns provider extraction and business validation to Core, document-format decoding to Infrastructure, and Function triggers plus composition to Worker. A Worker trigger calls a Core use case and contains no provider parsing.
- The [multi-format intake ADR](../../../architecture/decisions/ADR-0005-multiformat-intake-assets.md) keeps `ProcessQdosIntake` as the single Core owner used by the Development-only Web upload and the planned Worker. Core contracts remain engine-neutral.
- Raw documents remain authoritative, extracted values remain reviewable suggestions, bounded or uncertain outcomes remain visible, and no case/reference is allocated from an unsafe or unsupported result.
- A later extraction runtime boundary is not forbidden forever, but the accepted architecture requires measured scale or ownership evidence before adding another service or deployment unit.

### Current implementation and caller

The current real path is:

`POST /Intake/Qdos` -> [`QdosModel.OnPostAsync`](../../../../src/CollisionSpike.Web/Pages/Intake/Qdos.cshtml.cs) -> [`ProcessQdosIntake`](../../../../src/CollisionSpike.Core/Intake/Qdos/ProcessQdosIntake.cs) -> Core [`IQdosIntakeSourceReader`](../../../../src/CollisionSpike.Core/Intake/Qdos/QdosIntakeContracts.cs) -> Infrastructure [`MimeKitPdfPigQdosSourceReader`](../../../../src/CollisionSpike.Infrastructure/Intake/Qdos/MimeKitPdfPigQdosSourceReader.cs).

MimeKit, PdfPig, and Open XML remain Infrastructure dependencies. Provider-specific extraction and review decisions remain in Core. The Web caller is Development-only; the Worker currently has composition and telemetry but no intake trigger or parser caller. No current intake code is coupled to EVA.

## Differences from the legacy ADRs

| Legacy decision | Current v2 treatment |
| --- | --- |
| Deploy a focused parser Azure Function service | Rejected. The existing Infrastructure reader and Core use case run behind Web now and the planned thin Worker later. No separate parser service or network hop is justified. |
| Isolate Python/document dependencies | Python is not part of the accepted intake stack. Format libraries are isolated in Infrastructure without creating another runtime. |
| Give every caller the same contract | Already adopted through the single Core use case and engine-neutral reader port. A future caller must use that owner rather than call a parser service directly. |
| Exercise extraction through the real intake path | Already implemented through the Development-only Web caller. Production Worker delivery and custody remain planned, not proved. |
| Return settled EVA fields from the parser | Rejected. Intake produces reviewable business data and evidence. EVA export is a downstream adapter/use case and must not shape the document-reader contract. |
| Use the parser during retroactive reconstruction | Rejected with ADR-0022. Migration of predecessor cases or application state is explicitly outside v2 cutover scope, so reconstruction is not an approved parser caller. |
| Be idempotent, fixture-driven, observable, and non-authoritative | The intent is already covered by current source identity, bounded outcomes, retained evidence, caller tests, planned content-free telemetry, and operator review. Repository tests were not rerun for this documentation review. |
| Tolerate an extra base64 layer | Not adopted. No current transport requires this predecessor quirk; silently decoding speculative layers would weaken the explicit input contract. |
| Vendor or merge `cedocumentmapper_v2` and materialise Python copies | Rejected. v2 is a clean-room .NET implementation and does not reuse, vendor, synchronise, or package the predecessor engine. |

## Existing current-v2 provenance gap

Current [ADR-0001](../../../architecture/decisions/ADR-0001-hybrid-pdf-extraction.md) requires retaining the extractor version and independently versioning provider-specific rules. Current [ADR-0003](../../../architecture/decisions/ADR-0003-pdfpig-for-first-qdos-slice.md) also requires the adapter to record its engine and version.

The current PDF reader adds a `pdf-engine` evidence entry whose human-readable detail names `PdfPig 0.1.15`. The persisted Core intake record has no explicit extractor-engine version or provider-rule version field. This is a gap against current v2 provenance requirements, not an accepted reason to introduce the predecessor's service, Python engine, vendoring, materialised copies, or cross-language drift machinery.

The current owner should eventually provide stable, queryable version provenance through the existing engine-neutral intake contract and persistence path. The exact field shape and migration belong to that implementation slice; this report does not design them.

## Real caller and evidence still required

Future production evidence must show the authorised Worker caller translating one mailbox/queue receipt into `ProcessQdosIntake`, using the same Core rules as Web. It must prove source-occurrence idempotency, bounded format outcomes, durable source custody, visible failure, structured extractor/rule provenance, and no duplicated parsing or EVA field policy in Worker, Web, scripts, or tests.

That evidence would prove the current modular boundary. It would not prove a separate parser service, production deployment, OCR accuracy, Box custody, or operator acceptance unless those specific callers and boundaries are exercised.

## Deferred-capability impact

Targeted Azure OCR, automated DOC/MSG extraction, broader mailbox coverage, future provider formats, and possible later extraction scaling remain caller-backed increments behind the same Core contract. A new extraction deployment unit would require measured workload or ownership evidence and an accepted current ADR.

No Python runtime, parser Function, EVA-shaped parser response, predecessor-engine copy, cross-language parity layer, dormant queue, endpoint, configuration, or deployment resource is introduced by this decision.
