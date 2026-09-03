# Research — CASE-029 (2026-09-02, gpt-5.6-terra high, wrapper-checked)

## Scope and evidence status

**VERIFIED** — `git status --porcelain` was empty at completion. The checkout
was initially `cad00be9`; a later read-only check found `HEAD` and `origin/dev`
at `897db953`, so the stated fixed-HEAD premise changed externally during
research.

**VERIFIED** — `git log origin/dev --oneline --grep='ENG-027'` and
`git merge-base --is-ancestor 450b9234 origin/dev` show [[ENG-027]] merged.
The same log produced no [[CASE-038]] or [[ENG-035]] commit; `DetailsModel`
still selects one section at a time, so their frame/vocabulary work is not
merged on the observed `origin/dev`.

## Current behaviour

**VERIFIED** — `rg -n 'CaseDataValueKind|CaseField' \
src/Pegasus.Core/Cases/CaseDataContracts.cs` shows `Fact`, `Suggestion`, and
`Confirmed`. `CaseField<T>` retains one value of each kind and exposes
`Current`; `EfCaseDataStore` maps persisted `suggestion` records back into
that slot.

**VERIFIED** — `rg -n 'Suggestion|VehicleLookup' \
src/Pegasus.Infrastructure/Persistence/EfVehicleWorkflowStore.cs` shows the
existing vehicle flow records lookup observations separately, then accepts or
corrects the whole observation into confirmed vehicle fields. It does not
render or accept an individual case-data suggestion chip.

**VERIFIED** — `Get-Content src/Pegasus.Core/Vehicle/VehicleWorkflow.cs` shows
`IRequestVehicleLookup`, `IAcceptVehicleSuggestion`, edit-lease/version
checks, queue publication, and `VehicleSuggestionDecision` (`Accept`,
`Correct`). `AcceptVehicleSuggestionCommand` has an observation ID, not a
field key.

**VERIFIED** — `Get-Content \
src/Pegasus.Infrastructure/Vehicle/DvlaDvsaProductionAdapter.cs` shows one
adapter calls DVLA VES and DVSA MOT history, returning make, model, year,
engine capacity, fuel, raw MOT observations, and derived mileage. It reports
one combined provider identity, `dvla-ves+dvsa-mot-history`.

**VERIFIED** — `rg -n 'IVehicleLookupAdapter|ProductionLive' \
src/Pegasus.Infrastructure/DependencyInjection.cs` shows production wires
`DvlaDvsaProductionAdapter`; `Program.cs` records requests in Web and leaves
live execution to Worker.

**VERIFIED** — `_CaseVehicle.cshtml` presently draws two forms, "Refresh
DVLA" and "Refresh DVSA/MOT", both posting the same lookup handler; it draws
an Experian disabled seam, a vehicle-checks history table, then whole-record
Accept and Correct forms. `Vehicle.cshtml.cs` exposes those two handlers.

**VERIFIED** — `OperatorLabels.cs` contains the separate DVLA/DVSA labels,
`VehicleChecksPanel`, history label, and the disabled-seam condition
"Experian is not connected". It has no CASE-029 valuation labels.

**VERIFIED** — `Valuations.cs`, `EfValuationStore.cs`, and migration
`20260829095336_CaseValuations.cs` implement persisted valuation rows with
source, date, time, mileage, retail, and trade. The closed source enum is
currently `Glasses`, `Cazana`, and `EngineersValue`; guide month and
`MarketResearch` do not exist.

**VERIFIED** — `ValuationPolicy.EngineersValueField` writes only an
Engineer's Value row to `assessment.values.engineer`; `AssessmentPolicy`
requires an authenticated Engineer for that confirmed professional finding.

**VERIFIED** — `RequestUploadPolicy.cs` defines request-link state and limits,
but `CreateRequestUploadLinkCommand` and `RequestUploadLink` carry neither
Recipient nor Reason. `EfDocumentRequestStore`, `CustodyEntities`, and
`EfCaseQueryStore` persist/project the existing link shape.

**VERIFIED** — `ManualChaseRecord` already maps the required chase fields:
`Channel`, `TargetPartyOrAddress`, `Outcome`, optional `Note`, and `Reason`.
`_CaseHistory.cshtml` currently renders these as an inline form; its handler
is `TasksModel.OnPostRecordManualChaseAsync`, not `CustodyModel`.

## Mockup behaviour

**VERIFIED** — `21-case-sections.js` renders one "Look up DVLA & MOT" action
and a disabled "Experian check" with condition "not connected". It puts a chip
beside every editable field whose non-empty lookup-map value differs from the
stored field.

**VERIFIED** — `04-fixtures.js` lookup maps can supply make, model, colour,
fuel, engine capacity, first registration, tax expiry, MOT expiry, mileage,
year, and transmission. Empty lookup values produce no chip.

**VERIFIED** — `apply-suggestion` sets only the selected field, deletes that
field's suggestion, re-renders Vehicle, restores focus to that field, and
keeps other suggestions. The mockup has no checks panel or suggestion table.

**VERIFIED** — `22-case-engineer.js` renders source cards with Retail, Trade,
guide month, mileage, and date. The Add valuation dialog offers source, guide
month, mileage, retail, and trade; Cazana is disabled and displayed as "not
connected".

**VERIFIED** — the mockup includes an AI market-research card shape, but also
shows adjustments, rationale, and history. Those latter three are explicitly
outside this ticket.

**VERIFIED** — `07-shell.js` gives the upload-request dialog Case, Recipient,
a read-only policy summary, and Reason. `20-case.js` gives the chase dialog
Recipient, Channel, Content, Outcome, and Reason.

## Gaps and implications

**VERIFIED** — one lookup action and per-field chips require replacing the
two-control/checks-table/whole-observation UI, while reusing the existing
combined lookup queue and provenance records.

**VERIFIED** — the persisted `Suggestion` representation already exists, but
the vehicle workflow does not populate or consume it per field. The Core
vehicle contract and persistence workflow require a narrow field-level
acceptance path.

**VERIFIED** — guide month requires a valuation contract/entity/configuration
change and migration. Adding AI market research to the source vocabulary is
owned by [[AUTO-018]], not this ticket.

**VERIFIED** — upload Recipient and Reason require the request-link contract,
persistence entity, query projection, migration, handler, and dialog to agree.
They cannot be UI-only if the Recipient column shown by the mockup is retained.

**VERIFIED** — the real upload UI is `_CaseDocuments.cshtml`; the real chase
UI is `_CaseHistory.cshtml`. `Custody.cshtml` contains only its route
directive, so editing it would not change either dialog.

## Reuse and risks

**VERIFIED** — reuse `CaseDataValueKind.Suggestion`, `CaseField<T>`,
`EfCaseDataStore.Field`, `RequestVehicleLookup`, `VehicleLookupObservation`,
and `CaseMutationPageModel` for source-attributed, leased, versioned changes.

**VERIFIED** — reuse `EfValuationStore`, `ValuationPolicy`,
`IListCaseValuations`, existing dialog bindings in `wwwroot/js/site.js`, and
the `.gated` disabled-seam convention. Do not create a second source-label
list outside `OperatorLabels`.

**VERIFIED** — `site.css` has dialog, form-grid, panel, and gated primitives,
but no `valuation-card` or `suggest-btn` rules. Card/chip styling therefore
needs [[CASE-038]] coordination, not an unannounced CSS edit.

**VERIFIED** — routed-page changes normally require regenerated Test UI
snapshots. This ticket changes routed Case handlers and partial output, so it
triggers that rule; [[UIIMP-014]] owns `docs/design/test-ui/**` and must carry
the snapshot/catalogue update.

**ASSUMED** — [[AUTO-018]] will introduce the `MarketResearch` job kind and
AI valuation row before CASE-029 renders that source. Current code has no
`MarketResearch` occurrence under `src/Pegasus.Core/AiWork`.

## Operator-only open questions

None. The ticket and EPIC-012 decisions already fix the source list, disabled
seams, dialog fields, and exclusions.

## Wrapper checks (Claude, 2026-09-02)

Spot-checked in `C:/Users/PC/Documents/GitHub/pegasus` against
`origin/dev` `897db953` after the Codex run; every claim below held.

- `git merge-base --is-ancestor 450b9234 origin/dev` succeeds (PR #621,
  ENG-027); `git log origin/dev --grep=CASE-038 --grep=ENG-035` is empty.
  The frame ([[CASE-038]], backlog) and vocabulary ([[ENG-035]], preparing)
  blockers are not merged; ENG-027 is.
- Every existing path in the Files table resolves with
  `git cat-file -e origin/dev:<path>` (22 of 22).
- `Custody.cshtml` is two lines (`@page` and `@model`); the upload-request
  form lives in `_CaseDocuments.cshtml` lines 174–183 and posts
  `CustodyModel.OnPostCreateRequestUploadLinkAsync`; the chase form lives in
  `_CaseHistory.cshtml` lines 65–84 and posts
  `TasksModel.OnPostRecordManualChaseAsync`. The ticket's "Custody.*
  (dialog fields)" wording therefore maps to `_CaseDocuments.cshtml` +
  `Custody.cshtml.cs` and `_CaseHistory.cshtml` + `Tasks.cshtml.cs`.
- `grep -c 'valuation-card\|suggest-btn' site.css` = 0.
  `CaseDataEntities.cs:93` holds `CaseDataCodes.Suggestion = "suggestion"`.
  `DvlaDvsaProductionAdapter.cs:98,341` carry `dvla-ves+dvsa-mot-history`.
  `Program.cs:666,674` register `VehicleLookupAvailability`
  (`DevelopmentOfflineReplay` / `ProductionLive`); `DependencyInjection.cs:683`
  wires `DvlaDvsaProductionAdapter`.
- `OperatorLabels.cs` has no `Recipient` label anywhere; the mockup's
  `L.valuationSource` (`03-labels.js:100`) also lists CAP HPI, AutoTrader and
  Vehicle data, which D40 and the EPIC-012 non-goals exclude — port only
  Glass's, Cazana, Engineer's Value and AI market research.
- Mockup chip rule, exact: `21-case-sections.js:110` builds the suggestion
  map as `if (val && String(c.vehicle[k] || '') !== String(val))`; the chip
  is a `button.suggest-btn` inside the field label (`20-case.js:10`) whose
  `apply-suggestion` sets the field and deletes only that key. The mockup also
  defines `apply-all-suggestions` (`21-case-sections.js:122`) but renders no
  control for it; D34 names per-field chips only, so no "apply all" ships.
- Mockup dialog fields, exact: upload request = Case (select, required),
  Recipient (text, required, "Name or e-mail address"), read-only lines
  Expires / Files, Reason; record chase = Recipient (required, prefilled
  sender), Channel (E-mail / Telephone / Letter), Content (textarea),
  Outcome (Sent / Spoke to recipient / Left voicemail / No answer), Reason.
  Mapping to `ManualChaseRecord`: Recipient → `TargetPartyOrAddress`,
  Channel → `Channel`, Content → `Note`, Outcome → `Outcome`,
  Reason → `Reason`; `AttemptedAtUtc` stays server-supplied as today.
- The research checkout HEAD moved from `cad00be9` to `897db953` during the
  run via a `checkout origin/dev` (reflog); it stayed detached and clean, and
  897db953 is the DELIV-041 merge that records D29–D43 in the governing docs.
