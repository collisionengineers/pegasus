# CASE-027 plan

Four section bodies become four partials this lane owns; `Details.cshtml`'s
dispatch shrinks to four `<partial>` lines. No new model data, no new CSS, no
new script, no new package.

## 1. `_CaseVehicle.cshtml` — the `?section=vehicle` body

**Reuses:** `Model.Case.Data.Vehicle.*` and `Model.Case.VehicleEvidence`
(already loaded by `GetCase`, research premise 4); `Shared/_Provenance` for
each value's source glyph; `Shared/_StatusChip` for lookup outcomes;
`Shared/_ReasonDialog` is *not* used — the accept/correct decisions need
fields a reason dialog cannot carry, so they are forms, as they were before
PR #599 removed them (`git show 2204117a^:…/_CaseWorkflow.cshtml`);
`OperatorLabels.OfficeTime`, `.MileageUnit`, `.Provenance`; classes `panel`,
`panel-head`, `panel-body`, `fact-grid`, `fact`, `button-row`, `table-wrap`,
`gated`, `stack`.

- Vehicle facts: Registration, Make, Model, Year, Mileage, Mileage source.
  Only populated facts render (design README, "Only populated, relevant
  sections render").
- "Vehicle checks": **Refresh DVLA** and **Refresh DVSA/MOT**, both posting
  the one existing `Cases/Vehicle?handler=RequestVehicleLookup`; **Run
  Experian check** as a D7/D22 seam —
  `<span class="gated" data-condition="…"><button type="button" class="btn"
  disabled aria-disabled="true">` — `data-condition` always set (PLAT-061).
- State list: `VehicleEvidence.Observations`, newest first, with outcome
  chip, provider, retrieved time and failure code.
- Suggestion decisions (edit mode only): Accept, and Correct with the four
  correctable values, posting the existing
  `Cases/Vehicle?handler=AcceptVehicleSuggestion`. This restores the only
  production caller of `IAcceptVehicleSuggestion` and `IRequestVehicleLookup`
  (research premise 3).
- Vehicle History textarea: **not rendered** — no write handler exists
  (premise 5); [[CASE-029]] owns it.

## 2. `_CaseInspectionAddress.cshtml` — the `?section=inspection-address` body

**Reuses:** `Model.Case.Data.Inspection`; `_Provenance`;
`OperatorLabels.InspectionMode`; `Cases/Details?handler=Save`; classes
`panel`, `definition-list`, `definition`, `field`, `button-row`.

- Recorded value, Provider default (the slot whose `Source.Kind` is
  `ProviderSetting`), Inspection mode — each with its provenance glyph.
- Edit (in edit mode, data present): one `inspectionAddress` input, Cancel
  (the existing `[data-edit-toggle-off]` finish form) and Save, posting
  `Cases/Details?handler=Save`. The visible input is rendered **before**
  `_CaseDataHiddenFields`, so first-value-wins (premise 10) makes it the
  submitted value while every other editable value posts unchanged.
- Previous values select: **not rendered** — no store (premise 6).

## 3. `_CaseDataHiddenFields.cshtml` — one list, not two

**Reuses:** the same `Data.*.Confirmed?.Value` reads `_CaseWorkflow.cshtml`
already performs. Renders all twenty `CaseEditableData` members as hidden
inputs so the inspection-address form does not carry a second copy of the
editable-field list. Modelled on the existing
`Cases/Shared/_ReadinessHiddenFields.cshtml`, which is the same idea for
readiness evidence.

## 4. `_CaseFiles.cshtml` — the `?section=case-files` body

**Reuses:** the existing `_CaseDocuments` partial (step 5), `_ImageGallery`,
`_EvidenceViewer` (already rendered once by `Details.cshtml:476`), the two
gallery URL blocks moved verbatim out of `Details.cshtml:310–396`, and
`OperatorLabels.OfficeTime`.

- Documents (step 5) · Instruction photographs · Vehicle images.
- Correspondence: **not rendered** — no case-scoped mail query (premise 7).

## 5. `_CaseDocuments.cshtml` — restyle onto the design system

**Reuses:** `panel`/`panel-head`/`panel-body`, `document-list`,
`document-row`, `document-icon`, `btn`/`btn--dark`/`btn--icon`, `status`
(via `_StatusChip`), `table-wrap`, `_ReasonDialog`, and every
`OperatorLabels` call it already makes.

- Legacy `form-panel`/`section-label`/`primary-action`/`secondary-action`
  replaced by the vocabulary the design README's component map names.
- Row: file name, `type · size · source`, custody chip, **Preview**
  (`Documents/Download?inline=true`) and **Save as** (`Documents/Download`).
- Panel head gains **Add evidence** → `/Upload` and **Open Operations** →
  `/Operations`, per §1.8.
- The sentence "No public upload request is recorded. Availability is not
  assumed." is deleted: explanatory copy in a read-only view.

## 6. `Details.cshtml` — dispatch only (lane E1's file)

Four branches at :303–427 collapse to four `<partial>` lines. Net removal.
Reported loudly; nothing else in that file changes.

## 7. `_CaseWorkflow.cshtml` — two hidden inputs (lane E1's file)

`claimantContactNumber` and `claimantAddress` added, matching the twelve
already there, so an Overview save stops clearing them (premises 8–9).
D19 rule 2. Reported loudly.

## 8. `OperatorLabels.cs` — append one nested class

`static class CaseWorkspace` holding this lane's labels (the Experian seam's
`data-condition`, the two refresh-control labels, the Vehicle-checks and
Case Files panel names). Appended at the end of the file, inside its own
nested class; nothing existing reordered.

## 9. `catalogue.json` — three false classifications

`Vehicle`/`Custody`/`Tasks` → `protocol`, with the wording [[CASE-012]] round
2 used for `Workflow`/`Closure`. No snapshot regeneration.

## 10. Tests

`CaseVehicleWebTests.cs` gains render pins for the Vehicle view (the two
refresh controls post the lookup handler; the Experian seam is `disabled`
with a `data-condition`; the observation state list renders). `CaseCustodyWebTests.cs`
gains a Case Files render pin (document row with Preview + Save as, and no
explanatory sentence). `CaseTasksWebTests.cs` gains the Inspection address
pin. Existing assertions are not weakened: the three files' current tests are
handler-binding tests and are untouched.

## Simplification pass — 2026-08-29

Run over this branch's own diff before the PR. Findings and dispositions
appended below at that point.
