# Plan — CASE-029 (2026-09-02, gpt-5.6-terra high)

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

Diff estimate: approximately 30 owned-file changes/creates, one serialized
migration, and no new dependency.

## Governing behaviour

[[EPIC-012]] D34 and D40 govern the lookup and valuation UI. The Case
identity, vehicle evidence, and operator-experience rules remain owned by
`docs/frd/frd-01-case-identity-and-lifecycle.md`,
`docs/frd/frd-06-vehicle-and-engineering-evidence.md`, and
`docs/frd/frd-12-operator-experience.md`.

Execution is sequenced after [[CASE-038]], [[ENG-035]], and the required
[[AUTO-018]] valuation vocabulary/input have merged into `dev`. This choice
ensures `_CaseValuation.cshtml` has its required production caller in
`Details.*` before CASE-029 is presented as wired; CASE-029 will not edit
`Details.*`.

## Step 1 — Persist and accept individual vehicle suggestions

- **Files:** `src/Pegasus.Core/Vehicle/VehicleWorkflow.cs`;
  `src/Pegasus.Infrastructure/Persistence/EfVehicleWorkflowStore.cs`;
  `src/Pegasus.Infrastructure/Persistence/EfVehicleLookupWorkStore.cs`.
- **Reuses:** `CaseDataValueKind.Suggestion`, `CaseField<T>`,
  `RequestVehicleLookup`, `VehicleLookupObservation`, the existing combined
  DVLA/DVSA provenance, and `CaseMutationPageModel` lease/version semantics.
- **Change:** Replace whole-observation acceptance with a narrow, keyed
  acceptance operation that applies one looked-up field and clears only that
  field's suggestion. On completed lookup, project each supported non-empty
  result into source-attributed case-data suggestions; retain the observation
  record and combined provider provenance. Do not create a second lookup,
  suggestion table, or bulk-apply path.
- **Test:** `tests/Pegasus.Core.Tests/Vehicle/VehicleWorkflowTests.cs` proves
  stale lease/version rejection, one-field acceptance, retained sibling
  suggestions, and cleared accepted suggestion.

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
  `RequestUploadLink`, `CreateRequestUploadLinkCommand`, and existing
  replay-safe request-link persistence.
- **Change:** Add and validate guide month through the valuation contract,
  entity, mapping, ordering, and one migration. Add Recipient and Reason to
  request-link creation, persistence, and case projection in that same
  migration. Preserve Engineer's Value authorization and keep Glass's
  valuation labels distinct from Glass's estimate-import labels.
- **Test:** `tests/Pegasus.Core.Tests/Assessment/ValuationTests.cs` proves
  guide-month validation and source boundaries;
  `tests/Pegasus.IntegrationTests/DocumentCustodyDurabilityTests.cs` proves
  metadata durability/replay; `tests/Pegasus.IntegrationTests/TypedCaseDataMigrationTests.cs`
  pins the migration.

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
  differs from the current field; each chip posts one field acceptance. Remove
  the checks panel/history table and whole-record accept/correct forms; keep
  Experian as the only disabled vehicle seam with its named condition.
- **Change:** Add source cards and an Add valuation dialog with Glass's,
  disabled Cazana (`not connected`), Engineer's Value, and the
  [[AUTO-018]] AI-market-research row shape. Show Retail, Trade, guide month,
  mileage, and date; do not add adjustments, rationale, or history.
- **Change:** Replace direct upload-link creation with a dialog containing
  Recipient, read-only policy values, and Reason. Render Record chase fields
  as Recipient, Channel, Content, Outcome, and Reason; no explanatory copy
  or empty-state panel is added.
- **Test:** `tests/Pegasus.IntegrationTests/CaseVehicleWebTests.cs` asserts
  the single action, difference-only chips, absent legacy controls, and
  disabled Experian seam. `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs`
  asserts card/dialog output and exact labels.

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
  `tests/Pegasus.IntegrationTests/TypedCaseDataMigrationTests.cs`.
- **Reuses:** `Vehicle.cshtml` handler pattern, `ISaveValuation`,
  `IListCaseValuations`, `CustodyModel.OnPostCreateRequestUploadLinkAsync`,
  `TasksModel.OnPostRecordManualChaseAsync`, and
  `RecordingCaseDetailsStore`.
- **Change:** Bind the one lookup action and per-field chip post through the
  existing mutation/PRG path. Add the valuation route and handlers with the
  existing lease, expected-version, operation-key, and authorization checks.
  Bind upload Recipient/Reason and map chase Recipient to
  `TargetPartyOrAddress`, Content to `Note`, with server-supplied
  `AttemptedAtUtc`.
- **Test:** Prove successful PRG and persisted values, validation failures,
  lease/version propagation, Cazana's lack of a handler, and that each chip
  clears only its selected suggestion.

## Shared locks and hand-offs

Acquire capacity-one locks in this order: `Persistence/Migrations/**`, then
`Pages/Cases/Shared/*`, then `Presentation/OperatorLabels.cs`. If a lock is
held, wait for its owner, refresh with `git merge --no-edit origin/dev`, and
retry; never rebase. Do not retain one lock while waiting for another.

[[CASE-038]] must supply the scrolling `Details.*` caller and valuation
projection for `_CaseValuation.cshtml`, plus `site.css` presentation for
valuation cards and suggestion chips. [[UIIMP-014]] must regenerate and commit
`docs/design/test-ui/**` snapshots and catalogue output after the routed-page
changes. [[AUTO-018]] owns the MarketResearch job kind, Automation Actor
completion, and AI-created valuation row. `Custody.cshtml` remains route-only.

## Design rules

All new visible strings belong in `Presentation/OperatorLabels.cs`. Use exact
state labels, labels/values only, and at most one destructive consequence
sentence. Cazana and Experian are the only disabled seams and must state their
conditions. Every other excluded capability is absent, not disabled.

## Out of scope (absent, not disabled)

- CAP HPI, AutoTrader, Vehicle data, and apply-all suggestions.
- Vehicle checks/history and whole-observation accept/correct UI.
- AI job creation, scraping, Automation Actor work, and AI row creation
  ([[AUTO-018]]).
- Valuation adjustments, rationale, revaluation history, and removal
  ([[TICK-083]] / EXT-10).
- Notes timeline work, `Details.*`, `site.css`, `site.js`,
  `docs/design/test-ui/**`, and `Custody.cshtml`.

## Simplification pass

to be recorded on the branch diff before the PR opens

## Verification commands

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"
./scripts/Test-MigrationGrants.ps1
./scripts/Update-TestUiSnapshots.ps1
./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture
./scripts/Test-UiCatalogue.ps1
```

## Acceptance conditions

- The Vehicle section renders exactly one lookup control labelled
  `Look up DVLA & MOT` posting `VehicleModel.OnPostRequestVehicleLookupAsync`,
  and one disabled Experian seam with its named condition; `Refresh DVLA`,
  `Refresh DVSA/MOT`, the `Vehicle checks` panel, the `Recorded checks`
  table and the whole-record Accept/Correct forms are absent from the
  response body.
- After a completed lookup, a chip renders beside a field only when the
  looked-up value is non-empty and differs from the stored value; posting a
  chip fills that field, clears only that field's suggestion, leaves sibling
  suggestions in place, and returns through the existing PRG path with the
  lease and expected version enforced.
- The Valuation section lists Glass's, Engineer's Value and (when the
  [[AUTO-018]] source exists on `origin/dev`) AI market research cards with
  Retail, Trade, guide month, mileage and date; the Add valuation dialog
  offers Glass's, Cazana (disabled, `not connected`, no handler) and
  Engineer's Value, with guide month and mileage per entry; no adjustments,
  rationale, history or remove control renders.
- Glass's valuation and Glass's estimate import keep separate
  `OperatorLabels` entries; no source list exists outside `OperatorLabels`.
- The upload-request dialog carries Recipient (required), the read-only
  policy values and Reason; a created link persists Recipient and Reason and
  the Case page projects them; identical replay returns the same link.
- The Record chase dialog carries Recipient, Channel, Content, Outcome and
  Reason mapped to `ManualChaseRecord.TargetPartyOrAddress`, `Channel`,
  `Note`, `Outcome`, `Reason`; `AttemptedAtUtc` stays server-supplied.
- Migration list, grants and snapshot are consistent:
  `./scripts/Test-MigrationGrants.ps1` passes and
  `TypedCaseDataMigrationTests` pins the new migration.
- The canonical commands exit 0; the Test UI verify and catalogue checks
  pass on the regenerated snapshots committed by [[UIIMP-014]].

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
- Sequencing note: the plan lists [[AUTO-018]] beside [[CASE-038]] and
  [[ENG-035]] as a merge precondition. The ticket's Blocked-by names only
  the frame, the vocabulary and [[ENG-027]]. Treat AUTO-018 as soft: if it
  is unmerged when this lane starts, the AI market research card is absent
  (not disabled) and the `OperatorLabels` entry for it is added by AUTO-018;
  the rest of the plan is unaffected.
- Codex ran read-only in `.worktrees/research`; `git status --porcelain`
  was empty and HEAD stayed at `897db953` throughout.

## Stop condition

Open the CASE-029 PR targeting `dev`, move the ticket to Review, and stop.
Do not merge it. CASE-029 is not declared wired or Done until [[CASE-038]] has
supplied the production `Details.*` caller.
