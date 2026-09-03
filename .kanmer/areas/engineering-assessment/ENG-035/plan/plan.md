# Plan — ENG-035 (2026-09-03, gpt-5.6-terra xhigh; revised after plan review)

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

Every new path, type, and code set is pinned here; implementation adds no path
this table does not name.

| Path | Type | Codes / bound |
| --- | --- | --- |
| `vehicle.vin_checked` | Flag | — |
| `vehicle.transmission` | Enumerated | `manual`, `automatic`, `semi_automatic`, `cvt`, `unknown` |
| `vehicle.colour` | Text | 40 |
| `vehicle.body` | Text | 40 |
| `vehicle.tax_expiry` | Date | 10 |
| `vehicle.mot_expiry` | Date | 10 |
| `vehicle.airbags_deployed` | Text | 200 |
| `vehicle.fault_codes` | Text | 2000 |
| `vehicle.temporary_repairs_possible` | Flag | — |
| `vehicle.temporary_repair_method` | Text | 2000 |
| `vehicle.temporary_repair_cost` | Money | 20 |
| `damage.impacts` | structured (new `AssessmentFieldType.Json`) | 4000; see below |
| `damage.tyres.right_front.tyre` … `damage.tyres.left_rear.tyre` (4) | Enumerated | `ok`, `worn`, `damaged`, `illegal` |
| `damage.tyres.right_front.belt` … `damage.tyres.left_rear.belt` (4) | Enumerated | `ok`, `locked`, `deployed`, `not_fitted` |
| `damage.tyres.spare` | Enumerated | `ok`, `repair_kit`, `missing`, `damaged` |
| `damage.tyres.centre_belt` | Enumerated | `ok`, `locked`, `not_fitted` |
| `damage.unrelated` | Text | 2000 |
| `damage.unrelated_deduction` | Money | 20 |
| `damage.material_transfer` | Text | 2000 |
| `settlement.excess` | Money | 20 |
| `settlement.betterment` | Money | 20 |
| `settlement.claimant_vat_registered` | Flag | — |
| `settlement.reserve` | Money | 20 |
| `settlement.repair_delays` | Text | 2000 |
| `settlement.report_delay` | Text | 2000 |
| `settlement.storage_per_day` | Money | 20 |
| `settlement.hire_start` | Date | 10 |
| `settlement.hire_daily_cost` | Money | 20 |
| `settlement.diminution` | Money | 20 |
| `settlement.salvage.at` | Text | 400 |
| `settlement.salvage.agent` | Text | 200 |
| `settlement.salvage.agent_reference` | Text | 100 |
| `settlement.salvage.moved` | Flag | — |
| `settlement.salvage.owner_retains` | Flag | — |
| `settlement.salvage.value_agreed` | Flag | — |
| `settlement.salvage.settled` | Date | 10 |

`settlement.storage_per_day` is the daily rate D41 names and is **not** the
existing `costs.storage_charge` total; both exist and neither is reinterpreted.
`costs.recovery_charge` carries recovery, the current estimate's
`EstimateDetails.RepairDays` carries repair duration, `assessment.salvage_value`
carries the salvage figure, and `fee.description_lines` carries the fee lines —
none of those is duplicated.

`damage.impacts` is a canonical JSON array. Each element has exactly
`zone`, `severity`, `note` (D45 — no `type`). Zone codes are the closed list
`front`, `left_front`, `right_front`, `left_side`, `right_side`, `rear`,
`left_rear`, `right_rear`, `roof`, `wheel_right_front`, `wheel_left_front`,
`wheel_right_rear`, `wheel_left_rear`, `underside`, `interior`, `mechanical`
(D39's zones; `multiple` stays headline-only). Severity codes are Core's
existing `light`, `light_to_moderate`, `moderate`, `moderate_to_heavy`,
`heavy`. Zones are unique within the array. `note` is bounded at 200
characters, matching the existing short-text bounds in the vocabulary; the
whole serialized value stays inside the 4000-character row.

No existing normalizer parses structured values, so Step 1 adds one Core-local
JSON normalizer over `System.Text.Json` (already available in the framework;
no package is added). Its zone/severity metadata is the single source used by
validation, derivation, and projection — there is no second list.

Derived values. `assessment.impact_location` and `assessment.impact_severity`
become Core-derived-only; a direct caller write fails closed. One zone maps to
its own code, except the four wheel zones, which map to the existing `wheel`
code; two or more zones derive `multiple`. Severity is the highest zone
severity in the existing code order. Report-only equity is
`engineer value − (repair cost − betterment) − salvage`; excess is a separate
field and is not part of it. Financial ratios are permitted, not required by
D41, so do not add them.

Save bound. `AssessmentPolicy.MaximumFieldsPerSave` is 60 and this table takes
the vocabulary past that, so one Case-page save of the Damage and Settlement
sections could exceed it. Step 1 raises the bound to cover the whole vocabulary
in one save and asserts the new value; nothing else about the bound changes.

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
| Modify | `tests/Pegasus.IntegrationTests/Reports/AssessmentReportDraftWebTests.cs` |
| Modify | `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs` |

The last two are consequences of this ticket's own diff, not new scope:
`AssessmentReportDraftWebTests.cs` builds `AssessmentReportSnapshot`
positionally, and `IntakePersistenceIntegrationTests.cs:40-119` asserts an
exact applied-migration census that any new migration must extend.

## Dependencies and exclusions

- Serialize the migration under the capacity-one migration lock, and add the
  generated migration name to the census in the same diff. PLAT-068 also adds
  one migration; whichever lands second refreshes with
  `git merge --no-edit origin/dev`, regenerates against the merged snapshot,
  and re-runs the census assertion.
- DOCS-017 edits the signatory block of the same four report files. ENG-035
  touches no signatory member, tuple, readiness rule, or signature markup, and
  refreshes with `git merge --no-edit origin/dev` before opening its PR. If
  DOCS-017 has already merged, take its head first.
- ENG-036 owns the marked-diagram component. This ticket exposes the validated
  zones to the report; it must not invent a diagram, crop behaviour, or an
  ENG-036-owned asset.
- AUTO-015 owns MCP overwrite, clear, and confirmation semantics. Do not edit
  `AssessmentMcpTools.cs`; prove the existing generic route accepts a new
  vocabulary path and rejects direct derived-field input. ENG-035 does not wait
  on AUTO-015: vocabulary admission and derived-field rejection are Core policy
  in ENG-035's own files, and the tool enumerates no paths.
- Do not modify Case Razor pages/partials, `OperatorLabels.cs`, estimate or
  valuation ownership, report-image curation, D31 sign-off files, or governing
  FRDs.

## Ordered steps

### Step 1 — Define and validate the closed Core vocabulary

- Files: `src/Pegasus.Core/Assessment/AssessmentContracts.cs`,
  `src/Pegasus.Core/Assessment/AssessmentPolicy.cs`
- Reuses: `AssessmentVocabulary.DefinitionList`,
  `AssessmentFieldDefinition`, `ValidateAndNormalize`,
  `NormalizeFieldValue`, `NormalizeValue`, and `ValidateMergedState`. No
  existing normalizer handles a structured value, so add one Core-local JSON
  normalizer over `System.Text.Json`.
- Change: add every path in the vocabulary table with its stated type and
  codes, plus the `Json` field type and the `damage.impacts` normalizer.
  Reject malformed JSON, unknown zones or severities, duplicate zones, notes
  over 200 characters, unknown or missing object members, and oversized values.
- Change: make both scalar impact paths Core-derived-only, with the mapping,
  `multiple`, and highest-severity rules stated above.
- Change: raise `MaximumFieldsPerSave` to cover the whole vocabulary in one
  save.
- Preserve: unknown and case-owned paths fail closed; existing codes,
  finding-confirmation rules, and readiness requirements remain.
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

- Files: the two new `*_ExtendAssessmentVocabulary` migration files,
  `PegasusDbContextModelSnapshot.cs`, and the migration census in
  `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs`
- Reuses: `AssessmentModelConfiguration`, which already builds
  `CK_CaseAssessmentFields_FieldPath` from `AssessmentVocabulary.Definitions`.
- Change: generate one migration that replaces the check constraint with the
  expanded closed path list, update the generated snapshot, and append the
  generated migration name to the census list in the same diff.
- Preserve: no entity, table, `DbSet`, package, or data backfill. The change
  replaces a constraint and needs no new grant; if implementation finds a
  permission is required, it ships in this same migration.
- Rollback: the down migration restores the prior constraint only while no
  new-path rows exist. If such rows exist, SQL must fail transactionally rather
  than delete assessment evidence; production rollback then requires an
  explicit operator decision.
- Done when: the generated migration, model snapshot, migration-grant check,
  and census assertion are green.

### Step 4 — Extend the immutable Core report projection

- Files: `src/Pegasus.Core/Reports/AssessmentReportProjection.cs`,
  `src/Pegasus.Core/Reports/AssessmentReportRendering.cs`
- Reuses: `AssessmentReportSnapshot`, `AssessmentReportPresentation`,
  `ReportVehicle`, `Project`, `BuildVehicle`, `CostsOf`, and
  `EstimateTotals.Compute`.
- Change: add immutable report shapes for vehicle extras, impacts, restraint
  facts, unrelated damage, material transfer, and settlement data. Update every
  positional construction site rather than adding compatibility defaults: a
  member is optional only where the domain genuinely permits absence.
- Change: have projection consume the Core-normalized impact data, reuse
  current-estimate repair days and repair cost, and calculate equity only in
  Core. Keep fee lines on the existing `SplitLines` route.
- Change: bump `AssessmentReportContract.TemplateVersion` to `rendererref1-v2`
  — the snapshot and template shape changes and `PayloadVersion` is asserted
  against it.
- Change: report display text for the new codes lives in Core, alongside the
  existing `AssessmentReportPresentation` wording, so the code list and its
  report wording stay in one place. Infrastructure gains no label catalogue and
  the new zone codes are not left to `Display()`'s title-casing.
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
  Scriban-resource loading, and the renderer's existing money and date
  formatting.
- Change: add compact vehicle, damage, restraint, and settlement row and table
  slots to the Scriban context and template, taking display text from the Core
  snapshot. The damage table contains only Zone, Severity, and Note.
- Preserve: report headings follow the existing report-template convention;
  no Web label catalogue is created or changed.
- Forbidden: explanatory copy, UI labels, template-side calculations, a second
  Infrastructure label list, a marked-diagram substitute, or image curation.
- Dependency: the full D39 marked-diagram acceptance remains with ENG-036 and
  requires its report-ready contract.
- Done when: representative values reach the rendered PDF through the
  existing `GenerateAssessmentReportDraft` caller.

### Step 6 — Prove policy, persistence, MCP ingress, projection, and rendering

- Files: the eight owned test files in the Expected files table.
- Reuses: `AssessmentPolicyTests` vocabulary fixtures,
  `AssessmentReportProjectionTests.ReadyInput`,
  `AssessmentReportRenderingTests` snapshot fixture, the assessment persistence
  harness, MCP HTTP `ToolCallPayload` and lease setup, and renderer PDF
  extraction.
- Add assertions for canonical new-path round trips; malformed impacts;
  duplicate, unknown and extra impact members; derived-field rejection;
  wheel-zone and multi-zone location; highest severity; persistence and clear
  behaviour; provenance; the raised save bound; a representative generic MCP
  update; every new projection field; equity; the bumped payload version; and
  representative PDF text.
- Assert that no report column or projection member exists beyond Zone,
  Severity, and Note for an impact record.
- Note: `AssessmentReportRendererTests` is `Category=Browser`, so the PDF
  assertion is only proven by a run that includes the Browser category — see
  Commands.
- Preserve: existing unknown-path, case-owned-path, report readiness, and MCP
  audit assertions are not weakened.
- Done when: all specified acceptance assertions pass using production
  composition where integration coverage already does so.

### Step 7 — Simplify, validate, and hand off

- Reuses: the repository's locked restore, build and test rails and the
  ticket's required simplification-pass record.
- Run the simplification pass over the branch diff and apply its
  behaviour-preserving fixes first, recording dated Reuse, Simplification,
  Efficiency, and Altitude dispositions in this plan; then run the migration
  grant check and the canonical solution commands, so the verified tree is the
  tree that ships.
- No routed Razor page is changed. Test UI snapshot commands are therefore not
  run. If that scope changes, stop and hand the page work to its owner, who
  must run `Update-TestUiSnapshots.ps1`, `-Verify -SkipCapture`, and
  `Test-UiCatalogue.ps1`.
- Stop on any failed command, migration-lock conflict, changed external
  contract, or pressure to alter an excluded file.
- Done when: the post-implementation report is written and one PR to `dev` is
  open for independent review.

## Acceptance checks

- New closed paths normalize and round-trip; unknown, case-owned, malformed,
  direct-derived, and structurally invalid values fail closed.
- Two or more zones persist `multiple`; a wheel zone persists `wheel`;
  severity is the highest canonical severity.
- The report projects every new reportable field and D41 equity. Excess and
  `settlement.storage_per_day` are their own fields.
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
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"
```

The canonical gate is `Category!=Corpus`; it must not be narrowed to exclude
`Category=Browser`, because the rendered-PDF proof for Steps 5 and 6 lives in
that category.

## Stop condition

PR targeting `dev` is open, the post-implementation report is written, and
ENG-035 is in Review. Do not merge or begin another ticket.

## Plan review (2026-09-03, gpt-5.6-sol xhigh; dispositions Claude Opus)

| # | Sev | Finding | Disposition |
| --- | --- | --- | --- |
| 1 | blocker | Steps 4-6 report files collide with DOCS-017's whole-file ownership. | Partly accepted, fixed. The overlap is real, but the lanes touch disjoint regions (signatory block versus vehicle, damage and settlement), EPIC-012 already prescribes `git merge --no-edit origin/dev` for lane refresh, and the shared-lock capacity-one list does not include the Reports files. Added an explicit refresh-and-take-DOCS-017-head dependency and a no-signatory-member exclusion. Rejected the "cannot remain parallel lanes" remedy: re-allocating lanes is a controller decision this ticket does not need. |
| 2 | blocker | Migration and snapshot ownership collide, and `IntakePersistenceIntegrationTests.cs` holds an exact census the plan excluded. | Accepted, fixed. Confirmed at `IntakePersistenceIntegrationTests.cs:40-119`. The census line is a consequence of this diff, not other scope: the file joins Expected files and Step 3, and the "stop if that coordination is not assigned" ritual is deleted. Migration-lock serialization and the PLAT-068 second-lander refresh are stated. |
| 3 | blocker | `settlement.storage_per_day` was folded into the existing `costs.storage_charge`. | Accepted, fixed. D41 names storage per day, the mockup carries both `settlement.storagePerDay` and a charge, and research listed the rate as a gap. Added as its own Money path; the existing charge is untouched. |
| 4 | blocker | AUTO-015 is not enforced as a blocking prerequisite. | Rejected. `AssessmentMcpTools.cs:323-372` enumerates no paths and delegates to `ISaveAssessment`, so vocabulary admission and derived-field rejection are entirely Core policy inside ENG-035's own files. AUTO-015 owns overwrite, clear and confirm semantics, which ENG-035 does not touch. The non-dependency is now recorded explicitly. |
| 5 | blocker | Report labels: `Display()` title-casing is not a canonical label map, and `OperatorLabels.cs` is excluded. | Accepted in substance, fixed differently. Infrastructure cannot reference Web presentation code, so `OperatorLabels.cs` is architecturally unavailable to the renderer, and Core already owns report wording in `AssessmentReportPresentation`. Report display text for the new codes now lives in Core beside it, keeping one list per concept, adding no Infrastructure catalogue and producing no `Wheel Rf`. |
| 6 | should-fix | Paths, codes, bounds and the structured normalizer were under-specified; the 100-character note cap was unsourced. | Accepted, fixed. Every path, type and code set is pinned in one table; the note bound is 200 characters, matching existing short-text bounds and stated as a planner bound. Recorded that no existing normalizer fits and that the new one uses `System.Text.Json` (framework, no package). |
| 7 | should-fix | Defaulted snapshot members are compatibility engineering, and `TemplateVersion` was left at `rendererref1-v1`. | Accepted, fixed. Positional construction sites are updated instead of defaulted (rule 6, greenfield has no legacy); `AssessmentReportDraftWebTests.cs` joins Expected files; `TemplateVersion` bumps to `rendererref1-v2` with the `PayloadVersion` assertion. |
| 8 | should-fix | The command narrowed the canonical gate, and verification ran before simplification. | Accepted, fixed. Verified that `AssessmentReportRendererTests` carries `[Trait("Category", "Browser")]`, so the old filter would never have run the PDF proof it claimed. Commands revert to the canonical `Category!=Corpus`, and Step 7 simplifies before verifying. |
| 9 | should-fix | Added by the dispositioning pass: `AssessmentPolicy.MaximumFieldsPerSave` is 60 and the vocabulary now exceeds that, so one Case-page save of Damage plus Settlement could be refused. | Fixed. Step 1 raises the bound to cover the whole vocabulary and asserts it. |
| 10 | nit | Added by the dispositioning pass: D39 names four wheel zones while Core has one `wheel` code, and the derived mapping was ambiguous. | Fixed. The four wheel zone codes are pinned in the vocabulary table, and all four derive the existing `wheel` location code. |

D44, D45 and D46 were confirmed clean by both readers: no review flag,
checkbox, dialog or history line; no damage `type` in the vocabulary,
projection or report; no crop or image-curation change.

## Coordination note (2026-09-03) — do not strand an edit route

[[CASE-043]] (wave 4) owns the extended case vehicle record and its editable
surface. If any step of this plan retires a field from the Assessment
vocabulary and re-homes it on the case vehicle record, this PR must keep that
field editable and persisted until CASE-043 ships its replacement route. A
field that loses its only production edit route on `dev` is a regression, not
a migration, and the reviewer is asked to check this specifically.

No damage `type` may appear in any contract, projection or fixture (D45);
`files/files.md` still says "zone/type structures" and is stale on that point.
