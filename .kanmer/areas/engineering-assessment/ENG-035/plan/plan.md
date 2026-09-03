# Plan — ENG-035 (2026-09-03, gpt-5.6-terra xhigh)

Read-only plan. Evidence verified at source commit `897db953`; ticket inputs:
research `37380DA9`, files `4F4CC866`, questions `577A8C6F`, EPIC-012
context `9E0BD72E`. The live board connector was unavailable, so execution
must recheck its gates before taking the ticket.

## Objective

Extend the closed assessment field-map vocabulary, persist Core-derived impact
fields, and project the expanded vehicle, damage, and settlement record into
the existing assessment report and generic MCP update flow.

D45 supersedes the older ticket/FRD wording: each impact JSON object has
exactly `zone`, `severity`, and `note`.

## Governing documents

- FRD-06: retain structured engineering findings and have Core derive both
  impact values. EPIC-012 D45 overrides its currently stale impact wording.
- FRD-11: project the accepted damage and settlement facts into the report;
  calculate D41 equity in Core.
- FRD-10: `pegasus_assessment_update` continues through `ISaveAssessment`;
  no MCP handler policy is added.
- D44 has no owned-file coupling; add no review state, flag, history, or gate.
- D46 is ENG-031-owned image curation; make no crop or image-selection change.

## Required vocabulary and rules

| Area | Paths / shape | Rule |
| --- | --- | --- |
| Vehicle | `vehicle.vin_checked`, `vehicle.transmission`, `vehicle.colour`, `vehicle.body`, `vehicle.tax_expiry`, `vehicle.mot_expiry`, `vehicle.airbags_deployed`, `vehicle.fault_codes`, `vehicle.temporary_repairs_possible`, `vehicle.temporary_repair_method`, `vehicle.temporary_repair_cost` | Existing scalar normalizers. |
| Damage | `damage.impacts` JSON array | Closed unique zone list; each entry has `zone`, canonical severity, and note up to 100 characters. |
| Restraints | Per-corner tyre and belt paths, plus `damage.tyres.spare` and `damage.tyres.centre_belt` | Closed snake-case codes in `AssessmentVocabulary`. |
| Other damage | `damage.unrelated`, `damage.unrelated_deduction`, `damage.material_transfer` | Existing text/money normalizers. |
| Settlement | `settlement.excess`, `settlement.betterment`, `settlement.claimant_vat_registered`, `settlement.reserve`, `settlement.hire_start`, `settlement.hire_daily_cost`, `settlement.diminution`, `settlement.repair_delays`, `settlement.report_delay`, and salvage-logistics paths | Reuse `costs.recovery_charge`, `costs.storage_charge`, existing salvage value, and current-estimate repair days. |
| Derived | `assessment.impact_location`, `assessment.impact_severity`, report-only equity | Direct caller writes fail closed. One wheel maps to existing `wheel`; two or more zones derive `multiple`; severity is the highest canonical severity. Equity is `engineer value − (repair cost − betterment) − salvage`; excess is separate. |

Financial ratios are permitted, not required by D41, so do not add them. Fee
description lines already use `fee.description_lines`; do not duplicate them.

## Expected files

| Action | Path |
| --- | --- |
| Modify | `src/Pegasus.Core/Assessment/AssessmentContracts.cs` |
| Modify | `src/Pegasus.Core/Assessment/AssessmentPolicy.cs` |
| Modify | `src/Pegasus.Infrastructure/Persistence/EfCaseAssessmentStore.cs` |
| Modify | `src/Pegasus.Core/Reports/AssessmentReportProjection.cs` |
| Modify | `src/Pegasus.Core/Reports/AssessmentReportRendering.cs` |
| Modify | `src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs` |
| Modify | `docs/design/assets/report-renderer/templates/assessment_report.scriban` |
| Add | `src/Pegasus.Infrastructure/Persistence/Migrations/*_ExtendAssessmentVocabulary.cs` |
| Add | `src/Pegasus.Infrastructure/Persistence/Migrations/*_ExtendAssessmentVocabulary.Designer.cs` |
| Modify | `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs` |
| Modify | `tests/Pegasus.Core.Tests/Assessment/AssessmentPolicyTests.cs` |
| Modify | `tests/Pegasus.Core.Tests/Reports/AssessmentReportProjectionTests.cs` |
| Modify | `tests/Pegasus.Core.Tests/Reports/AssessmentReportRenderingTests.cs` |
| Modify | `tests/Pegasus.IntegrationTests/AssessmentPersistenceIntegrationTests.cs` |
| Modify | `tests/Pegasus.IntegrationTests/AutomationAssessmentIngressTests.cs` |
| Modify | `tests/Pegasus.IntegrationTests/Reports/AssessmentReportRendererTests.cs` |

## Dependencies and exclusions

- Serialize the migration under the capacity-one migration lock.
- `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs` has a
  strict applied-migration census but is outside ENG-035's authoritative file
  list. Its owner must add the generated migration name before the canonical
  integration suite can pass. Stop if that coordination is not assigned.
- ENG-036 owns the marked-diagram component. This ticket exposes the validated
  zones to the report; it must not invent a diagram, crop behaviour, or an
  ENG-036-owned asset.
- AUTO-015 owns MCP overwrite, clear, and confirmation semantics. Do not edit
  `AssessmentMcpTools.cs`; prove the existing generic route accepts a new
  vocabulary path and rejects direct derived-field input.
- Do not modify Case Razor pages/partials, `OperatorLabels.cs`, estimate or
  valuation ownership, report-image curation, D31 sign-off files, or governing
  FRDs.

## Ordered steps

### Step 1 — Define and validate the closed Core vocabulary

- Files: `src/Pegasus.Core/Assessment/AssessmentContracts.cs`,
  `src/Pegasus.Core/Assessment/AssessmentPolicy.cs`
- Reuses: `AssessmentVocabulary.DefinitionList`,
  `AssessmentFieldDefinition`, `ValidateAndNormalize`,
  `NormalizeFieldValue`, `NormalizeValue`, and `ValidateMergedState`.
- Change: add the scalar paths and one structured `damage.impacts` value to
  the vocabulary. Normalize to canonical JSON; reject malformed JSON, unknown
  zones/severities, duplicate zones, excessive notes, unknown object members,
  and oversized serialized values.
- Change: make both scalar impact paths Core-derived-only. Derive one-zone
  location from its canonical mapping, `multiple` for two or more zones, and
  highest-zone severity using the existing severity-code order.
- Preserve: unknown and case-owned paths fail closed; existing codes, maximum
  save bound, finding-confirmation rules, and readiness requirements remain.
- Forbidden: UI labels, a second code catalogue, direct writes of derived
  fields, or any extra impact member.
- Done when: Core has one vocabulary and one normalization/derivation owner.

### Step 2 — Persist only Core-produced derived impact rows

- Files: `src/Pegasus.Infrastructure/Persistence/EfCaseAssessmentStore.cs`
- Reuses: `SaveAsync`'s serializable transaction, merged field map,
  `AssessmentPolicy.ValidateMergedState`, and `AssessmentFieldWriter.Write`.
- Change: after merging an accepted `damage.impacts` change, ask Core for the
  derived values and write or remove both derived rows through the existing
  writer and action-history path.
- Preserve: a save that does not contain `damage.impacts` leaves derived rows
  untouched; provenance, idempotency, and unchanged Automation resubmissions
  retain their existing behaviour.
- Negative cases: clearing or replacing impacts cannot leave stale impact
  rows; a direct impact-field request fails before persistence.
- Done when: accepted damage saves persist the source and both derived rows
  with normal provenance.

### Step 3 — Generate the constrained vocabulary migration

- Files: the two new `*_ExtendAssessmentVocabulary` migration files and
  `PegasusDbContextModelSnapshot.cs`
- Reuses: `AssessmentModelConfiguration`, which already builds
  `CK_CaseAssessmentFields_FieldPath` from `AssessmentVocabulary.Definitions`.
- Change: generate one migration that replaces the check constraint with the
  expanded closed path list and updates the generated snapshot.
- Preserve: no entity, table, `DbSet`, package, grant, bootstrap change, or
  data backfill. Existing rows remain valid.
- Rollback: the down migration restores the prior constraint only while no
  new-path rows exist. If such rows exist, SQL must fail transactionally rather
  than delete assessment evidence; production rollback then requires an
  explicit operator decision.
- Dependency stop: do not edit the out-of-scope migration census test; obtain
  its coordinated update before claiming the integration suite passes.
- Done when: the generated migration, model snapshot, migration-grant check,
  and coordinated census are green.

### Step 4 — Extend the immutable Core report projection

- Files: `src/Pegasus.Core/Reports/AssessmentReportProjection.cs`,
  `src/Pegasus.Core/Reports/AssessmentReportRendering.cs`
- Reuses: `AssessmentReportSnapshot`, `ReportVehicle`, `Project`,
  `BuildVehicle`, `CostsOf`, and `EstimateTotals.Compute`.
- Change: add immutable report shapes for vehicle extras, impacts, restraint
  facts, unrelated damage, material transfer, and settlement data. Append
  snapshot members with defaults to retain existing positional test fixtures.
- Change: have projection consume the Core-normalized impact data, reuse
  current-estimate repair days and repair cost, and calculate equity only in
  Core. Keep fee lines on the existing `SplitLines` route.
- Preserve: the accepted signatory tuple, report readiness, valuation and
  estimate ownership, and deterministic snapshot rendering.
- Forbidden: template arithmetic, JSON parsing outside Core policy, report
  image changes, ratio lines, or duplicate repair-duration storage.
- Done when: every new reportable value and derived equity is available from
  `AssessmentReportSnapshot`.

### Step 5 — Render the expanded report sections

- Files: `src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs`,
  `docs/design/assets/report-renderer/templates/assessment_report.scriban`
- Reuses: `VehicleRows`, `VehicleDataRows`, `Rows`, `AmountRows`, embedded
  Scriban-resource loading, and the renderer's existing display formatting.
- Change: add compact vehicle, damage, restraint, and settlement row/table
  slots to the Scriban context and template. The damage table contains only
  Zone, Severity, and Note.
- Preserve: report headings follow the existing report-template convention;
  no Web label catalogue is created or changed.
- Forbidden: explanatory copy, UI labels, template-side calculations, a
  marked-diagram substitute, or image-curation logic.
- Dependency: the full D39 marked-diagram acceptance remains with ENG-036 and
  requires its report-ready contract.
- Done when: representative values reach the rendered PDF through the
  existing `GenerateAssessmentReportDraft` caller.

### Step 6 — Prove policy, persistence, MCP ingress, projection, and rendering

- Files: all six owned test files in the Expected files table.
- Reuses: `AssessmentPolicyTests` vocabulary fixtures,
  `AssessmentReportProjectionTests.ReadyInput`,
  `AssessmentReportRenderingTests` snapshot fixture, the assessment persistence
  harness, MCP HTTP `ToolCallPayload`/lease setup, and renderer PDF extraction.
- Add assertions for canonical new-path round trips; malformed impacts;
  duplicate/unknown/extra impact members; derived-field rejection; wheel and
  multi-zone location; highest severity; persistence and clear behaviour;
  provenance; a representative generic MCP update; every new projection field;
  equity; and representative PDF text.
- Assert that no report column or projection member exists beyond Zone,
  Severity, and Note for an impact record.
- Preserve: existing unknown-path, case-owned-path, report readiness, and MCP
  audit assertions are not weakened.
- Done when: all specified acceptance assertions pass using production
  composition where integration coverage already does so.

### Step 7 — Validate, simplify, and hand off

- Reuses: the repository's locked restore/build/test rails and the ticket's
  required simplification-pass record.
- Run the migration-grant check, canonical solution checks, and only then
  record dated Reuse, Simplification, Efficiency, and Altitude dispositions in
  the ticket plan.
- No routed Razor page is changed. Test UI snapshot commands are therefore not
  run. If that scope changes, stop and hand the page work to its owner, who
  must run `Update-TestUiSnapshots.ps1`, `-Verify -SkipCapture`, and
  `Test-UiCatalogue.ps1`.
- Stop on any failed command, migration-lock conflict, missing census update,
  changed external contract, or pressure to alter an excluded file.
- Done when: the post-implementation report is written and one PR to `dev` is
  open for independent review.

## Acceptance checks

- New closed paths normalize and round-trip; unknown, case-owned, malformed,
  direct-derived, and structurally invalid values fail closed.
- Two or more zones persist `multiple`; severity is the highest canonical
  severity; report display renders the existing exact state labels.
- The report projects every new reportable field and D41 equity. Excess is
  shown independently.
- Existing generic `pegasus_assessment_update` is the production MCP caller;
  `GenerateAssessmentReportDraft` is the report caller.
- No field is rendered prematurely in Case UI: it remains absent, not disabled,
  until its owning UI ticket wires it through `OperatorLabels.cs`.
- Core remains the sole owner of policy, code lists, parsing, and calculation.

## Commands

```powershell
./scripts/Test-MigrationGrants.ps1
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"
```

## Stop condition

PR targeting `dev` is open, the post-implementation report is written, and
ENG-035 is in Review. Do not merge or begin another ticket.
