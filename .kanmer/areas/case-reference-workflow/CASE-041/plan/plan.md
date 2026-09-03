# Plan — CASE-041 (2026-09-02, gpt-5.6-terra high)

CASE-041 is planned from `origin/dev` at `897db953`, which includes the
DELIV-041 governing-doc merge. The live board has CASE-041 in Preparing; its
research and files documents exist, while plan and checklist are the remaining
Preparing exit gates. CASE-038 remains a blocking frame/shared-lock dependency.

## Governing behaviour and persistence decision

D33 governs the ordered Inspect at choices: Image Based Assessment, Claimant
address, Repairer location, Storage location, previous addresses for the
principal, and Manual entry. The Inspection section remains the sole edit
surface; no `Details.cshtml`, navigation, CSS, or JavaScript change is needed.

Use a `storage_location` confirmed field in `CaseDataFields`, not a `Cases`
column. The `Cases`-column route is technically available through
`CaseEntity`, but `20260828185508_ProviderDeclaredInstruction` demonstrates
the established additive field-name/constraint route used by `claimant_address`.
A Case-data field preserves the existing confirmed-value source/provenance
shape and keeps all editable Case data in one vocabulary. Add exactly one
additive migration that adds `storage_location` to
`CK_CaseDataFields_FieldName`; use the runtime-role guard and reassert the
existing Web and Worker object grants for `Cases` and `CaseDataFields` without
reducing or expanding either role's access.

Repairer location has no persisted production source. The only repairer-related
value is `costs.repairer_vat_registered`; it is not an address. Therefore,
Repairer location always renders as the D33-required disabled
`Repairer location · not recorded` option. It must not infer a repairer from
estimates, provider reference data, or the Overview Repairer/holder display.
A persisted repairer address is out of scope and requires a follow-up ticket.

## Constraints

- Wait for CASE-038 to merge before CASE-041 starts its frame-dependent work.
  Refresh the task branch with `git merge --no-edit origin/dev`.
- `Presentation/OperatorLabels.cs` is capacity-one: make its CASE-041 change a
  small serialized commit after CASE-038's edit has merged.
- `Persistence/Migrations/**` is capacity-one. Generate CASE-041's one
  migration only after the preceding serialized migration, including any
  CASE-039 or PLAT-068 migration ahead of it, has merged and the branch has
  refreshed from `origin/dev`.
- Do not modify `Pages/Cases/Details.cshtml`,
  `Pages/Cases/Shared/_CaseWorkspaceNav.cshtml`, `wwwroot/css/site.css`,
  `wwwroot/js/site.js`, `docs/design/test-ui/**`, governing docs,
  `docs/operator-notes.md`, or `corpus/`.
- Use no explanatory copy. All visible fixed labels live only in
  `Presentation/OperatorLabels.cs`; dynamic previous-address values remain
  values, not duplicated labels.
- Use the exact labels and state terms from D33. Missing values are disabled
  only inside this explicitly required Inspect at choice list; excluded
  capabilities remain absent.
- Reuse existing Case test values first. D43 permits mockup-derived fixture
  values only if an existing Case-test value cannot express the scenario.

## Ordered steps

### Step 1 — Serialize dependencies and establish the implementation base

- **Files:** none.
- **Reuse:** the existing wave/lock procedure; no new helper fits or is needed.
- Wait for CASE-038, then merge `origin/dev` with
  `git merge --no-edit origin/dev`. Inspect the migration head immediately
  before migration generation and wait for any earlier serialized migration to
  merge.
- Confirm the CASE-038 frame renders the existing inspection partial without
  changing frame-owned files.
- **Acceptance:** CASE-041 starts from a branch containing CASE-038 and the
  current merged migration head; no shared-lock file is edited concurrently.

### Step 2 — Add storage location and the focused choices contract

- **Files:**
  - `src/Pegasus.Core/Address/InspectionAddressResolution.cs`
  - `src/Pegasus.Core/Cases/CaseDataContracts.cs`
  - `src/Pegasus.Core/Cases/CaseDataOperations.cs`
- **Reuse:** `Ext18InspectionAddressPolicy.ImageBasedAssessment`,
  `CaseEditableData`, `CaseInspectionData`, and
  `CaseDataPolicy.Normalize`/`Text`.
- Add a narrow `IInspectionAddressChoicesQueries` contract and projection for
  the Case Inspection UI. Do not widen `ICaseDataQueries`, whose consumers
  include unrelated integrations and test doubles.
- Add nullable storage location to the editable and projected inspection data.
  Normalize it with the existing Case text policy; retain the existing paired
  inspection address/mode and exact Image Based Assessment invariants.
- Define choices in one order: Image Based Assessment, claimant, repairer,
  storage, distinct prior addresses, Manual entry. The repairer projection is
  always unavailable; the query contract does not invent a repairer source.
- **Acceptance:** Core can carry normalized storage location through the same
  editable-data shape as inspection address, and the narrow port can provide
  all values needed to render choices without changing broad Case reads.

### Step 3 — Persist storage and query principal history

- **Files:**
  - `src/Pegasus.Infrastructure/Persistence/CaseDataEntities.cs`
  - `src/Pegasus.Infrastructure/Persistence/CaseDataSnapshotFactory.cs`
  - `src/Pegasus.Infrastructure/Persistence/EfCaseDataStore.cs`
  - `src/Pegasus.Infrastructure/Persistence/InspectionAddressChoicesQueries.cs`
  - `src/Pegasus.Infrastructure/DependencyInjection.cs`
- **Reuse:** `CaseDataFieldNames`, confirmed `CaseDataFieldEntity` values,
  `EfCaseDataStore.SnapshotQuery`, `ApplyEditableData`, `EditableData`, `Map`,
  and scoped Infrastructure port registration.
- Add `CaseDataFieldNames.StorageLocation = "storage_location"` and map it
  through snapshot creation/projection and normal full-save persistence. Do
  not add a `CaseEntity` property or a `Cases` column.
- Implement the focused EF adapter. For the current Case, read claimant and
  storage locations; for prior choices, query same-principal cases, exclude the
  current Case and the exact Image Based Assessment sentinel, remove blanks,
  keep distinct address values by their newest confirmation, and order newest
  first. No history table is added.
- Register only the new focused query port.
- **Acceptance:** saving then reading a storage location preserves its
  confirmed field/source; principal history is distinct, excludes invalid
  candidates, and is newest-first.

### Step 4 — Add the single additive migration after serialization clears

- **Files:**
  - `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_CaseInspectionAddressChoices.cs`
  - `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_CaseInspectionAddressChoices.Designer.cs`
  - `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs`
- **Reuse:** the field-name constraint change pattern from
  `20260828185508_ProviderDeclaredInstruction`, runtime-role guard/grant style
  from `20260829212237_GrantProviderSubmissionAcceptRecovery`, and Case grant
  baseline from `20260814092852_AddWorkerCaseCreationGrants`.
- Generate exactly one EF migration after merging the then-current
  `origin/dev`. Add `storage_location` to
  `CK_CaseDataFields_FieldName` by the established drop/re-add constraint
  pattern. Include the SQL Server/runtime-role guard and reassert existing
  Web/Worker `Cases` and `CaseDataFields` object grants; add no new privilege
  and reduce none.
- **Acceptance:** the generated migration, designer, and model snapshot agree;
  the database accepts `storage_location`, rejects unsupported field names,
  and migration-grant verification passes.

### Step 5 — Render and bind the Inspection edit flow

- **Files:**
  - `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs`
  - `src/Pegasus.Web/Pages/Cases/CaseMutationPageModel.cs`
  - `src/Pegasus.Web/Pages/Cases/Shared/_CaseDataHiddenFields.cshtml`
  - `src/Pegasus.Web/Pages/Cases/Shared/_CaseInspectionAddress.cshtml`
  - `src/Pegasus.Web/Presentation/OperatorLabels.cs`
- **Reuse:** the routed `inspection-address` section, `OnPostSaveAsync`,
  `RetainableFormFields`, `_CaseDataHiddenFields.cshtml`, the existing
  one-lease edit form, and `OperatorLabels.CaseWorkspace`.
- Load the focused choices query only for the Inspection section. Bind and
  retain `storageLocation` in `DetailsModel` and `CaseMutationPageModel`.
  Add a hidden storage-location input to the shared full-save partial so an
  Overview or other section save cannot clear it.
- Put the editable storage-location input in the existing Inspection edit
  form, matching the mockup's `storageAddress` placement. This is the minimal
  ticket-owned placement.
- Render the ordered Inspect at select. Unavailable claimant, storage, and
  repairer values are disabled and suffixed with the exact
  ` · not recorded` state. Repairer remains disabled in every state.
- Selecting a recorded choice copies its value into the bound inspection
  address; Image Based Assessment shows Provider default instead of the address
  input. Manual entry preserves an existing physical input and clears only the
  Image Based Assessment sentinel. Read mode shows Inspect at, matched Source,
  and Provider default; unmatched values show Manual entry.
- Add only centralized label constants in one small serialized
  `OperatorLabels.cs` commit after CASE-038. Do not put literals in the
  partial, apart from dynamic address values.
- **Acceptance:** every drawn control posts to the existing save handler;
  storage survives all full saves; unavailable options are disabled only as
  D33 requires; no repairer address is manufactured; and no explanatory copy
  or parallel label list is introduced.

### Step 6 — Extend focused tests

- **Files:**
  - `tests/Pegasus.Core.Tests/Cases/CaseDataOperationsTests.cs`
  - `tests/Pegasus.IntegrationTests/InspectionAddressChoicesPersistenceTests.cs`
  - `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs`
  - `tests/Pegasus.IntegrationTests/CaseTasksWebTests.cs`
- **Reuse:** existing address/mode invariant tests, the SQL Server persistence
  harness, `RecordingCaseDetailsStore`, and
  `InspectionAddressEditorPostsEveryEditableValueWithTheTypedAddressFirst`.
- Cover storage normalization and persistence; the history query's distinct,
  newest-first, current-case-excluding and sentinel-excluding behaviour; and
  the exact Inspect at option ordering and read-mode source output.
- Assert unavailable choices are disabled with ` · not recorded`, including
  the permanently unavailable repairer option. Do not test a repairer choice
  as selectable: that would contradict D33 and current persisted data.
- Extend the full-save posting test to prove storage location is carried by
  visible binding and hidden fields. Cover Image Based Assessment and Manual
  behaviour, including preserving a physical manual address.
- **Acceptance:** tests prove the observable D33 flow and protect against an
  Overview save silently clearing storage location.

## Verification commands

Run, in wave order:

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category=Browser" -- xUnit.MaxParallelThreads=2
./scripts/Update-TestUiSnapshots.ps1
./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture
./scripts/Test-UiCatalogue.ps1
./scripts/Test-MigrationGrants.ps1
```

CASE-041 does not edit `docs/design/test-ui/**`. If snapshot capture or verify
detects this ticket's routed Razor change, regenerate on the merging branch per
the wave loop and hand the resulting snapshot/catalogue diff to UIIMP-014; if
that hand-off cannot be made cleanly, report the failure rather than committing
snapshot changes in CASE-041.

## Stop condition

Open the CASE-041 PR against `dev`, write the post-implementation report, and
move the ticket to Review. Do not merge the PR or begin UIIMP-014.

## Wrapper check (Claude, 2026-09-02)

Reused the gpt-5.6-terra (effort high) plan produced earlier this session
in `.worktrees/research` at `897db953` (origin/dev; `git status
--porcelain` clean afterwards). Checked against the research checkout:
`CaseDataSnapshotFactory.cs`, `CaseMutationPageModel.cs`,
`_CaseDataHiddenFields.cshtml`, `InspectionAddressResolution.cs`, the
`Text(...)` normaliser in `CaseDataOperations.cs`, and
`CaseDataFieldNames.ClaimantAddress = "claimant_address"` all exist; the
migration head is still `20260829212237`.

Corrections and dependencies the executor must carry:

- `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` and
  `src/Pegasus.Web/Pages/Cases/CaseMutationPageModel.cs` are outside the
  ticket's named owned paths but are required by `files.md` for the
  storage-location binding. `Details.cshtml.cs` is also changed by CASE-038
  (wave 2, merges first) and by CASE-039 (wave 3, same wave as this ticket):
  treat it as a serialized shared edit — smallest possible change (load the
  focused choices port for the `inspection-address` section, bind
  `storageLocation`), made only after CASE-038 has merged and refreshed in,
  and ordered against CASE-039 so the two wave-3 edits never land
  concurrently. Report the overlap in the post-implementation report.
- `_CaseDataHiddenFields.cshtml` is under the capacity-one
  `Pages/Cases/Shared/*` lock; the hidden `storageLocation` input is a
  one-line serialized edit under the same rule.
- The ticket's own verification line "Choosing Repairer fills the address"
  cannot be satisfied in this programme: no repairer address is recorded
  anywhere in production, so under D33 the option is always disabled
  (` · not recorded`). The observable acceptance is therefore: choosing
  Claimant address, Storage location or a previous address fills the
  address; Repairer location is present and disabled; Manual entry keeps the
  typed input; history lists distinct previous addresses newest first. A
  persisted repairer address is a follow-up ticket, not this one.

## Acceptance conditions

- Inspection edit form shows the Inspect at select in exactly the D33 order;
  options without a recorded value are disabled with the ` · not recorded`
  suffix, nothing else on the section is drawn disabled.
- Choosing a recorded option copies its value into the bound inspection
  address; Manual entry keeps a typed physical address; Image Based
  Assessment stores the exact `Ext18InspectionAddressPolicy.ImageBasedAssessment`
  value and shows Provider default.
- Read mode shows Inspect at, Source and Provider default; an unmatched
  value reads as Manual entry.
- Storage location is entered in the Inspection edit form, persisted as the
  `storage_location` confirmed field, and survives an Overview (or any other
  section) save.
- Same-principal history is distinct, excludes the current Case, blanks and
  the Image Based Assessment value, and is ordered newest first.
- One additive migration; `./scripts/Test-MigrationGrants.ps1` passes; Web
  and Worker grants on `Cases` and `CaseDataFields` are unchanged.
- No literal label in the partial beyond dynamic address values; all new
  constants live in `Presentation/OperatorLabels.cs`.
