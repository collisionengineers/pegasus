# Files — TICK-097

## Where the change lands

| Path | Why |
|---|---|
| `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` | Normatively define four assessment outcomes, common/conditional inputs, calculations, three repair-spec lists, fee-note relationship, fail-closed rules and acceptance evidence. |
| `docs/capabilities.md` | Preserve RPT-02 allocation while clarifying approved four-outcome evidence if needed; no behaviour duplication. |
| `src/Pegasus.Core/Assessment/AssessmentContracts.cs` | Reuse/evolve approved field vocabulary and accepted repair-spec inputs; avoid a second report-only editable model. |
| `src/Pegasus.Core/Assessment/AssessmentPolicy.cs` | Extend existing readiness owner with outcome-specific, accepted-only, source/custody/version-aware checks. |
| `src/Pegasus.Core/Reports/**` | Typed immutable assessment-report snapshot, closed four-outcome contract, Core calculation/narrative policy, template selection and report/fee provenance seam. |
| `src/Pegasus.Infrastructure/Reports/**` | Map the Core snapshot to the integrated fixed assessment/fee-note renderer without business fallbacks or calculations. |
| `src/Pegasus.Infrastructure/Persistence/**` | Persist exact render snapshot/job/artifact identities through DOCS-001; retain source versions and correction lineage. |
| `docs/design/assets/report-renderer/templates/**` | Govern the accepted fixed assessment and fee-note presentation derived from approved evidence; do not activate unrelated templates. |
| `docs/design/assets/report-renderer/templates/report.css` | Reuse the single report stylesheet for approved four-variant layout. |
| `tests/Pegasus.Core.Tests/Assessment/**` and `Reports/**` | Four variants, conditional readiness, calculations/VAT, accepted-only inputs, repair-section mapping, stale/mismatch and correction tests. |
| `tests/Pegasus.IntegrationTests/Reports/**` | Deterministic template/resource rendering and report/fee-note provenance against representative evidence. |
| `artifacts/**` | Generated visual/PDF comparison evidence for the four variants and stress cases. |

## Context files

| Path | What it tells the implementer |
|---|---|
| SIMPLI-014 `open-questions` | Binding operator approval: four outcomes; rendererref1 assessment/fee-note families only; exact supplied wording/signatures with absent content failing closed. |
| EPIC-004 `context.md` | Monolith integration, Core ownership, evidence authority, immutable identity/provenance and fail-closed rules. |
| `reference/rendererref1/DESIGN_SPEC.md` | Approved layout, per-outcome wording/figures, calculations, worklist/photo and fee-note rules; contains stale three-outcome phrases explicitly overruled by operator. |
| `reference/rendererref1/report_data_schema.json` | Approved candidate input fields/conditions and raw-only calculation boundary; translate to Core rather than use as policy owner. |
| `reference/rendererref1/sample_job_*.json` and supplied PDFs | Representative four-outcome parity evidence; immutable reference inputs/outputs. |
| [[TICK-092]] research | Single accepted derived snapshot, source versions/hash and existing readiness policy. |
| [[TICK-093]] research | Canonical repair specification and mapping to new parts/repairs/additional operations. |
| [[TICK-094]] research | Engineer authority, typed outcome/value inputs and compute-once policy. |
| `src/Pegasus.Core/Assessment/AssessmentContracts.cs` | Existing four-code vocabulary, fields, confirmation provenance and estimate lines. |
| `src/Pegasus.Core/Assessment/AssessmentPolicy.cs` | Existing readiness owner to extend. |
| `src/Pegasus.Core/Cases/CaseDataContracts.cs` | Accepted case-owned facts that report snapshots consume without copying ownership. |
| `src/Pegasus.Core/Documents/DocumentContracts.cs` | Custodied source/photo identities and document types. |
| `workspaces/report-renderer/src/CollisionRenderer.Core/TemplateCatalog.cs` | Existing descriptors do not equal the approved RPT-02 contract; unsupported entries stay inactive. |
| `workspaces/report-renderer/src/CollisionRenderer.Core/Models/Documents.cs` and `AuthoringCatalog.cs` | Reusable rendering shapes/presets, but generic authoring cannot own report policy. |
| `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` | Current report finality/provenance/human-review requirements. |
| [[SIMPLI-014]] and [[DOCS-001]] | Engine integration and real caller/durable generation responsibilities adjacent to RPT-02. |

## Ripple effects

- TICK-092/093/094 define prerequisite data owners and likely overlap in Core assessment/report files; plan serially or in one coordinated lane.
- SIMPLI-014 supplies the Infrastructure renderer and canonical resources; RPT-02 must not create another rendering implementation.
- DOCS-001 binds readiness to an idempotent generation job, immutable reference/hash/provenance and custody.
- Assessment UI must display exact blockers and four outcome choices but must not equate generation with approval/issue.
- Persistence migrations, action history, calculation versions, correction handling, photo ordering/custody, package/runtime tests and visual baselines are affected.
- Audit/RPT-03 remains unavailable and must not appear as covered by the assessment template.

## Out of scope

- Audit, diminution, addendum, Part 35, roadworthy-criminal, blank letterhead, generic expert-report and valuation-evidence activation.
- Report approval, Outlook/provider sending, external receipt, invoice/accounting status or case closure.
- Glass's/Audatex/AI extraction implementation; RPT-02 consumes only accepted canonical specification versions.
- Any fabricated wording, qualifications, signatures, photos, case data or sample output.
- Modification of `reference/rendererref1/`; it is immutable supplied evidence.
- Azure deployment/cloud writes; PLAT-007 owns approved runtime deployment.
