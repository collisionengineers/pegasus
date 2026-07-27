# Accepted finding: suggestion-first image analysis and VRM recognition

**Operator decision:** Accepted in narrowed form on 2026-07-24. `fast-alpr` is recorded as a potential future vehicle-registration recognition candidate, not as the selected `Next`/`unallocated` engine.

**Legacy sources dealt with:** ADR-0009 (`../dealt-with/accepted/0009-image-processing/docs/adr/0009-image-processing-suggestion-first.md`) and its direct image-analysis ticket bundle (`../dealt-with/accepted/0009-image-processing/README.md`).

This report accepts a future product and evidence boundary. It does not move automated VRM reading or AI/vision into the `0.1.0-alpha.1`, select a provider or model, authorise image upload to any external service, or prove an implementation.

## Accepted finding

If CollisionSpike later adds automated vehicle-registration recognition, image-role classification, person/reflection warnings, or other image analysis:

- every automated result is a suggestion associated with a specific retained source-image occurrence;
- staff make the final case-data, image-role, readiness, matching, and correction decisions;
- a suggestion must never silently create or identify a case, overwrite a confirmed VRM or other case field, select an EVA image, or satisfy a readiness gate;
- the application records the task, provider or engine, model and version where applicable, timestamp, output and confidence where supplied, failure or unknown outcome, and the staff disposition separately from confirmed case data; and
- activation requires explicit data-protection approval, representative evaluation, understood residency and cost, and a reversible adapter boundary.

This narrows the predecessor's useful suggestion-first principle to current `Next`/`unallocated` ownership. Business decisions remain in a named Core use case; an Infrastructure image-analysis adapter may return typed observations but cannot decide workflow.

## `fast-alpr` as a potential candidate

`fast-alpr` is worth retaining on the future candidate list because it combines plate localisation with character recognition and can potentially run locally without sending vehicle images to a remote vision service. Those properties could reduce unrelated-scene-text false positives and external image-data exposure.

It is **not selected or planned as a `Next`/`unallocated` dependency**. The predecessor TKT-017 benchmark (`../dealt-with/accepted/0009-image-processing/docs/tickets/done/TKT-017-ai-reg-ocr/evidence/reg-ocr-benchmark.md`) demonstrated useful decision-layer risks, including whole-image scene-text false positives, but explicitly did not measure raw recognition accuracy on a representative labelled Collision Engineers vehicle-photo cohort. Claims that its route was already live describe the predecessor, not `Next`/`unallocated`.

Before any future selection, `fast-alpr` must be compared with then-current alternatives using authorised, representative evidence covering:

- readable, partially visible, angled, low-light, newer UK and private plates;
- images with no plate, unrelated text, more than one vehicle, and visible-but-unreadable plates;
- exact-read accuracy, false positives, uncertainty and confidence behaviour;
- latency, resource use, packaging, operating-system support, maintainability, licence and security posture; and
- data residency, data-protection impact, operational cost and failure recovery.

If selected later, it belongs behind an engine-neutral Infrastructure adapter called by one Core-owned use case. Its output remains a suggestion and never becomes case identity without an authorised staff decision. Azure Document Intelligence remains the currently accepted OCR route for persisted scan-like PDF pages; this finding does not select it as a fallback for ordinary vehicle photographs.

## Current `Next`/`unallocated` position

The [operator capability overview](../../operator-notes/product-requirements/required-capabilities.md) records vehicle-registration OCR as a required product capability, while marking in-app AI, guided capture and image/vision address assistance outside the `0.1.0-alpha.1`. The settled [questionnaire](../../history/product/project-discovery-questionnaire.md) and [remaining requirements](../../product/qdos-alpha-gap.md) place automated VRM OCR/VLM and AI/vision beyond the `0.1.0-alpha.1`. The accepted finding therefore preserves eventual scope without changing the current release boundary.

Current accepted architecture separates the two concerns:

- [ADR-0001](../../architecture/decisions/ADR-0001-hybrid-pdf-extraction.md) and [ADR-0005](../../architecture/decisions/ADR-0005-multiformat-intake-assets.md) permit targeted Document Intelligence OCR only for persisted scan-like PDF page candidates and keep ordinary images out of OCR.
- The [source-custody plan](../../history/plans/remainder-delivery/integrations/source-custody-and-document-processing.md) keeps AI/vision and VRM OCR deferred, retains engine-neutral source identities, and deliberately adds no dormant model, client, flag, queue, or widened OCR route.
- The [vehicle and EVA plan](../../history/plans/remainder-delivery/integrations/vehicle-data-and-eva-export.md) permits future typed, staff-visible suggestions but forbids silent overwriting of case data.

The current evidence state is therefore **Planned/deferred**, not implemented or called. The Development-only `/Intake/Upload` path retains ordinary image evidence; no current `Next`/`unallocated` VRM-recognition or image-analysis caller is established by these legacy files.

## Differences not accepted from the predecessor

The following legacy choices are not adopted:

- `fast-alpr` as the engine of record or Document Intelligence as its photograph fallback;
- a particular Foundry or GPT vision deployment for image role, reflection, location, same-vehicle, or registration-visible decisions;
- predecessor routes, services, database fields, feature flags, backfill jobs, orchestration, live-resource claims, or automatic EVA-readiness transitions;
- treating a model-produced role or registration-visible value as already accepted case evidence; and
- using the predecessor's small decision-layer harness as proof of raw model accuracy or `Next`/`unallocated` suitability.

The legacy statement that original images remain immutable also differs from current `Next`/`unallocated` policy. Current `Next`/`unallocated` retains every previous version, uses logical removal, records revisions in permanent action history, and makes files application-read-only when a case closes until an authorised reopen. Immutable source identity and retained history are required; irreversible evidence immutability is not.

## Deferred-capability impact

This accepted boundary preserves later VRM OCR, image/vision assistance, guided capture, inspection-address suggestions, EVA image assistance, and alternative providers through stable source-image identities and a narrow typed adapter. None of those capabilities, their models, data stores, routes, flags, background jobs, or cloud resources are being built now.

Activation requires a direct product-scope decision, accepted data contract, representative evaluation, data-protection and residency approval, cost/licence review, a real intended caller, and—if the architecture boundary changes—an accepted current `Next`/`unallocated` ADR.
