# ENG-035 implementation plan — 2026-09-02 (gpt-5.6-terra xhigh; reviewed by Claude, see "Plan review")

Diff estimate: 18 owned files changed/created; no new project, package, table, port,
or UI path.

## Verified basis

- `git status --short --branch; git log -1 --format='%H %D%n%s'; git diff --stat cad00be9 897db953 -- src tests` confirmed detached `897db953` at `origin/dev` and no source/test drift from the research SHA.
- `rg -n -C 4 'CaseAssessmentFields|AssessmentVocabulary.Definitions.Keys|CK_CaseAssessmentFields_FieldPath|AssessmentFieldWriter' ...` confirmed the database constraint is generated from vocabulary keys and persistence already merges then validates before writing.
- `Get-Content -Raw src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs; Get-Content -Raw docs/design/assets/report-renderer/templates/assessment_report.scriban` confirmed the embedded template has no marked-diagram slot.
- `rg -n -C 3 'Damage record|Settlement|D39|D41|Assessment-report outcomes|ENG-03|ENG-04' ...` confirmed the governing D39/D41 requirements now exist on this revision.

## Storage and decisions

Use one structured JSON field, `damage.impacts`, in `CaseAssessmentFields`, not
one field per zone. It is bounded to the closed zone list, normalizes to a
canonical JSON array, rejects duplicate zones and malformed/unknown members, and
is parsed only by `AssessmentPolicy`. This keeps `AssessmentFieldDefinition` and
`ValidateAndNormalize` as the single structural-validation owner, avoids
parallel per-zone parsers, and still requires the check-constraint migration.

Size bound: `CaseAssessmentFields.Value` is `nvarchar(4000)`. Each entry's
`note` is bounded to 100 characters, and the canonical serialization is
length-checked against the field's `MaximumLength` (4000) after
normalization, failing closed if it exceeds it (sixteen entries of
`{zone, severity, type, note}` fit; the check is what makes the bound real).

`assessment.impact_location` and `assessment.impact_severity` remain persisted
vocabulary entries only for Core-produced values. Add them to a
`DerivedPaths` set and reject caller/MCP writes in `ValidateAndNormalize` with
a sibling of the existing `CaseOwnedPaths` branch (same shape, its own
message naming `damage.impacts` as the source); they are never silently
overwritten. `EfCaseAssessmentStore` merges the accepted request, applies
`AssessmentPolicy.DeriveImpactFields` to the merged map, and persists/removes
only those two resulting derived rows through `AssessmentFieldWriter`.
Readiness keeps its two `RequireField` calls over the persisted derived paths
(`AssessmentPolicy.cs:238-239`) and changes only their section wording to
`Damage`; no impacts means no derived rows, so both requirements remain
outstanding.

Open-question isolation:

- Q1 changes only `AssessmentPolicy.DeriveImpactFields`. If confirmed as
  expected, it selects the highest ordered severity. The code spelling is not
  an operator matter: the existing Core codes (`light_to_moderate`,
  `moderate_to_heavy`) are the closed wire vocabulary; the mockup ids are
  UI-only and ENG-034's labels map them.
- Q2 changes only `AssessmentReportProjection.SettlementFiguresOf`, beside
  `CostsOf`/`EstimateTotals.Compute`. If confirmed as expected, it computes
  `netRepair = repairCost - betterment`,
  `equity = engineerValue - netRepair - salvageValue`, and excludes excess;
  ratio values are nullable when their denominator is not positive.

The path list, the migration, and every direct-field projection depend on
neither question; only the aggregation rule and the equity arithmetic do.

## New vocabulary

Codes follow the existing convention: lowercase `snake_case` wire codes;
operator labels for them belong to `OperatorLabels.cs` (ENG-034/ENG-029), never
to Core.

| Type | New paths | Codes / normalized shape |
| --- | --- | --- |
| Structured | `damage.impacts` | JSON array of `{ zone, severity, type, note }`; zones: `front`, `right_front`, `left_front`, `right_side`, `left_side`, `right_rear`, `left_rear`, `rear`, `roof`, `wheel_rf`, `wheel_lf`, `wheel_rr`, `wheel_lr`, `underside`, `interior`, `mechanical`; severity uses the existing `assessment.impact_severity` codes; types: `collision`, `scrape`, `vandalism`, `fire`, `flood`, `theft`, `other`; `note` ≤ 100 characters, text rules. |
| Text | `vehicle.body`, `vehicle.colour`, `vehicle.transmission`, `vehicle.fault_codes`, `vehicle.temporary_repair_method`, `damage.unrelated`, `damage.material_transfer`, `settlement.repair_delays`, `settlement.report_delay`, `settlement.salvage_at`, `settlement.salvage_agent`, `settlement.salvage_agent_reference` | Existing text normalization and bounded field definitions. |
| Enumerated | `damage.tyres.rhf.tyre`, `damage.tyres.lhf.tyre`, `damage.tyres.rhr.tyre`, `damage.tyres.lhr.tyre` | `ok`, `worn`, `damaged`, `illegal`. |
| Enumerated | `damage.tyres.rhf.belt`, `damage.tyres.lhf.belt`, `damage.tyres.rhr.belt`, `damage.tyres.lhr.belt` | `ok`, `locked`, `deployed`, `not_fitted`. |
| Enumerated | `damage.tyres.spare` | `ok`, `repair_kit`, `missing`, `damaged`. |
| Enumerated | `damage.tyres.centre_belt` | `ok`, `locked`, `not_fitted`. |
| Flag | `vehicle.vin_checked`, `vehicle.airbags_deployed`, `vehicle.temporary_repairs_possible`, `settlement.claimant_vat_registered`, `settlement.salvage_moved`, `settlement.salvage_owner_retain`, `settlement.salvage_value_agreed` | Existing `Flag` normalization: `true` / `false`. |
| Money | `vehicle.temporary_repair_cost`, `damage.unrelated_deduction`, `settlement.excess`, `settlement.betterment`, `settlement.reserve`, `settlement.hire_daily_cost`, `settlement.diminution` | Existing money normalization. |
| Date | `vehicle.tax_expiry`, `vehicle.mot_expiry`, `settlement.hire_start`, `settlement.salvage_settled` | Existing `yyyy-MM-dd` normalization. |

Every path is under the 60-character `FieldPath` column limit (longest:
`settlement.salvage_agent_reference`, 34).

`IsFinding` (staff-Engineer confirmation authority, the existing rule for
values/outcome/legal status/salvage): `damage.impacts`, the eight per-corner
tyre/belt states, `damage.tyres.spare`, `damage.tyres.centre_belt`,
`damage.unrelated_deduction`, `settlement.betterment` and
`settlement.diminution` are findings; vehicle facts, dates, flags, logistics
text, `settlement.excess`, `settlement.reserve` and
`settlement.hire_daily_cost` are not. The two derived paths keep
`IsFinding: false` (they are never in a request).

Reuse, rather than duplicate: mockup `vin` → `vehicle.vin`; roadworthiness and
reason → `assessment.legal_status` / `assessment.unroadworthy_reason`;
`outcome`, `category`, `salvageValue` → existing assessment paths; `recovery`
and `storagePerDay` → `costs.recovery_charge` and `costs.storage_charge`;
repair duration → Current `EstimateDetails.RepairDays`; report comments,
history check, fee, and fee lines → existing narrative/fee paths. Equity and
ratios are derived report values, never vocabulary paths.

## Ordered steps

1. Extend `src/Pegasus.Core/Assessment/AssessmentContracts.cs` and
   `src/Pegasus.Core/Assessment/AssessmentPolicy.cs`.

   Reuse `AssessmentVocabulary`, `AssessmentFieldDefinition`,
   `NormalizeFieldValue`, and `ValidateAndNormalize`. Add the closed paths and
   code lists above, one new `AssessmentFieldType` member for the structured
   damage list (its `NormalizeValue` case is the only JSON parser), and
   `DeriveImpactFields`. Reject direct derived-path writes. Keep the existing
   60-field request bound; this ticket adds no bulk-save behaviour.

   Acceptance: unknown and derived-input paths fail closed; accepted new scalar
   values and canonical damage JSON round-trip; duplicate zones, unknown codes,
   malformed objects, over-long notes and invalid values reject; two zones
   derive `multiple`.

2. Update `src/Pegasus.Infrastructure/Persistence/EfCaseAssessmentStore.cs`.

   Reuse its existing serializable transaction, merged field map,
   `ValidateMergedState`, action history, and `AssessmentFieldWriter`.
   Materialize Core-derived impact rows after the request merge only when the
   request carries `damage.impacts` (set or cleared); clearing the damage list
   removes both rows. The derived rows follow the same actor/provenance and
   unchanged-Automation-resubmission rule as the row they derive from, and
   appear in the same before/after action-history evidence. No persistence
   entity, table, or `DbSet` is added.

   Acceptance: a save persists both accepted fields and derived fields with
   ordinary provenance; stale derived fields cannot survive a damage-list
   replacement or clear; a save without `damage.impacts` leaves the derived
   rows untouched.

3. Serialize the migration under the capacity-one
   `src/Pegasus.Infrastructure/Persistence/Migrations/**` lock.

   Create
   `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_ExtendAssessmentVocabulary.cs`,
   its Designer, update
   `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs`,
   and append the migration name to the exact applied-migration census in
   `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs`
   (lines ~107-117, currently ending at
   `20260829212237_GrantProviderSubmissionAcceptRecovery`). Do not edit
   `AssessmentModelConfiguration.cs`: it already builds
   `CK_CaseAssessmentFields_FieldPath` from
   `AssessmentVocabulary.Definitions.Keys`. The migration is additive — no
   existing path is removed, so existing `CaseAssessmentFields` rows
   (including hand-recorded `assessment.impact_*` values) stay valid until the
   next damage save replaces them. No table is created, so no GRANT and no
   `scripts/Invoke-AzureDatabaseBootstrap.ps1` change is needed. Wait for the
   migration lock and generate only one unmerged migration.

   Acceptance: the generated migration updates that check constraint for every
   new path, applies to the integration database, the census test passes, and
   `./scripts/Test-MigrationGrants.ps1` passes.

4. Extend the Core report contract and projection in
   `src/Pegasus.Core/Reports/AssessmentReportRendering.cs` and
   `src/Pegasus.Core/Reports/AssessmentReportProjection.cs`.

   Reuse `AssessmentReportSnapshot`, `ReportVehicle`, `Project`, `BuildVehicle`,
   `CostsOf`, and `EstimateTotals.Compute`. Grow `ReportVehicle` with the new
   vehicle facts; add immutable `ReportDamage`/impact/tyre data and
   `ReportSettlement`/`ReportSettlementFigures`; add those groups to
   `AssessmentReportSnapshot`. New record members are trailing parameters
   with defaults, so the two positional constructions in
   `AssessmentReportRenderingTests.cs:92` and
   `AssessmentReportRendererTests.cs:143` keep compiling and the diff stays
   small. `AssessmentReportProjectionInput` is unchanged: every new value is
   already reachable through the assessment projection or
   `CurrentEstimate`. `SettlementFiguresOf` is the only equity/ratio
   calculation. Reuse Current-estimate repair days rather than storing it.

   Acceptance: projection carries every new direct field, impacts, and derived
   values; no arithmetic appears in a renderer or Scriban template.

5. Update
   `src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs`
   and
   `docs/design/assets/report-renderer/templates/assessment_report.scriban`.

   Reuse `VehicleRows`, `VehicleDataRows`, `Rows`, `AmountRows`, and existing
   embedded-resource loading. Add compact vehicle-extra, damage-list/tyre, and
   settlement/derived-figure rows. The template renders labels and values only;
   it adds no explanatory copy.

   The report currently has no SVG slot. ENG-035 renders the zone list/table;
   ENG-036 must supply the marked-diagram contract and its later template
   integration. Do not invent an SVG or touch ENG-036-owned files.

   Acceptance: representative rendered PDF text includes each report section
   and values; the full D39 marked-diagram acceptance remains dependent on
   ENG-036.

6. Extend only the owned tests:

   - `tests/Pegasus.Core.Tests/Assessment/AssessmentPolicyTests.cs`
   - `tests/Pegasus.Core.Tests/Reports/AssessmentReportProjectionTests.cs`
   - `tests/Pegasus.Core.Tests/Reports/AssessmentReportRenderingTests.cs`
   - `tests/Pegasus.IntegrationTests/AssessmentPersistenceIntegrationTests.cs`
   - `tests/Pegasus.IntegrationTests/AutomationAssessmentIngressTests.cs`
   - `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs` (census only)
   - `tests/Pegasus.IntegrationTests/Reports/AssessmentReportRendererTests.cs`

   Reuse the existing policy fixtures, `ReadyInput`, renderer fake, EF
   assessment harness, MCP HTTP ingress harness, and PDF text extraction.
   Add coverage for every table path and its report projection, malformed
   damage rejection, multiple-zone derivation, persistence constraint
   round-trip, and representative rendered values.

   `AssessmentMcpTools.cs` is deliberately unchanged: its existing
   `Dictionary<string, string?>` route delegates to `ISaveAssessment` and
   enumerates no paths. `AutomationAssessmentIngressTests` proves today, with
   no AUTO-015 dependency, that a new path round-trips through
   `pegasus_assessment_update` and that a direct `assessment.impact_location`
   write through the tool now fails closed (this is the one MCP-visible
   contract change of the ticket).

7. Run the simplification pass on the branch diff before opening the PR, then
   record dated Reuse, Simplification, Efficiency, and Altitude findings with
   applied/skipped/deferred dispositions in the ticket plan. Open one PR to
   `dev`, write the post-implementation report, and stop in Review; do not
   merge.

## Lane overlap and merge order (wave 1)

Whole-file ownership is not disjoint from the other wave-1 lanes:

- [[DOCS-017]] changes `AssessmentReportProjection.cs`,
  `AssessmentReportRendering.cs`, `PlaywrightAssessmentReportRenderer.cs`,
  `assessment_report.scriban`, `AssessmentReportProjectionTests.cs`,
  `AssessmentReportRenderingTests.cs`, `AssessmentReportRendererTests.cs` and
  `AssessmentPersistenceIntegrationTests.cs` — all also in this plan. Its
  change replaces the accepted-signatory tuple; ENG-035's adds
  damage/settlement groups. Textually separate regions, but both touch the
  snapshot constructor and the same test fixtures.
- [[AUTO-018]] changes `AssessmentPersistenceIntegrationTests.cs`, the
  migration census in `IntakePersistenceIntegrationTests.cs`, and the model
  snapshot; [[PLAT-068]] adds a migration and changes the model snapshot.

Rule: migrations and the census stay under the existing capacity-one lock.
ENG-035 merges before DOCS-017 (DOCS-017 waits on CASE-040/PLAT-068 in any
case); if DOCS-017 lands first, refresh with `git merge --no-edit origin/dev`
and build the report-test fixtures against its `Signatory` contract before
opening the PR. Refresh from `origin/dev` after every neighbour merge and
re-run the report and persistence tests.

## Binding scope and design rules

No Razor page, `OperatorLabels.cs`, MCP tool, valuation/estimate code, governing
documentation, or corpus file changes. New Case UI labels and exact displayed
state labels belong exclusively to
`src/Pegasus.Web/Presentation/OperatorLabels.cs` in ENG-029/ENG-034. Until
those lanes render the fields, they are absent from the UI, not disabled.
Report table headings are renderer/template content, not a second UI label
catalogue. Do not add explanatory copy.

## Commands

```powershell
./scripts/Test-MigrationGrants.ps1
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"
```

The Browser lane is omitted because no routed page changes; `dotnet build`
still compiles every Browser test against the changed Core records.

Test UI commands are not applicable because ENG-035 must not change a routed
Razor page. If that scope changes, stop and hand the page/snapshot work to its
owner; that owner runs:

```powershell
./scripts/Update-TestUiSnapshots.ps1
./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture
./scripts/Test-UiCatalogue.ps1
```

## Simplification pass

_Implementer adds a dated entry here before the PR, covering the four lenses
and every finding's applied, skipped, or ticketed disposition._

## Stop condition

The PR targeting `dev` is open, the post-implementation report is written, and
ENG-035 is in Review. Do not merge or begin a neighbouring ticket.

## Plan review (2026-09-02, Claude)

Cross-family review of the gpt-5.6-terra plan against the ticket body, D39/D41,
EPIC-012 `context.md`, EPIC-011 `context.md`/`waves.md`, the mockup sources and
the code at `897db953`. Every named helper was grepped and exists:
`ValidateAndNormalize`, `NormalizeFieldValue`, `ValidateMergedState`,
`CaseOwnedPaths`, `AssessmentFieldWriter.Write`, `BuildVehicle`, `CostsOf`,
`EstimateTotals.Compute`, `ReportVehicle`, `AssessmentReportSnapshot`,
`VehicleRows`, `VehicleDataRows`, `Rows`, `AmountRows`, `ReadyInput`,
`scripts/Test-MigrationGrants.ps1`, and the `Category!=Corpus&Category!=Browser`
filter (CI's own lane). `DeriveImpactFields`, `DerivedPaths`,
`SettlementFiguresOf`, `ReportDamage`, `ReportSettlement*` are new names the
plan introduces, not reuse claims.

| # | Finding | Disposition |
| --- | --- | --- |
| 1 | Wave-1 file ownership is not disjoint: DOCS-017's files doc and plan change the same four report source files and four of the same test files; AUTO-018 and PLAT-068 share the model snapshot, migration lock and (AUTO-018) `AssessmentPersistenceIntegrationTests.cs` and the migration census. | Reported to the orchestrator (not fixable in this plan); "Lane overlap and merge order" section added with the serialization rule ENG-035 → DOCS-017 and the refresh-and-rebuild instruction. |
| 2 | The migration census in `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs:107-117` asserts the exact applied-migration list; a new migration without it fails the integration lane. Missing from plan and files. | Fixed: added to step 3, step 6 and the checklist. |
| 3 | Enumerated codes for tyres/belts/spare/centre belt were given as display strings (`OK`, `Repair kit`, `Not fitted`), breaking the Core `snake_case` code convention and putting labels in Core (one list per concept). | Fixed: codes are `ok`, `worn`, `damaged`, `illegal`, `locked`, `deployed`, `not_fitted`, `repair_kit`, `missing`; labels stay with ENG-034/ENG-029 in `OperatorLabels.cs`. |
| 4 | `damage.impacts` had no per-entry `note` bound and no statement of how the 4,000-character column is enforced on the canonical serialization (`NormalizeValue` length-checks the raw value only, and JSON escaping can expand it). | Fixed: `note` ≤ 100 characters; canonical serialization length-checked against `MaximumLength`, fail closed. |
| 5 | `IsFinding` was not stated for any new path; the flag decides who may confirm a value (`RequireFindingConfirmationAuthority`). | Fixed: classification recorded under "New vocabulary"; the reviewer of the PR checks it against FRD-06. |
| 6 | "Reuse the existing `CaseOwnedPaths` rejection branch" would emit the case-detail message for derived paths. | Fixed: sibling branch with its own message. |
| 7 | Readiness wording said the derived result is "evaluated from `damage.impacts`"; since the derived rows are persisted the existing `RequireField` calls already do this — only the section name changes. | Fixed: smaller diff recorded. |
| 8 | Step 2 did not say when derived rows are written; without the "only when the request carries `damage.impacts`" rule an unrelated save would re-stamp derived provenance, and the unchanged-Automation-resubmission rule (`EfCaseAssessmentStore.cs:177-188`) would be bypassed. | Fixed. |
| 9 | "Generate one migration only after Q1 is resolved" — the path list does not depend on Q1 or Q2. | Fixed: sentence replaced; migration waits only for the lock. |
| 10 | Step 6 made the MCP ingress test conditional on AUTO-015; the generic route already accepts any vocabulary path, and the derived-path rejection through the tool is a contract change that needs its own test now. | Fixed: AUTO-015 dependency removed from the test; rejection test added. |
| 11 | Q1's second half (code spelling) is an engineering convention, not an operator decision: Core's existing codes are the wire vocabulary and the mockup ids are UI-only. | Fixed: open question narrowed to the aggregation rule; Q1 and Q2 remain unticked for the operator. |
| 12 | Growing `ReportVehicle`/`AssessmentReportSnapshot` breaks the two positional constructions in tests unless new members are trailing with defaults. | Fixed: stated in step 4. |
| 13 | Existing production rows for `assessment.impact_*` (hand-recorded via MCP) were not addressed. | Fixed: step 3 states the migration is additive and the rows stay valid until the next damage save replaces them (assumed, not checked against production; a read-only SQL check at execution is permitted). |
| 14 | Plan proportionality: ~230 lines for an 18-file diff; the "Open-question isolation" paragraph is the only ritual-leaning part. | Accepted: kept because both questions are still open and the split tells the implementer what can proceed. |
| 15 | Rules check: Core owns policy (derivation and equity are Core statics beside `EstimateTotals.Compute`); one list per concept (codes in `AssessmentVocabulary` only, labels in `OperatorLabels.cs`, no template-side arithmetic); no explanatory copy (template rows are labels and values); no new package; migration and census in the same diff, no grant needed because no table is created. | No violation found. |

## Resolutions (2026-09-03)

- Controller: derived `impact_severity` = highest zone severity; Core's existing severity codes are canonical.
- Controller: equity = Engineer's value − (repair cost − betterment) − salvage value; excess is a separate field, not part of equity.
- Operator (D45): no damage type — the `damage[]` record is zone, severity, note; drop `type` from the vocabulary, the projection and the template.
