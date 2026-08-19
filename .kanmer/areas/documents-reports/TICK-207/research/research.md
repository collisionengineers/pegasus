# Research — TICK-207: missing Audit renderer template

## Question

Does Pegasus currently have enough accepted evidence to define and implement the RPT-03 Audit report template, and if not, what exact boundary must remain closed until representative evidence is supplied or approved?

## Findings

- The operator decided on 2026-08-19 to **defer Audit rendering until a representative Audit report/template is supplied or explicitly approved**. Assessment samples must not be used to invent Audit wording. Source: resolved SIMPLI-014 `open-questions` (“all yes”), item for [[TICK-207]].
- This is a resolved deferral, not an unanswered design question. TICK-207 cannot responsibly create a template from current evidence, and RPT-03 must remain unavailable rather than shipping a guessed or closed-gated report. Sources: operator decision; AGENTS.md closed-composition and non-fabrication safety rails.
- RPT-03's accepted functional direction is narrower than a template: Audit rendering preserves conservative and maximised specifications and records their uplift, and both accepted specification versions are required. Source: `docs/capabilities.md`; [[TICK-098]].
- The operator separately accepted that the two Audit specifications are immutable, role-labelled conservative/maximised versions and that uplift is computed. [[TICK-205]] research resolves the apparent ENG-01 conflict and defines the data seam, but explicitly leaves presentation/wording to this ticket. Source: TICK-205 `research` and resolved SIMPLI-014 questions.
- `reference/rendererref1/` contains assessment-report evidence only: four assessment outcome samples/schema, design rules, fee-note content, signatures and assessment wording. It contains no representative Audit PDF, Audit schema, Audit sections, Audit statement wording, comparison layout or accepted visual example. Sources: `reference/rendererref1/DESIGN_SPEC.md`, `report_data_schema.json`, directory inventory.
- The rendererref1 schema has one `worklists` object and one assessment outcome. It cannot represent or evidence the Audit pair, source report, role-labelled totals or uplift. Reusing it would collapse required Audit evidence and falsely treat assessment behaviour as Audit authority. Source: `reference/rendererref1/report_data_schema.json`.
- The imported renderer has no Audit template or Audit-specific document model. Its catalogue includes assessment/general families such as repairable/contract repair, total loss, addendum, diminution rebuttal, roadworthy criminal, Part 35, generic expert report, valuation evidence and fee note; searches find no conservative/maximised/uplift Audit contract. Sources: `workspaces/report-renderer/src/CollisionRenderer.Core/TemplateCatalog.cs`, `Models/Documents.cs`, `AuthoringCatalog.cs`.
- The generic `expert-report` template is not an acceptable fallback. Its flexible blocks would allow a caller to compose ungoverned Audit wording/layout and would move policy into payload authoring. RPT-03 needs a fixed, accepted Audit family whose required content is enforced by Core and rendered deterministically. Sources: workspace template architecture; AGENTS.md one policy owner; EPIC-004 context.
- FRD-11 defines report-wide invariants—accepted facts/evidence, deterministic template/payload versioning, human review, immutable artifact identity/hash and correction/addendum—but does not define Audit-specific inputs, sections, wording, ordering or presentation. Source: `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md`.
- Operator notes define Audit's business meaning: another engineering firm inspected the vehicle and Collision Engineers audits/double-checks its work; an Inspection + Audit has secondary Audit identity/custody. They do not supply the missing report wording or layout. Source: `docs/operator-notes.md`.
- Current Core has Audit case/reference and original-report evidence concepts, but no report template contract. The assessment surface has one estimate-line collection today; TICK-205 identifies the required future two-specification aggregate. Sources: `src/Pegasus.Core/Cases/CaseContracts.cs`, `Documents/DocumentContracts.cs`, `Assessment/AssessmentContracts.cs`; TICK-205 research.
- A future representative/approved Audit template must identify, at minimum: report title/purpose; original report/engineer/source identity; Case and Audit references; conservative and maximised specification presentation; exact total/uplift labels and whether any percentage is shown; findings/narrative sections; legal/statement wording; signatures/qualifications; attachments/images; page furniture; fee-note relationship; mandatory versus optional fields; and representative long/minimal cases.
- Supplied evidence must be retained unchanged under `reference/`; accepted behaviour is then restated in FRD-11 and Core contracts, while governed visual/template assets belong under `docs/design/`. The reference file itself never becomes runtime policy. Sources: AGENTS.md documentation/reference rules; EPIC-004 context; docs authority chain.
- Acceptance requires more than a single screenshot/PDF: a representative source plus explicit operator approval, field-to-source mapping, fixed wording, role/conditional rules, fail-closed validation cases, visual baselines and confirmation that the two exact accepted specification versions drive the comparison. Sources: ADR-0009/0025 activation evidence; FRD-11; RPT-03.
- No existing file can be modified now to produce an honest Audit template. Creating a placeholder Scriban file, cloning the assessment layout, or adding a dormant catalogue entry would be speculative product behaviour and must not occur.

## Implications

- TICK-207 is research-complete but implementation-deferred pending external operator-supplied or explicitly approved representative Audit evidence.
- RPT-03, Audit template registration, and any Audit render action remain fail-closed/unavailable. Assessment rendering may proceed independently; it must not advertise Audit coverage.
- When evidence arrives, reopen/research the ticket against the exact supplied artifact before planning. Translate accepted behaviour into FRD-11/Core first, then implement a fixed template/model in the integrated Infrastructure renderer.
- The later plan must include a traceability table from every template field/section/wording element to accepted source or Core-owned derived value, plus minimal, maximal and correction/version visual baselines.
- [[TICK-205]] can progress the two-specification domain contract without inventing presentation. [[SIMPLI-014]] can integrate caller-backed assessment/fee-note renderer families while leaving Audit inactive.

## Open questions

- None requiring an answer now. The operator explicitly chose deferral.
- The future activation question is evidence-triggered: “Is this supplied representative Audit report/template approved as the RPT-03 authority, including its wording, layout and field rules?” It should be asked only when an actual artifact is available for review.
