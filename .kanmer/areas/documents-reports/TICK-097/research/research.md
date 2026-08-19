# Research — TICK-097: four-outcome assessment and fee-note rendering

## Question

What is the exact accepted RPT-02 assessment-rendering scope, how does it map to current Pegasus data and CollisionRenderer, and what must fail closed before the four variants, fee note and itemised repair specification can be claimed?

## Findings

- The operator approved exactly four RPT-02 outcomes on 2026-08-19: **Total loss**, **Repairable**, **Cash in lieu**, and **Contract repair**. Contract repair is a distinct fourth variant with capped agreed-repair wording. The “three outcomes” and three-value dropdown passages in rendererref1 are stale where they conflict with the four-row table and JSON schema. Source: resolved SIMPLI-014 `open-questions`; `reference/rendererref1/DESIGN_SPEC.md`; `report_data_schema.json`.
- Initial renderer activation is restricted to the caller-backed assessment and fee-note families evidenced by `reference/rendererref1/`. Unsupported workspace catalogue entries remain inactive until an owning capability and accepted evidence exist. Source: resolved SIMPLI-014 question for [[TICK-206]].
- `rendererref1` is approved evidence for its exact assessment wording, named qualifications and three supplied engineer signatures, subject to matching the selected engineer and human approval before issue. Any wording/qualification not actually present remains unavailable and fails closed; it is never invented. Source: resolved SIMPLI-014 question for [[TICK-216]].
- The four variants share one assessment data family and most layout, but have outcome-specific title, badge, highlighted figure and settlement section:
  - total loss: category badge, recommended settlement = Engineer value − salvage, and salvage section;
  - repairable: repair cost including VAT and repairable-proposition wording;
  - cash in lieu: settlement equals accepted computed repair total and cash-in-lieu wording;
  - contract repair: agreed repair total and explicit “costs cannot increase” wording.
  Source: `reference/rendererref1/DESIGN_SPEC.md`.
- The report also requires fixed vehicle/incident/assessment sections, ordered images, three names-only repair-specification lists (new parts, repairs, additional operations), statement/signature, and a fee-note page with agreed fee, VAT/total, payment details and terms. Paint operations merge into additional operations; part numbers and per-line prices do not appear in those lists. Source: rendererref1 design spec/schema and sample JSON.
- Calculation inputs are raw components only. Labour, subtotal, VAT, repair total, total-loss settlement and fee VAT/total are computed once with decimal/round-half-up rules; repairer VAT registration changes the VAT basis (full subtotal versus parts and paint only). Derived totals and settlement narrative must not be caller-entered or independently recomputed in a template. Source: rendererref1 design spec/schema; RPT-01; [[TICK-094]] research.
- `rendererref1/report_data_schema.json` is evidence, not the runtime policy owner. Its accepted behaviour must be restated in FRD-11 and typed Core contracts/readiness. Infrastructure serializes the accepted Core snapshot into the integrated renderer; it must not interpret business outcomes, select values or calculate totals. Sources: EPIC-004 context; ADR-0025; repository authority model.
- The current Core assessment surface already follows rendererref1-like paths and includes the four outcome codes, roadworthiness/reason, values, salvage, history, engineer identity/signature, fee and statement fields. It also retains confirmation actor/time and ordered estimate lines. Sources: `src/Pegasus.Core/Assessment/AssessmentContracts.cs`; [[TICK-094]] research.
- Existing `AssessmentPolicy.EvaluateReadiness` is the policy seam to extend, not duplicate. [[TICK-092]] found that it already blocks unconfirmed assessment fields/estimate lines and names report-content requirements, but the projection is not yet an immutable outcome-specific render snapshot bound to exact source versions/custody.
- RPT-02 depends on three already-researched domain capabilities:
  - [[TICK-092]]/CASE-31: one derived immutable accepted case/engineering snapshot;
  - [[TICK-093]]/ENG-01: one accepted versioned canonical ordinary repair specification with source provenance and lines mapped to the three report sections;
  - [[TICK-094]]/ENG-02: named-Engineer-confirmed outcomes/values and Core-derived economics/narratives.
  None may be replaced with renderer-owned data.
- Current estimate lines are a single replace-all ordered collection with technical line types, price/work-unit fields and confirmation provenance. They do not yet have a canonical repair-specification identity/version/source route or explicit stable mapping to the three display sections. Source: Assessment contracts; TICK-093 research.
- The accepted render input must also compose case-owned principal/address/reference, claimant, incident and vehicle facts, assessment method/location, current custodied ordered photos and selected accepted Engineer identity. Current assessment projection alone does not own all of these facts, and copying them into a second editable “report record” would violate one-owner rules. Sources: rendererref1 schema; TICK-092; SIMPLI-014 research.
- Outcome-specific readiness must fail closed:
  - every variant needs complete accepted common facts, exactly one accepted ordinary repair specification, calculation inputs, report images, engineer/signature and fee data;
  - total loss additionally needs accepted category and salvage value;
  - unroadworthy additionally needs an accepted reason;
  - physical assessment needs location; image-based follows its fixed section rule;
  - missing, unconfirmed, ambiguous, stale, mismatched or uncustodied input prevents render.
  Sources: rendererref1 schema/design; Core fail-closed invariants.
- The imported renderer does not currently implement the accepted rendererref1 job contract directly. Its catalogue has fixed/general families including separate `repairable-contract-repair-report`, `total-loss-report`, `fee-note` and generic `expert-report`, backed primarily by shared expert/fee Scriban templates and authoring presets. A deliberate fixed assessment adapter/template is needed; generic blocks must not become a way to author policy in payloads. Sources: workspace `TemplateCatalog.cs`, `AuthoringCatalog.cs`, `Models/Documents.cs`, templates.
- The report and fee note must bind the same immutable accepted source snapshot and generation transaction so case/principal/reference/Engineer/fee data cannot drift between them. Whether represented as one PDF with a fee-note page or two retained artifacts, identities and hashes must make the relationship explicit and earlier versions immutable. The approved design evidence shows fee-note presentation, but delivery/issue remains separate. Sources: rendererref1 design; FRD-11 finality; EPIC-004 context.
- Generation is not report approval, sending, external receipt, invoicing/accounting completion or case closure. Human review remains required before issue, and exact Sent evidence belongs to later delivery capability. Sources: FRD-11; [[DOCS-001]]; capability boundaries.
- [[SIMPLI-014]] integrates the engine/port; DOCS-001 supplies the real complete-assessment caller, idempotent job/result identity and custody. TICK-097 owns the four-variant functional behaviour and representative evidence, not Azure runtime deployment.
- Acceptance evidence should render the four supplied representative jobs/PDF families, plus fail-closed/minimal/maximum cases: both VAT bases, roadworthy/unroadworthy, category requirements, physical/image-based, long worklists, ordered images, missing optional text, invalid/unconfirmed data, duplicate/retry and correction/version binding. Generated evidence belongs under `artifacts/`, never `reference/`.

## Implications

- Restate the approved four-outcome behaviour and exact conditional rules in FRD-11 before implementing it; resolve stale “three outcome” prose without modifying supplied reference evidence.
- Build one typed Core assessment-report snapshot and calculation/readiness policy with a closed outcome vocabulary. Avoid four duplicated pipelines and avoid allowing templates to own calculations or select fallbacks.
- Use outcome-specific fixed presentation over shared components, with Contract repair explicitly distinct despite sharing most Repairable layout.
- Bind report, fee-note presentation, repair-spec version, payload/template/calculation versions, Engineer approval inputs and custody identities in immutable provenance.
- Keep Audit, diminution, addendum, generic expert-report and other unsupported catalogue entries inactive. RPT-03 remains separately deferred pending its representative template.
- Sequence implementation after/reusing TICK-092/093/094 contracts and alongside SIMPLI-014/DOCS-001 without creating parallel models.

## Open questions

- None requiring operator input. Four outcomes, assessment/fee-note activation scope, approved evidence and fail-closed treatment of absent wording are settled.
- Exact artifact packaging (one PDF containing the fee-note page versus linked report and fee-note artifacts) should follow the approved representative evidence during implementation and must preserve explicit identities/hashes either way; it must not be guessed if the samples do not prove it.
