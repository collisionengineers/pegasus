# Research — TICK-204: assessment-report outcome variants

## Question

Which assessment-report outcome variants are evidenced, how do their generated documents differ, and what must be made authoritative before the integrated Pegasus renderer can select one from accepted assessment data?

## Findings

- The roadmap already allocates **four** variants. `RPT-02` says assessment rendering covers four outcome variants and emits the fee note plus itemised repair-specification breakdown; `EXT-08` activates deterministic rendering from accepted Core-owned data, and `CASE-31` makes the accepted structured case/engineering record the common source. Source: `docs/capabilities.md`.
- The governing FRD currently requires deterministic template/payload versioning, accepted-fact provenance, human approval, immutable issued identity/hash, and correction/addendum versioning, but it does **not** name the four outcome values or their variant-specific titles, badges, figures, and wording. Source: `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md`.
- The accepted integration direction is fixed: Core owns report readiness, policy, immutable identity, and the render contract; Infrastructure adapts CollisionRenderer; Web/Worker compose the caller. The workspace is not a separate service/package/deployment. Source: `docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md` and `EPIC-004/context.md`.
- Supplied renderer evidence contains four concrete values: `total_loss`, `repairable`, `cash_in_lieu`, and `contract_repair`. The JSON Schema enum includes all four, there is one sample job and one seven-page sample PDF for each, and the design table describes each variant. Source: `reference/rendererref1/report_data_schema.json`, `sample_job_*.json`, `Sample - *.pdf`, and `DESIGN_SPEC.md`.
- The four PDFs share a stable seven-page assessment bundle shape: outcome page, findings/settlement, vehicle data and repair-cost calculation, itemised repair specification, vehicle images, statement/signature, and fee note. Their differentiating behavior is concentrated on pages 1–2:
  - `total_loss`: “TOTAL LOSS — CATEGORY x”; shows PAV, repair cost, salvage, and PAV-minus-salvage recommended settlement; includes category-specific salvage wording.
  - `repairable`: “REPAIRABLE”; shows PAV, labour hours, and repair cost; settlement says the vehicle is repairable and uses calculated repair cost.
  - `cash_in_lieu`: “CASH IN LIEU”; the red figure is the cash-in-lieu settlement, equal to calculated repair cost; wording recommends settlement by cash in lieu.
  - `contract_repair`: “CONTRACT REPAIR”; shows PAV, labour hours, and repair cost; wording records an agreed VAT-inclusive capped repair amount that cannot increase.
  Source: the four PDFs and the outcome table in `reference/rendererref1/DESIGN_SPEC.md`.
- `DESIGN_SPEC.md` is internally stale in two sentences: it calls the section “three assessment report outcomes” and later calls a three-value dropdown the “complete set,” while the immediately following table includes `contract_repair`, the schema enum includes it, and dedicated JSON/PDF samples prove it. This is a documentation contradiction, not an absence of implementation evidence.
- The existing workspace does **not** implement the supplied assessment-job schema as a typed model or a four-way outcome router. Its production catalog exposes generic `ExpertReportDocument` presets, including one combined `repairable-contract-repair-report` and one `total-loss-report`; it has no distinct cash-in-lieu catalog entry and no typed outcome enum. Source: `workspaces/report-renderer/src/CollisionRenderer.Core/TemplateCatalog.cs`, `AuthoringCatalog.cs`, and `Models/Documents.cs`.
- Existing renderer tests prove catalog consistency, generic starter deserialization/preview, placeholder handling, formatting, hashes, filenames, density, and optional Chromium rendering. They do not prove the four supplied assessment outcomes, schema validation, computed figures, variant selection, or parity with the four reference PDFs. The integration test treats missing Chromium as an early return, so it is not sufficient caller/runtime proof. Source: `workspaces/report-renderer/tests/CollisionRenderer.Core.Tests/*`.
- The reference design requires raw cost components to be supplied and labour/subtotal/VAT/repair total/settlement computed once with decimal `ROUND_HALF_UP`; total loss alone requires category and salvage. Method, legal status, mileage source, condition, impact, worklists, engineer, fee, and photos also affect bundle content. These inputs must come from accepted Core-owned assessment/case data or rendering must fail closed; the renderer must not accept precomposed outcome narratives as a second policy owner. Source: `reference/rendererref1/report_data_schema.json`, `DESIGN_SPEC.md`, FRD-11, and EPIC-004 context.
- Some supplied wording/assets remain explicitly unaccepted: salvage paragraphs for categories N/A/B/N/A, recovery/storage wording, final statement of truth, and two engineers’ qualifications. These belong to `TICK-216`; this ticket must not silently bless them while defining the outcome vocabulary. Source: `reference/rendererref1/DESIGN_SPEC.md` and `SIMPLI-014`.
- Variant definition and template-to-capability mapping are separate concerns: this ticket should settle the four outcome behaviors; `TICK-206` owns mapping templates to capabilities; `SIMPLI-014` owns the integrated Core contract/Infrastructure adapter/caller; `TICK-216` owns closed-gate handling of unaccepted wording and signatures.

## Implications

- The evidence strongly supports a four-value closed vocabulary: `total_loss | repairable | cash_in_lieu | contract_repair`. Unknown, missing, conflicting, or incomplete outcome data must not choose a template or render an accepted report.
- The durable behavior should be added to FRD-11, not inferred forever from `reference/rendererref1`; the reference tree remains evidence, not a second policy owner.
- The variant contract should define outcome-specific title, badge, key figures, settlement heading/meaning, required inputs, and generated wording while preserving one common assessment-bundle structure.
- Core should own selection/readiness and computed business figures. The renderer adapter should receive an already-authorized, versioned render request and produce deterministic bytes plus identity/hash/provenance; it must not decide the professional outcome.
- Implementation acceptance later needs four caller-backed fixtures/tests, one per value, plus fail-closed cases and parity assertions against the approved distinctions. Generic catalog tests are insufficient.
- The contradiction in the supplied design prose should be corrected only if/when reference stewardship permits; it does not justify collapsing contract repair into repairable because dedicated schema and samples distinguish it.

## Open questions

- Operator confirmation is required on whether the four-value set and variant distinctions described above are the product contract despite the two stale “three/complete set” sentences in `DESIGN_SPEC.md`.
