# Plan — CASE-029 (2026-09-02, gpt-5.6-terra high; corrected 2026-09-03 after cross-model review)

## Premises verified

- `git status --porcelain; git rev-parse --verify HEAD; git rev-parse
  --verify origin/dev` — checkout is clean; both HEAD and `origin/dev` are
  `897db9530a45063e8f684f2800685afbfdced006`.
- `rg -n 'CaseDataValueKind|CaseField<T>|IRequestVehicleLookup|
  IAcceptVehicleSuggestion|VehicleSuggestionDecision' ...` — Core already has
  `Suggestion`, `CaseField<T>`, and the vehicle lookup/acceptance ports.
- `rg -n 'DvlaDvsaProductionAdapter|VehicleLookupAvailability\.ProductionLive'
  src/Pegasus.Infrastructure/DependencyInjection.cs src/Pegasus.Web/Program.cs`
  — production uses the combined DVLA/DVSA adapter; Web records requests.
- `rg -n 'OnPostRequestVehicleLookupAsync|OnPostAcceptVehicleSuggestionAsync|
  OnPostCreateRequestUploadLinkAsync|OnPostRecordManualChaseAsync' ...` —
  the existing mutation handlers are present in Vehicle, Custody, and Tasks.
- `Get-ChildItem .../Migrations -Filter '*CaseValuation*'` and
  `rg -n 'CaseValuations|RequestUploadLinks' ...ModelSnapshot.cs` — the
  existing valuation migration and both affected tables are in the snapshot.
- `rg -n 'RecordingRequestStore|RecordingAcceptStore|RecordingStore|
  RecordingCaseDetailsStore' tests/...` — the named Core fakes and shared
  web recording store already support these test seams.
- `Get-Content src/Pegasus.Core/Documents/RequestUploadPolicy.cs |
  Select-Object -Skip 230 -First 42` — request-link contracts lack Recipient
  and Reason; `TasksModel` already accepts the `ManualChaseRecord` fields.
- `rg -n 'valuation-card|suggest-btn' src/Pegasus.Web/wwwroot/css/site.css`
  and `rg -n 'section|CaseSection|SelectedSection' .../Details.*` — the
  required chip/card styles and scrolling Valuation caller are not present.

Corrected 2026-09-03 (review findings 1, 3, 4, 7, 9, 10):

- `EfVehicleLookupWorkStore.RecordOutcomeAsync` already calls the private
  `AddLookupSuggestionsAsync` (`EfVehicleLookupWorkStore.cs:204,296`), which
  writes `CaseDataCodes.Suggestion` rows for make, model, mileage and
  mileage unit with `SourceKind = CaseDataCodes.VehicleLookup` and the
  combined provider label. Suggestion persistence therefore exists; this
  ticket consumes it and does not build a second projection path.
- That helper skips a field that already carries a suggestion
  (`existing.Contains(fieldName)`), so a repeat lookup never overwrites a
  pending suggestion.
  `tests/Pegasus.IntegrationTests/VehicleLookupGapFillTests.cs` pins that
  rule; this ticket keeps it.
- `CaseVehicleData` (`Core/Cases/CaseDataContracts.cs:91-96`) holds only
  Registration, Make, Model, Mileage and MileageUnit, and
  `CaseDataFieldNames` (`Infrastructure/Persistence/CaseDataEntities.cs:40`)
  is enforced by a database check constraint. The mockup's other lookup
  values (colour, fuel, engine capacity, first registration, tax expiry,
  MOT expiry, transmission, manufacture year) have no case-data owner — see
  the open question.
- `ValuationSources.IsSupported` is `Enum.IsDefined`, and `ValuationPolicy`
  authorises only Engineer's Value specially, so Core currently accepts a
  `Cazana` row from any caller. A disabled seam must be refused in Core.
- `CaseField<T>.Current` is `Confirmed ?? Fact ?? Suggestion`
  (`CaseDataContracts.cs:61`), so a chip must compare the suggestion against
  `Confirmed ?? Fact`, never against `Current`.
- `EfDocumentRequestStore` replays creation by operation key through
  `DocumentActionHistory.RequireExactReplay` over `RequestUploadHistoryValue`
  (`EfDocumentRequestStore.cs:44,448,460`); that record holds no recipient or
  reason today.
- The migration list is pinned in
  `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs:116`,
  not in `TypedCaseDataMigrationTests.cs`.
- `tests/Pegasus.Core.Tests/Vehicle/VehicleWorkflowTests.cs` drives recording
  Core fakes, so it cannot prove EF persistence; the EF seams are
  `VehicleLookupGapFillTests.cs` and
  `tests/Pegasus.IntegrationTests/AssessmentPersistenceIntegrationTests.cs`
  (which already constructs `EfValuationStore`).

Diff estimate: approximately 30 owned-file changes/creates, one serialized
migration, and no new dependency.

## Governing behaviour

[[EPIC-012]] D34 and D40 govern the lookup and valuation UI. The Case
identity, vehicle evidence, and operator-experience rules remain owned by
`docs/frd/frd-01-case-identity-and-lifecycle.md`,
`docs/frd/frd-06-vehicle-and-engineering-evidence.md`, and
`docs/frd/frd-12-operator-experience.md`.

Execution is sequenced after [[CASE-038]] and [[ENG-035]] have merged into
`dev`. This choice ensures `_CaseValuation.cshtml` has its required production
caller in `Details.*` before CASE-029 is presented as wired; CASE-029 will not
edit `Details.*`.

[[AUTO-018]] is **not** a precondition and is not conditionally rendered.
CASE-029 renders one card per persisted valuation row, keyed by
`ValuationSource` and labelled from `OperatorLabels`, so the AI market
research card appears the moment AUTO-018 adds its enum member, its label and
its rows — with no edit to `_CaseValuation.cshtml`. The D35 "request AI market
research" action on the Valuation section belongs to AUTO-018, which adds it
to `_CaseValuation.cshtml` under the `Pages/Cases/Shared/*` lock after
CASE-029 merges. CASE-029 ships no AI source label, card variant, action or
handler.

## Step 1 — Consume and accept individual vehicle suggestions

- **Files:** `src/Pegasus.Core/Vehicle/VehicleWorkflow.cs`;
  `src/Pegasus.Infrastructure/Persistence/EfVehicleWorkflowStore.cs`.
- **Reuses:** `EfVehicleLookupWorkStore.AddLookupSuggestionsAsync` (already
  persists the suggestion rows — no second projection path is written),
  `CaseDataValueKind.Suggestion`, `CaseField<T>`, `CaseDataFieldNames`,
  `RequestVehicleLookup`, `VehicleLookupObservation`,
  `VehicleSuggestionAcceptancePolicy` (`VehicleWorkflow.cs:313`), and
  `CaseMutationPageModel` lease/version semantics.
- **Change:** Replace whole-observation acceptance with a narrow, keyed
  acceptance operation that applies one looked-up field and clears only that
  field's suggestion row. The key is a Core-owned field selector restricted to
  the fields `CaseVehicleData` owns — make, model, and mileage (mileage and
  its unit are accepted atomically as one key, because the figure without its
  unit states something the case does not hold). Retain the observation record
  and combined provider provenance. Do not create a second lookup, a second
  suggestion writer, a suggestion table, or a bulk-apply path.
- **Repeat-lookup rule (unchanged):** a later lookup does not overwrite a
  pending suggestion; the existing `existing.Contains(fieldName)` guard and
  the tests that pin it stay as they are.
- **Test:** `tests/Pegasus.Core.Tests/Vehicle/VehicleWorkflowTests.cs` proves
  stale lease/version rejection and command validation of the field key.
  `tests/Pegasus.IntegrationTests/VehicleLookupGapFillTests.cs` proves the
  persisted behaviour: one-field acceptance against the database, retained
  sibling suggestion rows, the cleared accepted suggestion, atomic
  mileage+unit acceptance, retained provenance, and the unchanged
  repeat-lookup rule.

## Step 2 — Add valuation guide month and upload-request metadata

- **Files:** `src/Pegasus.Core/Assessment/Valuations.cs`;
  `src/Pegasus.Infrastructure/Persistence/AssessmentEntities.cs`;
  `src/Pegasus.Infrastructure/Persistence/AssessmentModelConfiguration.cs`;
  `src/Pegasus.Infrastructure/Persistence/EfValuationStore.cs`;
  `src/Pegasus.Core/Documents/RequestUploadPolicy.cs`;
  `src/Pegasus.Infrastructure/Persistence/CustodyEntities.cs`;
  `src/Pegasus.Infrastructure/Persistence/CustodyModelConfiguration.cs`;
  `src/Pegasus.Infrastructure/Persistence/EfDocumentRequestStore.cs`;
  `src/Pegasus.Infrastructure/Persistence/EfCaseQueryStore.cs`;
  `src/Pegasus.Infrastructure/Persistence/Migrations/*_CaseValuationGuideMonthAndRequestUploadMetadata.cs`;
  `src/Pegasus.Infrastructure/Persistence/Migrations/*_CaseValuationGuideMonthAndRequestUploadMetadata.Designer.cs`;
  `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs`.
- **Reuses:** `ValuationDetails`, `ValuationPolicy`, `IValuationStore`,
  `RequestUploadLink`, `CreateRequestUploadLinkCommand`,
  `DocumentActionHistory.RequireExactReplay`, and existing replay-safe
  request-link persistence.
- **Change:** Add and validate guide month through the valuation contract,
  entity, mapping, ordering, and one migration. The migration adds columns
  only; it creates no table, so `Test-MigrationGrants.ps1` has no new grant to
  add — the same diff still carries the migration, its designer, and the
  snapshot.
- **Change (review finding 1):** `ValuationPolicy` gains the one Core-owned
  rule that says which sources a person may record by hand. Glass's and
  Engineer's Value are manually recordable; Cazana is a disabled seam and is
  refused in Core, so a crafted post cannot create one. The vocabulary stays
  the `ValuationSource` enum; only its display labels live in
  `OperatorLabels`.
- **Change (review finding 7):** Recipient and Reason are normalised and
  validated in `RequestUploadPolicy` (Core), carried into
  `RequestUploadHistoryValue` so they are part of the audited creation
  snapshot, and persisted and projected. An identical replay of the same
  operation key returns the same link; the same operation key with different
  recipient or reason is refused by `RequireExactReplay` rather than silently
  returning the earlier link.
- Preserve Engineer's Value authorization and keep Glass's valuation labels
  distinct from Glass's estimate-import labels.
- **Test:** `tests/Pegasus.Core.Tests/Assessment/ValuationTests.cs` proves
  guide-month validation, the manually-recordable source rule (a direct
  `Cazana` save is refused), and the Engineer's Value authority boundary;
  `tests/Pegasus.IntegrationTests/AssessmentPersistenceIntegrationTests.cs`
  proves guide-month save/edit/list/order round-trips against
  `EfValuationStore`;
  `tests/Pegasus.IntegrationTests/DocumentCustodyDurabilityTests.cs` proves
  metadata durability, identical replay, and refused conflicting replay;
  `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs` pins
  the new migration in the migration list.

## Step 3 — Render the Case sections and dialogs

- **Files:** `src/Pegasus.Web/Pages/Cases/Shared/_CaseVehicle.cshtml`;
  `src/Pegasus.Web/Pages/Cases/Shared/_CaseValuation.cshtml`;
  `src/Pegasus.Web/Pages/Cases/Shared/_CaseDocuments.cshtml`;
  `src/Pegasus.Web/Pages/Cases/Shared/_CaseHistory.cshtml`;
  `src/Pegasus.Web/Presentation/OperatorLabels.cs`.
- **Reuses:** existing panel, dialog, form-grid, `.gated`, and lease hidden
  field conventions; valuation/query projections; `ManualChaseRecord`.
- **Change:** Replace the two refresh controls with one exact
  `Look up DVLA & MOT` action. Render a chip only when a non-empty suggestion
  differs from `Confirmed ?? Fact` for that field (never from
  `CaseField.Current`, which falls back to the suggestion itself); each chip
  posts one field acceptance. Chips ship for the fields the case record owns —
  make, model, mileage (with unit). Remove the checks panel/history table and
  whole-record accept/correct forms; keep Experian as the only disabled
  vehicle seam with its named condition.
- **Change:** Add one source card per persisted valuation row, labelled from
  `OperatorLabels` and keyed by `ValuationSource`, plus an Add valuation
  dialog offering Glass's and disabled Cazana (`not connected`). Engineer's
  Value keeps its own authored entry, unchanged. Show Retail, Trade, guide
  month, mileage, and date; do not add adjustments, rationale, history, or a
  remove control. No AI source label, card variant or action is added here
  ([[AUTO-018]]).
- **Change:** Replace direct upload-link creation with a dialog containing
  Recipient, read-only policy values, and Reason. Render Record chase fields
  as Recipient, Channel, Content, Outcome, and Reason; no explanatory copy
  or empty-state panel is added.
- **Test:** `tests/Pegasus.IntegrationTests/CaseVehicleWebTests.cs` asserts
  the single action, difference-only chips, absent legacy controls, and
  disabled Experian seam.
  `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs` asserts card/dialog
  output and exact labels.

## Step 4 — Bind routes, commands, and regression coverage

- **Files:** `src/Pegasus.Web/Pages/Cases/Vehicle.cshtml.cs`;
  `src/Pegasus.Web/Pages/Cases/Valuation.cshtml`;
  `src/Pegasus.Web/Pages/Cases/Valuation.cshtml.cs`;
  `src/Pegasus.Web/Pages/Cases/Custody.cshtml.cs`;
  `src/Pegasus.Web/Pages/Cases/Tasks.cshtml.cs`;
  `tests/Pegasus.Core.Tests/Vehicle/VehicleWorkflowTests.cs`;
  `tests/Pegasus.Core.Tests/Assessment/ValuationTests.cs`;
  `tests/Pegasus.IntegrationTests/CaseVehicleWebTests.cs`;
  `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs`;
  `tests/Pegasus.IntegrationTests/DocumentCustodyDurabilityTests.cs`;
  `tests/Pegasus.IntegrationTests/VehicleLookupGapFillTests.cs`;
  `tests/Pegasus.IntegrationTests/AssessmentPersistenceIntegrationTests.cs`;
  `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs`.
- **Reuses:** `Vehicle.cshtml` handler pattern, `ISaveValuation`,
  `CustodyModel.OnPostCreateRequestUploadLinkAsync`,
  `TasksModel.OnPostRecordManualChaseAsync`, and
  `RecordingCaseDetailsStore`.
- **Change:** Bind the one lookup action and per-field chip post through the
  existing mutation/PRG path. Add the valuation route and handlers with the
  existing lease, expected-version, operation-key, and authorization checks.
  The route model is mutation-only and injects `ISaveValuation` alone; reading
  the valuation list stays with [[CASE-038]]'s `DetailsModel`, exactly as
  `Vehicle.cshtml.cs` reads nothing. Bind upload Recipient/Reason and map
  chase Recipient to `TargetPartyOrAddress`, Content to `Note`, with
  server-supplied `AttemptedAtUtc`.
- **Test:** Prove successful PRG and persisted values, validation failures,
  lease/version propagation, that a hand-recorded Cazana valuation is refused
  by Core, and that each chip clears only its selected suggestion.

## Shared locks and hand-offs

Acquire capacity-one locks in this order: `Persistence/Migrations/**`, then
`Pages/Cases/Shared/*`, then `Presentation/OperatorLabels.cs`, then
`docs/design/test-ui/**`. If a lock is held, wait for its owner, refresh with
`git merge --no-edit origin/dev`, and retry; never rebase. Do not retain one
lock while waiting for another. Regenerate the migration after the last
refresh so its timestamp sorts after every migration merged first.

[[CASE-038]] must supply the scrolling `Details.*` caller and valuation
projection for `_CaseValuation.cshtml`, plus `site.css` presentation for
valuation cards and suggestion chips. [[AUTO-018]] owns the MarketResearch job
kind, the `MarketResearch` valuation source and its label, the Automation
Actor completion, the AI-created valuation row, and the D35 "request AI market
research" action it adds to `_CaseValuation.cshtml` after this ticket merges.
`Custody.cshtml` remains route-only.

**Test UI snapshots (review finding 5):** this ticket changes routed Razor
output, so it regenerates `docs/design/test-ui/**` and **commits those files
in its own PR**, under the `docs/design/test-ui/**` capacity-one lock, as
`AGENTS.md` requires ("Commit `docs/design/test-ui/` with the page change").
[[UIIMP-014]] reconciles the catalogue across lanes afterwards; it does not
carry this ticket's snapshots.

## Design rules

All new visible strings belong in `Presentation/OperatorLabels.cs`. Use exact
state labels, labels/values only, and at most one destructive consequence
sentence. Cazana and Experian are the only disabled seams and must state their
conditions. Every other excluded capability is absent, not disabled.

## Out of scope (absent, not disabled)

- CAP HPI, AutoTrader, Vehicle data, and apply-all suggestions.
- Vehicle checks/history and whole-observation accept/correct UI.
- AI job creation, scraping, Automation Actor work, the `MarketResearch`
  source and label, the AI valuation row and the D35 request action
  ([[AUTO-018]]).
- Valuation adjustments, rationale, revaluation history, and removal
  ([[TICK-083]] / EXT-10).
- Extending `CaseVehicleData` beyond registration, make, model, mileage and
  mileage unit — see the open question.
- Notes timeline work, `Details.*`, `site.css`, `site.js`, and
  `Custody.cshtml`.

## Simplification pass

to be recorded on the branch diff before the PR opens

## Verification commands

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"
./scripts/Test-MigrationGrants.ps1
./scripts/Update-TestUiSnapshots.ps1
./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture
./scripts/Test-UiCatalogue.ps1
```

The canonical solution test command above is the delivery gate; the
Browser/non-Browser split forms in
[the runbook](docs/runbook.md#locked-restore-build-and-test) may be run in
addition, never instead.

## Acceptance conditions

- The Vehicle section renders exactly one lookup control labelled
  `Look up DVLA & MOT` posting `VehicleModel.OnPostRequestVehicleLookupAsync`,
  and one disabled Experian seam with its named condition; `Refresh DVLA`,
  `Refresh DVSA/MOT`, the `Vehicle checks` panel, the `Recorded checks`
  table and the whole-record Accept/Correct forms are absent from the
  response body.
- After a completed lookup, a chip renders beside make, model or mileage only
  when the suggestion is non-empty and differs from `Confirmed ?? Fact`;
  posting a chip fills that field, clears only that field's suggestion row
  (mileage and unit together), leaves sibling suggestions in place, and
  returns through the existing PRG path with the lease and expected version
  enforced. A repeat lookup leaves a pending suggestion untouched.
- The Valuation section renders one card per persisted valuation row with
  Retail, Trade, guide month, mileage and date; the Add valuation dialog
  offers Glass's and Cazana (disabled, `not connected`, no handler); no
  adjustments, rationale, history or remove control renders, and no AI source
  label, card or action is present.
- A `Cazana` valuation submitted directly to `ISaveValuation` is refused by
  `ValuationPolicy`, proved by a test.
- Glass's valuation and Glass's estimate import keep separate
  `OperatorLabels` entries; no source list exists outside the
  `ValuationSource` enum and its `OperatorLabels` label map.
- The upload-request dialog carries Recipient (required), the read-only
  policy values and Reason; a created link persists Recipient and Reason and
  the Case page projects them; identical replay returns the same link, and the
  same operation key with different recipient or reason is refused.
- The Record chase dialog carries Recipient, Channel, Content, Outcome and
  Reason mapped to `ManualChaseRecord.TargetPartyOrAddress`, `Channel`,
  `Note`, `Outcome`, `Reason`; `AttemptedAtUtc` stays server-supplied.
- Migration list, grants and snapshot are consistent:
  `./scripts/Test-MigrationGrants.ps1` passes and
  `IntakePersistenceIntegrationTests` pins the new migration.
- The canonical commands exit 0; the Test UI verify and catalogue checks pass
  on the regenerated snapshots committed **in this PR**.

## Wrapper checks (Claude, 2026-09-02)

Spot-checked in `C:/Users/PC/Documents/GitHub/pegasus` against `origin/dev`
`897db953` after the Codex run:

- Handler names in Step 4 exist: `VehicleModel.OnPostRequestVehicleLookupAsync`
  and `OnPostAcceptVehicleSuggestionAsync` (`Vehicle.cshtml.cs:21,43`),
  `CustodyModel.OnPostCreateRequestUploadLinkAsync` (`Custody.cshtml.cs:119`),
  `TasksModel.OnPostRecordManualChaseAsync` (`Tasks.cshtml.cs:169`);
  `CaseMutationPageModel` at `Pages/Cases/CaseMutationPageModel.cs:19`.
- `ValuationSource` is `Glasses`, `Cazana`, `EngineersValue`
  (`Valuations.cs:8-13`); `ISaveValuation`, `IEditValuation`,
  `IListCaseValuations`, `IValuationStore` exist (`Valuations.cs:161-190`).
- `RequestUploadLink` and `CreateRequestUploadLinkCommand`
  (`RequestUploadPolicy.cs:237-255`) carry no Recipient/Reason, as the plan
  states. `ManualChaseRecord` (`Core/Tasks/CaseWorkScheduling.cs:31`) has
  the five mapped fields plus `AttemptedAtUtc`.
- Latest migration on `origin/dev` is
  `20260829212237_GrantProviderSubmissionAcceptRecovery`; the new migration
  timestamp must sort after it and after any migration merged first under
  the `Migrations/**` lock.
- `_CaseDocuments.cshtml` is reached through `_CaseFiles.cshtml:17`, which
  `Details.cshtml:318` includes; `_CaseHistory` is included at
  `Details.cshtml:314`; `_CaseVehicle` at `Details.cshtml:322`. Those three
  partials already have a production caller. `_CaseValuation.cshtml` has
  none until [[CASE-038]] includes it, which is why the plan sequences after
  the frame.
- All six test files in Step 4 exist on `origin/dev`.
- Codex ran read-only in `.worktrees/research`; `git status --porcelain`
  was empty and HEAD stayed at `897db953` throughout.

## Plan review (2026-09-03, gpt-5.6-sol xhigh; dispositions Claude Opus)

Read-only review at `897db9530a45063e8f684f2800685afbfdced006`; the research
checkout stayed clean. Verdict: REQUEST CHANGES, eight findings; two further
findings were added by the disposition pass. The reviewer confirmed no plan
item assumes a D44 staff-review flag or a D45 damage type, that no new package
is added, and that no D46 cropper work sits in CASE-029's paths.

| # | Severity | Plan step | Finding | Disposition |
| --- | --- | --- | --- | --- |
| 1 | blocker | Steps 2–4 | `ValuationSources.IsSupported` is `Enum.IsDefined`, so Core accepts a `Cazana` row; "Cazana has no handler" was a UI-only claim. | Fixed. Step 2 adds the Core rule for manually recordable sources; acceptance and the Step 4 tests prove a direct `Cazana` save is refused. |
| 2 | blocker | Governing behaviour, Step 3, acceptance | AUTO-018 was both a hard precondition and "soft"; D40's AI source and D35's request action were conditional or missing. | Fixed. AUTO-018 is no longer a precondition. Cards render generically per `ValuationSource`, so the AI card appears when AUTO-018 merges; the D35 action is written as an explicit AUTO-018 hand-off into `_CaseValuation.cshtml` after this ticket merges. Conditional acceptance wording removed. |
| 3 | blocker | Step 1 | `EfVehicleLookupWorkStore.AddLookupSuggestionsAsync` already writes the suggestion rows and is pinned by `VehicleLookupGapFillTests`; the plan risked a parallel path, compared chips against `CaseField.Current` (which falls back to the suggestion), and named vehicle fields the case record does not own. | Fixed. Step 1 reuses that helper by name and writes no second projection; the chip rule compares against `Confirmed ?? Fact`; mileage and unit are one atomic key. The remaining mockup vehicle fields have no Core owner — raised as the open question rather than silently added. |
| 4 | blocker | Steps 1–2 | `VehicleWorkflowTests` and `ValuationTests` drive recording fakes and cannot prove EF persistence, ordering or provenance. | Fixed. `VehicleLookupGapFillTests`, `AssessmentPersistenceIntegrationTests` and `IntakePersistenceIntegrationTests` are now named in the steps, the files list and the checklist. |
| 5 | blocker | Verification, hand-offs | The plan ran the snapshot generator but deferred committing `docs/design/test-ui/**` to UIIMP-014, against AGENTS.md; it also substituted `Category!=Corpus&Category!=Browser` for the canonical test command. | Fixed. CASE-029 takes the `docs/design/test-ui/**` lock and commits its own regenerated snapshots in the same PR; the canonical `Category!=Corpus` command is restored as the gate. |
| 6 | blocker | Step 2, shared locks | CASE-029's `Migrations/**` writes are not disjoint from CASE-039/040/041. | Rejected. EPIC-012's Constraints make `Persistence/Migrations/**` a capacity-one shared lock with migrations serialized, and the wave-3 brief itself gives three sibling lanes "one migration" each. Serialization, not disjointness, is the governing rule. The plan now additionally requires regenerating the migration after the final `origin/dev` refresh so timestamps sort correctly. |
| 7 | blocker | Step 2 | Recipient/Reason were columns only; `RequestUploadHistoryValue` and `RequireExactReplay` would let the same operation key with different metadata return the earlier link. | Fixed. Both fields are normalised and validated in Core, carried into the audited creation snapshot, and both identical and conflicting replay are tested. |
| 8 | nit | Step 4 | `IListCaseValuations` in the mutation route is a second unused reader. | Fixed. The route injects `ISaveValuation` only; reading stays with CASE-038's `DetailsModel`. |
| 9 | should-fix (Claude) | Step 2 test list | The migration list is pinned in `IntakePersistenceIntegrationTests.cs:116`, not `TypedCaseDataMigrationTests.cs`. | Fixed. Test file corrected in the plan, the files list and the checklist. |
| 10 | should-fix (Claude) | Step 1 | The plan did not say what a second lookup does to a pending suggestion; the code's rule is "first suggestion wins". | Fixed. Stated explicitly and kept; a test pins it. |

Open question raised by this review: the mockup offers chips for colour, fuel,
engine capacity, first registration, tax expiry, MOT expiry, transmission and
manufacture year, but `CaseVehicleData` owns none of them and the field-name
allow-list is a database check constraint. Whether CASE-029 extends the case
vehicle record or a separate ticket does is a product-scope decision and is
recorded in `open-questions/`.

## Stop condition

Open the CASE-029 PR targeting `dev`, move the ticket to Review, and stop.
Do not merge it. CASE-029 is not declared wired or Done until [[CASE-038]] has
supplied the production `Details.*` caller.

## Resolutions (2026-09-03) — vehicle fields stay narrow

The operator answered the open question: a separate ticket owns the vehicle
record extension. For CASE-029 the plan's narrow answer stands and is now
binding:

1. **Chips for make, model and mileage only** — the fields `CaseVehicleData`
   already owns. No Core contract change, no `CaseDataFieldNames` allow-list
   change, no check-constraint change, no migration in this ticket.
2. **The lookup port is designed for reuse.** [[CASE-043]] consumes the same
   DVLA/MOT port from intake, so the port returns the full looked-up record
   and this ticket simply renders chips for the subset the case can persist.
   No second client and no second field list.
3. **[[CASE-043]]** "Extend the case vehicle record with the DVLA/MOT fields,
   populated from the instruction first and DVLA/DVSA on intake" is filed
   (EPIC-012 + EPIC-011, Backlog) and is blocked by CASE-029.

## Resolutions (2026-09-04, controller) — scope on the merged frame

The implementation wrapper stopped on three items the 2026-09-02/03 documents
did not foresee, because they were written before CASE-038 merged and under
the old capacity-one lock rule. Under EPIC-012 `context.md` §Build policy
(2026-09-04) and CASE-038's merged contract, all three are CASE-029's own
scope; the "must not touch `Details.cshtml`/`.cs`" lines above are read as
"merge after CASE-038", which has happened (`ddbbc5e8`).

1. **Wire the Valuation section.** `Details.cshtml` on `dev` carries the
   `section-valuation` host whose placeholder names CASE-029 as the lane that
   fills it. CASE-029 composes `_CaseValuation.cshtml` there and adds the
   valuation read to `DetailsModel` (the existing `IListCaseValuations` port
   from ENG-027, one read inside the existing section load — no second
   query path; lazily mounted through the `/Cases/{id}/Section` fragment if
   the section is deferred, exactly as the Files body is). Owned paths gain
   `src/Pegasus.Web/Pages/Cases/Details.cshtml` (the valuation include point
   only) and `Details.cshtml.cs` (the valuation projection only).
2. **Recipient/Reason on the upload-request summary.** Step 2 projects
   Recipient and Reason through `EfCaseQueryStore.cs`; the record it
   projects into, `CaseRequestUploadSummary` in
   `src/Pegasus.Core/Cases/CaseQueries.cs`, gains the two nullable members.
   Owned paths gain that file for those members only (CASE-009 has merged
   its own `CaseQueries.cs` change, so merge `origin/dev` first).
3. Everything else in the plan stands; CASE-029 merges after CASE-040 in the
   queue and regenerates its migration at merge prep if `dev`'s tail moved.

## Simplification pass (2026-09-05)

Ran gpt-5.6-sol (low) over the branch's uncommitted diff against `origin/dev`
(reuse, simplification, efficiency, altitude lenses). Two findings, both
applied:

1. `src/Pegasus.Core/Vehicle/VehicleWorkflow.cs` (`AcceptVehicleSuggestion.ExecuteAsync`)
   — the normalized `command with { ... }` expression redundantly set
   `Correction = null`; the preceding guard already refuses any command whose
   `Correction` is non-null when `Decision == Accept`, so the value is always
   already null at that point. Removed the redundant assignment.
   Behaviour-preserving; Core and the focused integration suite re-passed
   after the change.
2. `src/Pegasus.Infrastructure/Persistence/EfVehicleWorkflowStore.cs`
   (`AcceptOnceAsync`) — `observation.Id.ToString("D")` was computed twice
   (once building the suggestion-fields query predicate, again when writing
   confirmed fields). Hoisted a single `sourceIdentity` local computed once
   after `MapObservation` and reused it in both places. Behaviour-preserving
   (same value, same SQL parameter, same persisted `SourceIdentity`);
   `VehicleLookupGapFillTests` re-passed after the change.

Re-ran after applying: `dotnet build ./Pegasus.slnx --configuration Release`
(0 errors), `Pegasus.Core.Tests` (1225/1225 passed),
`Pegasus.ArchitectureTests` (100/100 passed), and the six focused integration
classes (`VehicleLookupGapFillTests`, `AssessmentPersistenceIntegrationTests`,
`CaseVehicleWebTests`, `CaseDetailsWebTests`, `DocumentCustodyDurabilityTests`,
`IntakePersistenceIntegrationTests`) — 110/110 passed.

No other findings reported (reuse, altitude, and correctness were explicitly
out of scope for this pass; no correctness issue was raised).

## Implementation notes and deviations (2026-09-05)

An earlier Codex attempt (packet predating the 2026-09-04 controller
Resolutions) stopped with outcome WAITING because its packet still forbade
editing `Details.cshtml`/`.cs`. The packet was corrected to include the
2026-09-04 Resolutions/Correction sections verbatim and owned-path list, and
Codex (gpt-5.6-sol, medium) completed the implementation on retry.

During verification (not trusting Codex's own numbers), the wrapper found and
fixed, within owned files only:

- `_CaseVehicle.cshtml` used `details.Data!` (null-forgiving) and then
  dereferenced `data.Vehicle...` without null-conditional access, throwing a
  `NullReferenceException` on any case whose `CaseDataProjection? Data` is
  null (e.g. `CaseReportApprovalWebTests`'s fixture). Reverted to the
  pre-existing nullable `data` pattern with `data?.Vehicle...` throughout.
- `VehicleWorkflowTests.AcceptanceRequiresAnExplicitReasonAndSupportedField`
  asserted `ArgumentException` for an invalid `Field`, but the production
  code (consistent with the adjacent `Decision` check) throws the more
  specific `ArgumentOutOfRangeException`; xUnit's `ThrowsAsync` requires an
  exact type match. Corrected the test's expected exception type.
- `VehicleLookupGapFillTests.AcceptingOneSuggestionClearsOnlyThatFieldAndMileageIsAtomic`
  (a new test for this ticket) hard-coded `expectedVersion: 0` for the first
  lease claim, but the seeded case is already at version 1 by the time of
  that claim (an existing, unrelated `EfVehicleLookupWorkStore` behaviour
  recording the lookup outcome bumps the workflow version). Read the actual
  `CaseWorkflows.Version` from the database first, matching the existing
  convention used elsewhere in the integration suite.
- `DocumentCustodyDurabilityTests.RequestUploadMetadataPersistsProjectsAndParticipatesInReplay`
  (new) exercised `ICreateRequestUploadLink` without an accepted
  `RequestUploadLimits`, so production composition resolved
  `UnavailableDocumentRequestStore` and the test failed closed as designed.
  `LocalDbTestDatabase` (in the owned `IntakePersistenceIntegrationTests.cs`)
  had no way to pass a `requestUploadLimitsFactory` through to
  `AddPegasusInfrastructure`, unlike its existing `localArtifactRootFactory`
  parameter. Added the matching optional parameter, threaded exactly like
  the existing one, and used it from the new test — the same pattern
  `ProductionCompositionTests.cs` already uses directly against
  `AddPegasusInfrastructure`.
- `CaseDetailsWebTests`: `TheRecordRendersElevenOrderedSectionHostsAndJumpLinks`
  and `TheSectionFragmentRefusesKeysItDoesNotServe` had hard-coded
  expectations from before `valuation` became a served, deferred section;
  updated the deferred-sections list and removed `valuation` from the
  refused-keys `[InlineData]`.
- `_CaseHistory.cshtml`'s Record-chase form dropped the hidden
  `attemptedAtUtc` field while adding the Recipient/Content fields; the
  handler still needs a rendered value for
  `ManualChasePostUsesAntiforgeryServerActorLiveLeaseVersionAndReplayKey` to
  read (the handler itself correctly ignores any posted value and stamps
  `timeProvider.GetUtcNow()` per the plan's "server-supplied" requirement).
  Restored the hidden field. Also updated the test's `ManualChaseForm` helper,
  which still posted the pre-rename `targetPartyOrAddress`/`note` field
  names instead of the new `recipient`/`content` names the handler now binds.
- `CaseVehicleWebTests.VehicleSectionDrawsOneLookupAndNoLegacyChecksSurface`
  asserted the plain-text label `"Look up DVLA & MOT"` against rendered HTML,
  where Razor correctly HTML-encodes `&` to `&amp;`. Corrected the assertion
  to compare against the HTML-encoded form.

`docs/design/test-ui/catalogue.json` needed one added entry for the new
`Valuation.cshtml` route (protocol/POST-only, same shape as the adjacent
`Vehicle.cshtml` entry) — `Test-UiCatalogue.ps1` failed until it was added.

No scope was added beyond the owned-path table plus this one catalogue entry
and the `LocalDbTestDatabase` parameter (both squarely within the already-owned
`docs/design/test-ui/**` and `IntakePersistenceIntegrationTests.cs` files).
