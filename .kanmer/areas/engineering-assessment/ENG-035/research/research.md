# Research — ENG-035 (2026-09-02, gpt-5.6-terra xhigh, wrapper-checked)

## Wrapper check (Claude, 2026-09-02)

Codex ran read-only in `.worktrees/research` at `cad00be9` (origin/dev);
the checkout was clean afterwards. The wrapper re-ran these VERIFIED claims
against the main checkout and confirmed every one:

- The `CK_CaseAssessmentFields_FieldPath` check constraint is generated from
  `AssessmentVocabulary.Definitions.Keys`
  (`src/Pegasus.Infrastructure/Persistence/AssessmentModelConfiguration.cs:11-21`)
  and the initial migration carries the literal 34-path list
  (`Migrations/20260803205759_SendToAiAssessmentToolset.cs`), so every new
  path needs a migration even with no new table.
- Mockup severity ids are `light_moderate` / `moderate_heavy`
  (`Pegasus_UI_v2_src/src/03-labels.js:98`) against Core's
  `light_to_moderate` / `moderate_to_heavy`.
- The report projection validates the accepted signatory tuple from
  `engineer.name/qualifications/signature`
  (`src/Pegasus.Core/Reports/AssessmentReportProjection.cs:117-128`).
- Readiness requires the scalar impact fields
  (`src/Pegasus.Core/Assessment/AssessmentPolicy.cs:233-239`).
- Diagram zone ids (`23-damage-diagram.js:3-16`), the `underside`,
  `interior`, `mechanical` chips (`22-case-engineer.js:5-6`) and the mockup
  equity formula (`05-state.js:120-132`) read as stated.
- `grep -rn -E "\bD39\b|impact_location" docs/` returns nothing: the
  Phase 0 docs chore has not yet recorded D29–D43.
- The template is embedded from
  `docs/design/assets/report-renderer/templates/assessment_report.scriban`,
  not from `src/Pegasus.Infrastructure/Reports/**` as the ticket brief
  assumed; the Files document reflects the real path.

Wrapper caveat on the ASSUMED storage shape: a JSON list of at most 17 zone
entries also fits one 4,000-character `CaseAssessmentFields` value, so the
"one structured value per zone path" proposal is a provenance/derivation
choice, not a size necessity. The plan decides between the two shapes; both
stay inside the existing field map and both need the check-constraint
migration.

## Evidence basis

- **VERIFIED** — this is a clean detached checkout at `cad00be`:
  `git status --short --branch; git log -1 --format=...`.

- **VERIFIED** — `CLAUDE.md` is a symlink to `AGENTS.md`; the repository
  requires one Core policy owner and one list per concept:
  `ls; sed -n '1,1600p' CLAUDE.md; sed -n '1,1600p' AGENTS.md`;
  `rg -n -i 'one list per concept' AGENTS.md`.

## Governing documents

- **VERIFIED** — link these three governing FRDs to ENG-035:

  - `docs/frd/frd-06-vehicle-and-engineering-evidence.md`
    — engineering facts, findings, vehicle evidence, and retained assessment
    evidence; see lines 12-17 and 161-182.

  - `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md`
    — the report snapshot, settlement meanings, fee note, and report entry
    point; see lines 21-31 and the Reports section.

  - `docs/frd/frd-10-mcp-automation-and-actor-boundary.md`
    — Automation Actor writes through ordinary Core commands and the MCP
    tool boundary; see lines 30-54 and 57-93.

  Command: `sed -n '1,420p' docs/frd/frd-06-vehicle-and-engineering-evidence.md;
  sed -n '1,460p' docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md;
  sed -n '1,400p' docs/frd/frd-10-mcp-automation-and-actor-boundary.md`.

- **VERIFIED** — the governing docs do not yet contain literal decisions
  D29-D43, nor `impact_location` or `impact_severity`. They contain only
  existing generic damage and settlement material. Phase 0 has therefore not
  recorded D39-D43 in the governing docs.

  Command: `rg -n -i -e '\bD29\b|...|\bD43\b' docs/;
  rg -n -i -e '\bimpact_location\b' -e '\bimpact_severity\b' docs/`.

## Current Core vocabulary and policy

- **VERIFIED** — `AssessmentVocabulary` contains exactly 34 persisted,
  closed paths. Its definition contains path, type, maximum length, finding
  status, positivity, and optional codes, but no presentation label:
  `src/Pegasus.Core/Assessment/AssessmentContracts.cs:20-137`.

  | Value kind | Paths |
  | --- | --- |
  | Text | `vehicle.year`, `vehicle.vin`, `vehicle.fuel`, `narrative.nature_of_incident`, `rates.card`, `assessment.unroadworthy_reason`, `narrative.history_check`, `narrative.engineers_comments`, `engineer.name`, `engineer.qualifications`, `engineer.signature`, `fee.description_lines`, `statement_of_truth` |
  | Enumerated | `vehicle.vehicle_type`, `vehicle.mileage_source`, `vehicle.condition`, `assessment.impact_severity`, `assessment.impact_location`, `rates.class`, `assessment.outcome`, `assessment.legal_status`, `assessment.category` |
  | Whole number | `vehicle.engine_cc` |
  | Money | `assessment.values.retail`, `assessment.values.trade`, `assessment.values.engineer`, `costs.recovery_charge`, `costs.storage_charge`, `assessment.salvage_value`, `fee.agreed_fee` |
  | Flag | `rates.manufacturer_approved`, `rates.regional_uplift`, `costs.repairer_vat_registered` |
  | Date | `incident.assessed` |

- **VERIFIED** — current impact codes are
  `front`, `left_front`, `right_front`, `left_side`, `right_side`, `rear`,
  `left_rear`, `right_rear`, `roof`, `underside`, `wheel`, `interior`,
  `mechanical`, and `multiple`. Severity codes are `light`,
  `light_to_moderate`, `moderate`, `moderate_to_heavy`, and `heavy`:
  `src/Pegasus.Core/Assessment/AssessmentContracts.cs:83-91`.

- **VERIFIED** — no `AssessmentFieldPath` type exists. Runtime consumers are
  `AssessmentPolicy`, `EfCaseAssessmentStore`, the EF model constraint,
  report projection, valuation write, AI-job estimate precondition, the
  Assessment page, and generic MCP tools. Tests exercise policy, report
  projection, persistence, and MCP ingress.

  Command: `rg -n -i -e 'AssessmentVocabulary' -e 'AssessmentFieldPath' -e
  'impact_location' -e 'impact_severity' src/ tests/ --glob '!**/Migrations/**'`.

- **VERIFIED** — unknown paths fail closed through
  `AssessmentVocabulary.Definitions`; case-owned paths fail closed with an
  instruction to use case-detail editing:
  `src/Pegasus.Core/Assessment/AssessmentPolicy.cs:26-77,111-120`.
  Existing tests prove both cases:
  `tests/Pegasus.Core.Tests/Assessment/AssessmentPolicyTests.cs:60-90`.

- **VERIFIED** — impact location and severity are currently independently
  saved scalar values; no Core calculation derives either one. The current
  report readiness requires both scalar fields:
  `src/Pegasus.Core/Assessment/AssessmentPolicy.cs:233-239`.

- **VERIFIED** — existing computed-value conventions are Core static methods:
  `EstimateTotals.Compute` owns estimate arithmetic
  (`src/Pegasus.Core/Assessment/Estimates.cs:67-91`), while
  `AssessmentReportProjection.CostsOf` consumes it
  (`src/Pegasus.Core/Reports/AssessmentReportProjection.cs:82-98`).
  This is the appropriate shape for a pure Core damage-summary calculation;
  not a front-end calculation.

## Storage and migration boundary

- **VERIFIED** — assessment values are rows in `CaseAssessmentFields`, keyed
  by `(CaseId, FieldPath)`, with one `nvarchar(4000)` value and provenance:
  `src/Pegasus.Infrastructure/Persistence/AssessmentEntities.cs:9-19`;
  `src/Pegasus.Infrastructure/Persistence/AssessmentModelConfiguration.cs:11-36`.

- **VERIFIED** — `EfCaseAssessmentStore` loads the field map, merges a save,
  validates the merged state, and writes each changed row through
  `AssessmentFieldWriter`:
  `src/Pegasus.Infrastructure/Persistence/EfCaseAssessmentStore.cs:49-191`;
  `src/Pegasus.Infrastructure/Persistence/AssessmentFieldWriter.cs:12-57`.

- **VERIFIED** — the database check constraint is generated from
  `AssessmentVocabulary.Definitions.Keys`; the initial migration contains the
  current literal list. Adding paths requires a serialized migration and model
  snapshot update, even though the EF configuration source itself need not
  change:
  `src/Pegasus.Infrastructure/Persistence/AssessmentModelConfiguration.cs:11-32`;
  `src/Pegasus.Infrastructure/Persistence/Migrations/20260803205759_SendToAiAssessmentToolset.cs:55-72`;
  `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs:1146-1152`.

- **ASSUMED** — no new table is necessary. D39 bounds the damage list to
  named zones, and the mockup toggles a zone rather than allowing duplicate
  entries. Store one validated structured damage value per closed zone path in
  the existing field map, then derive the two headline paths in Core before
  the generic writer persists them. This avoids a new table while retaining
  per-zone provenance (see the wrapper caveat above on the size argument).

  Evidence for the bounded, unique mockup list:
  `C:/Users/PC/Downloads/Pegasus_UI_v2_src/src/22-case-engineer.js:15-19`.

## Web, labels, MCP, and reports

- **VERIFIED** — the current `/Cases/{id}/Assessment` page is an old
  Assessment workspace centred on evidence, estimates, and report-draft
  controls. It does not render a vocabulary-driven assessment form; its only
  direct vocabulary reads are the Engineer's Value and display projections:
  `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml:1-16,440-532`;
  `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs:257-296,438-460`.

- **VERIFIED** — `OperatorLabels` has no assessment-field label catalogue.
  Existing Assessment-page labels are largely literal Razor strings; renderer
  labels are literal Infrastructure strings. `AssessmentVocabulary` is not a
  label store:
  `src/Pegasus.Web/Presentation/OperatorLabels.cs:34,784`;
  `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml:440-532`;
  `src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs:157-170`.

- **ASSUMED** — new operator-facing labels belong in
  `src/Pegasus.Web/Presentation/OperatorLabels.cs`, per the EPIC constraint,
  not in Core vocabulary. ENG-035 must not take that shared-locked file:
  ENG-029/ENG-034 own the new Case-section UI. Report template wording remains
  an Infrastructure/template concern because Infrastructure cannot reference
  Web presentation code.

- **VERIFIED** — `pegasus_assessment_update` takes
  `Dictionary<string, string?>`, describes values as closed field-path
  vocabulary, and delegates directly to `ISaveAssessment`. It enumerates no
  allowed paths; adding vocabulary paths changes its accepted surface without
  editing this file:
  `src/Pegasus.Web/Mcp/AssessmentMcpTools.cs:323-372`.

- **VERIFIED** — `AiJobOperations` consumes only the confirmed Engineer's
  Value to form an Estimate job; it does not own assessment paths:
  `src/Pegasus.Core/AiWork/AiJobOperations.cs:251-314`.

- **VERIFIED** — report projection currently maps vehicle basics, scalar
  impact values, values, outcome/salvage, estimate-derived costs, narratives,
  signatory fields, and fee fields into `AssessmentReportSnapshot`:
  `src/Pegasus.Core/Reports/AssessmentReportProjection.cs:150-210`.

- **VERIFIED** — the renderer populates a Scriban context from that snapshot.
  `assessment_report.scriban` is embedded directly from
  `docs/design/assets/report-renderer/templates/assessment_report.scriban`;
  no new renderer port or project-file edit is needed:
  `src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs:33-75,157-170`;
  `src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj:43-48`;
  `docs/design/assets/report-renderer/templates/assessment_report.scriban:5-12`.

- **VERIFIED** — the existing report reads `engineer.name`,
  `engineer.qualifications`, and `engineer.signature` from vocabulary and
  validates the existing accepted signatory tuple. Do not add D31's Case
  sign-off Engineer to assessment vocabulary:
  `src/Pegasus.Core/Reports/AssessmentReportProjection.cs:117-128,171-175`;
  `src/Pegasus.Core/Reports/AssessmentReportRendering.cs:125-155,231-245`.

## Mockup comparison and gap list

- **VERIFIED** — the mockup case shape contains vehicle extras, `damage.impacts`
  with zone/severity/type/note, tyres and belts, unrelated damage, material
  transfer, settlement figures, salvage logistics, and report fee lines:
  `C:/Users/PC/Downloads/Pegasus_UI_v2_src/src/04-fixtures.js:10-18`;
  `C:/Users/PC/Downloads/Pegasus_UI_v2_src/src/22-case-engineer.js:3-10,97-121`.

  | Area | Already available | ENG-035 gap |
  | --- | --- | --- |
  | Vehicle | VIN, engine, fuel, year, type, condition | VIN check, transmission, colour, body, tax/MOT expiry, airbags, fault codes, temporary-repair facts |
  | Damage | Scalar impact location/severity | Per-zone list, type/note, four wheel zones, tyre/belt state, spare, centre belt, unrelated damage/deduction, material transfer |
  | Settlement | Outcome, category, salvage value, recovery charge, storage charge, fee and fee-description lines | Excess, global betterment, claimant VAT, reserve, hire, storage-per-day, diminution, delays, salvage logistics, Core-derived equity and ratios |
  | Report | Existing scalar impact sentence and fee note | Projection and rendering of the expanded vehicle, damage, and settlement data |

- **VERIFIED** — repair duration is already the Current estimate's
  `EstimateDetails.RepairDays`; it must be reused rather than duplicated as a
  settlement vocabulary value:
  `src/Pegasus.Core/Assessment/Estimates.cs:7-16,137-140`;
  `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml:450,532`.

- **VERIFIED** — valuation adjustments, rationale, and revaluation history are
  outside ENG-035. Report-image crop/role/order and D31 sign-off are also
  outside the ticket, as stated by the mockup gaps and the supplied lane
  boundary:
  `C:/Users/PC/Downloads/Pegasus_UI_v2_notes.md:71-107`.

## Zones and derived values

- **VERIFIED** — the current Core body-zone codes map directly to mockup SVG
  IDs: `front`, `left_front`, `right_front`, `left_side`, `right_side`,
  `rear`, `left_rear`, `right_rear`, and `roof`. The diagram instead has
  four wheel IDs (`wheel_rf`, `wheel_lf`, `wheel_rr`, `wheel_lr`), while
  `underside`, `interior`, and `mechanical` are extra non-SVG chips.
  `multiple` is headline-only:
  `src/Pegasus.Core/Assessment/AssessmentContracts.cs:85-91`;
  `C:/Users/PC/Downloads/Pegasus_UI_v2_src/src/23-damage-diagram.js:3-32`;
  `C:/Users/PC/Downloads/Pegasus_UI_v2_src/src/22-case-engineer.js:5-6`.

- **VERIFIED** — the mockup severity identifiers are
  `light_moderate` and `moderate_heavy`, which differ from the current Core
  `light_to_moderate` and `moderate_to_heavy` codes:
  `C:/Users/PC/Downloads/Pegasus_UI_v2_src/src/03-labels.js:98-99`;
  `src/Pegasus.Core/Assessment/AssessmentContracts.cs:83-85`.

- **VERIFIED** — mockup-only `settlementFigures` calculates
  `equity = engineerValue - (repairCost - betterment) - salvage`, and exposes
  repair-cost ratios in the browser. No equivalent Core calculation exists:
  `C:/Users/PC/Downloads/Pegasus_UI_v2_src/src/05-state.js:120-132`;
  `C:/Users/PC/Downloads/Pegasus_UI_v2_src/src/22-case-engineer.js:93-110`.

## Existing tests to extend

- **VERIFIED** — `AssessmentPolicyTests` already prove unknown-path rejection,
  case-owned rejection, and every enumerated-code round trip:
  `tests/Pegasus.Core.Tests/Assessment/AssessmentPolicyTests.cs:60-90`.

- **VERIFIED** — `AssessmentReportProjectionTests` constructs the existing
  confirmed-field projection, including scalar impact fields:
  `tests/Pegasus.Core.Tests/Reports/AssessmentReportProjectionTests.cs:10-17,194-225`.

- **VERIFIED** — integration coverage exists for persistence, MCP assessment
  updates, and rendered report PDFs:
  `tests/Pegasus.IntegrationTests/AssessmentPersistenceIntegrationTests.cs:20-73`;
  `tests/Pegasus.IntegrationTests/AutomationAssessmentIngressTests.cs:18,108-184`;
  `tests/Pegasus.IntegrationTests/Reports/AssessmentReportRendererTests.cs:12-51`.

## Risks

- **VERIFIED** — the migration changes a database check constraint and the
  model snapshot, so the shared migration lock must be acquired and migration
  work serialized.

- **VERIFIED** — AUTO-015 owns
  `AssessmentMcpTools.cs` overwrite/clear/confirm behaviour. ENG-035 can
  expose paths through Core vocabulary but must not alter that file.

- **VERIFIED** — ENG-034, ENG-036, ENG-029, ENG-031, CASE-029, ENG-027, and
  D31 own the supplied neighbouring UI, diagram, valuation, image, and
  sign-off boundaries.

## Operator questions

- **OPEN — operator decision required.** For multiple damaged zones, what is
  the canonical derived `impact_severity` rule, and are the current
  `*_to_*` codes or mockup `*_` codes canonical? D39 requires derivation but
  does not define aggregation.

- **OPEN — operator decision required.** Is the mockup equity formula binding
  for Core and report projection, including whether excess contributes? D41
  says only that equity is derived; the formula currently exists only in the
  mockup.
