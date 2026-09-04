# Plan — CASE-041 (2026-09-02, gpt-5.6-terra high; revised 2026-09-03 after plan review)

CASE-041 is planned from `origin/dev` at `897db953`, which includes the
DELIV-041 governing-doc merge. The live board has CASE-041 in Preparing; its
research and files documents exist, while plan and checklist are the remaining
Preparing exit gates. PLAT-070 and CASE-038 are blocking base dependencies.

## Governing behaviour and persistence decision

D33 governs the ordered Inspect at choices: Image Based Assessment, Claimant
address, Repairer location, Storage location, previous addresses for the
principal, and Manual entry. The Inspection section remains the sole edit
surface; no `Details.cshtml` or `site.css` change is needed. A small
serialized `site.js` binder is required (see Step 5).

Use a `storage_location` confirmed field in `CaseDataFields`, not a `Cases`
column. This reconciles the ticket Approach's shorthand "one column with
grants": the Approach is advisory, the Verification lines are the acceptance,
and neither route changes observable behaviour. The `Cases`-column route is
technically available through `CaseEntity`, but
`20260828185508_ProviderDeclaredInstruction` demonstrates the established
additive field-name/constraint route used by `claimant_address`. A Case-data
field preserves the existing confirmed-value source/provenance shape and keeps
all editable Case data in one vocabulary. The field-name vocabulary is a
single list — `CaseDataFieldNames.All` in `CaseDataEntities.cs`, from which
`CaseDataModelConfiguration.cs` generates `CK_CaseDataFields_FieldName` — so
adding the name in one place drives the model; `CaseDataModelConfiguration.cs`
needs no edit.

**Grants.** The migration creates no table, and `CaseDataFields` and `Cases`
already carry object-level `SELECT/INSERT/UPDATE` (web) and `SELECT/UPDATE`
(worker) grants, which a new *row value* inherits. There is therefore no
permission delta, and `scripts/Test-MigrationGrants.ps1` only asserts that
migration-created tables are granted. Emitting fresh GRANT SQL would be ritual
work and a second copy of the permission matrix. The migration carries the
constraint drop/re-add only; the plan records here that no permission delta
exists, and `Test-MigrationGrants.ps1` still runs as proof.

Repairer location has no persisted production source. The only repairer-related
value is `costs.repairer_vat_registered`; it is not an address, and repairer
reference data is TICK-034 (backlog, post-alpha, not designated). Therefore,
under D33's "options without a value are disabled", Repairer location renders
as a disabled `Repairer location · not recorded` option in every current
state. It must not infer a repairer from estimates, provider reference data,
or the Overview Repairer/holder display. **This contradicts the ticket body's
own Verification line "Choosing Repairer fills the address", which no
implementation in this programme can satisfy. That contradiction is an open
question for the operator (see `open-questions/`); the disabled rendering above
is the recorded working default until it is answered.**

## Constraints

- The implementation base must contain **PLAT-070** (it edits
  `CaseDataOperations.cs`, `EfCaseDataStore.cs`, `CaseMutationPageModel.cs`,
  `_CaseWorkflow.cshtml` and `CaseDataOperationsTests.cs` — all files CASE-041
  also touches) and **CASE-038** (the frame, the `site.js` binder and the
  section-key rename). Refresh with `git merge --no-edit origin/dev`.
- **Section key.** CASE-038 deletes the `inspection-address` section key
  without an alias and replaces it with `inspection`, and renames the edit form
  to `id="case-inspection-address-form"` with `data-edit-save` removed. Every
  CASE-041 reference — the query-port load condition, the partial, and the test
  URLs — uses `?section=inspection` and that form id.
- `Presentation/OperatorLabels.cs` and `wwwroot/js/site.js` are capacity-one:
  make each CASE-041 change a small serialized commit after CASE-038's edits
  have merged.
- `Persistence/Migrations/**` is capacity-one. Generate CASE-041's one
  migration only after the preceding serialized migration, including any
  CASE-039 or PLAT-068 migration ahead of it, has merged and the branch has
  refreshed from `origin/dev`.
- **Concurrent wave-3 whole-file overlaps.** `Details.cshtml.cs` (CASE-039),
  `DependencyInjection.cs` (CASE-039) and `CaseDetailsWebTests.cs` (CASE-039,
  CASE-040, CASE-029) are edited by other live wave-3 lanes. Agree a written
  single-owner ordering with those lanes **in Step 1, before any edit** —
  post-implementation reporting is not authorization. Keep each CASE-041 change
  to the smallest additive hunk.
- Do not modify `Pages/Cases/Details.cshtml`,
  `Pages/Cases/Shared/_CaseWorkspaceNav.cshtml`, `wwwroot/css/site.css`,
  governing docs, `docs/operator-notes.md`, or `corpus/`.
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
- Wait for PLAT-070 and CASE-038, then merge `origin/dev` with
  `git merge --no-edit origin/dev`. Inspect the migration head immediately
  before migration generation and wait for any earlier serialized migration to
  merge.
- Record a written single-owner ordering with CASE-039, CASE-040 and CASE-029
  for `Details.cshtml.cs`, `DependencyInjection.cs` and `CaseDetailsWebTests.cs`
  before touching any of them.
- Confirm the merged CASE-038 frame renders the inspection partial under the
  `inspection` section key with form id `case-inspection-address-form`.
- **Acceptance:** CASE-041 starts from a branch containing PLAT-070, CASE-038
  and the current merged migration head; a written ordering exists for each
  concurrently-owned file; no shared-lock file is edited concurrently.

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
  - `src/Pegasus.Infrastructure/Persistence/EfCaseDataStore.cs`
  - `src/Pegasus.Infrastructure/Persistence/InspectionAddressChoicesQueries.cs`
  - `src/Pegasus.Infrastructure/DependencyInjection.cs` (serialized with
    CASE-039)
- **Reuse:** `CaseDataFieldNames`, confirmed `CaseDataFieldEntity` values,
  `EfCaseDataStore.SnapshotQuery`, `ApplyEditableData`, `EditableData`, `Map`,
  and scoped Infrastructure port registration.
- Add `CaseDataFieldNames.StorageLocation = "storage_location"` to the single
  `All` list and map it through projection and normal full-save persistence.
  Do not add a `CaseEntity` property or a `Cases` column.
  `CaseDataSnapshotFactory.cs` is **not** changed: it runs only at case
  acceptance and has no storage-location source (verified — it writes only the
  resolved inspection address and the provider inspection mode).
- Implement the focused EF adapter. For the current Case, read claimant and
  storage locations; for prior choices, query same-principal cases, exclude the
  current Case and the exact Image Based Assessment sentinel, remove blanks,
  keep distinct address values by their newest confirmation, and order newest
  first. No history table is added.
- Register only the new focused query port.
- **Acceptance:** saving then reading a storage location preserves its confirmed
  field/source; principal history is distinct, excludes invalid candidates, and
  is newest-first.

### Step 4 — Add the single additive migration after serialization clears

- **Files:**
  - `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_CaseInspectionAddressChoices.cs`
  - `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_CaseInspectionAddressChoices.Designer.cs`
  - `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs`
- **Reuse:** the field-name constraint change pattern from
  `20260828185508_ProviderDeclaredInstruction`.
- Generate exactly one EF migration after merging the then-current
  `origin/dev`. Add `storage_location` to `CK_CaseDataFields_FieldName` by the
  established drop/re-add constraint pattern.
- **No grant SQL.** Per the grants note above, no table is created and no
  permission delta exists; do not restate the permission matrix.
- **Acceptance:** the generated migration, designer, and model snapshot agree;
  the database accepts `storage_location` and rejects unsupported field names;
  `./scripts/Test-MigrationGrants.ps1` passes.

### Step 5 — Render and bind the Inspection edit flow

- **Files:**
  - `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` (serialized with CASE-039)
  - `src/Pegasus.Web/Pages/Cases/CaseMutationPageModel.cs`
  - `src/Pegasus.Web/Pages/Cases/Shared/_CaseDataHiddenFields.cshtml`
  - `src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkflow.cshtml`
  - `src/Pegasus.Web/Pages/Cases/Shared/_CaseInspectionAddress.cshtml`
  - `src/Pegasus.Web/Mcp/AssessmentMcpTools.cs`
  - `src/Pegasus.Web/Presentation/OperatorLabels.cs` (serialized)
  - `src/Pegasus.Web/wwwroot/js/site.js` (serialized)
- **Reuse:** the routed `inspection` section, `OnPostSaveAsync`,
  `RetainableFormFields`, `_CaseDataHiddenFields.cshtml`, the
  `case-inspection-address-form` one-lease edit form,
  `OperatorLabels.CaseWorkspace`, and CASE-038's root-scoped idempotent
  `bind(root)` binder.
- Load the focused choices query only for the `inspection` section. Bind and
  retain `storageLocation` in `DetailsModel` and add it to
  `CaseMutationPageModel.RetainableFormFields`.
- **Full-save preservation — three callers, not one.** `SaveCase` overwrites
  every editable value and `SetConfirmed(null)` deletes the field, so storage
  location must be carried by **every** caller that builds a whole
  `CaseEditableData`:
  1. `_CaseDataHiddenFields.cshtml` — the Inspection form's hidden list.
  2. `_CaseWorkflow.cshtml` — the Overview/workflow form has its **own** hidden
     field list (it does not include the shared partial); without a hidden
     `storageLocation` there, an Overview save silently clears it.
  3. `Mcp/AssessmentMcpTools.cs` — builds the whole replacement
     `CaseEditableData` positionally; pass
     `storageLocation ?? current...StorageLocation.Confirmed?.Value` in the same
     merge style as every other member.
- Put the editable storage-location input in the existing Inspection edit form.
  (The mockup has no editable storage input — `storageAddress` appears only in
  fixture state and `inspectAtOptions` — so this placement is CASE-041's own
  minimal ticket-owned decision, not a mockup transcription.)
- Render the ordered Inspect at select. Unavailable claimant, storage, and
  repairer values are disabled and suffixed with the exact ` · not recorded`
  state. Repairer remains disabled in every state.
- **Selection mechanism.** The deployed CSP is `default-src 'self'` with no
  nonce or hash, so an inline `<script>` is silently discarded in Production;
  `wwwroot/js/site.js` is the only enhancement home. Add one small
  progressive-enhancement block there, in the file's existing idiom (compare the
  INTK-022 `form[data-auto-submit]` select handler) and mounted through
  CASE-038's root-scoped idempotent `bind(root)` so a lazily mounted section is
  bound exactly once: on change of the Inspect at select, copy the chosen
  option's `data-` address value into the bound inspection-address input, switch
  the Image Based Assessment presentation to Provider default, and on Manual
  entry preserve an existing physical input while clearing only the Image Based
  Assessment sentinel. **Without JavaScript the section still works**: the
  address stays an ordinary free-text input posting to the same save handler,
  exactly as it does today — the select is a convenience, never the only way to
  set the value.
- Read mode shows Inspect at, matched Source, and Provider default; unmatched
  values show Manual entry.
- Add only centralized label constants in one small serialized
  `OperatorLabels.cs` commit after CASE-038. Do not put literals in the partial,
  apart from dynamic address values.
- **Acceptance:** every drawn control posts to the existing save handler;
  storage survives an Inspection save, an Overview save and an Automation MCP
  update; unavailable options are disabled only as D33 requires; no repairer
  address is manufactured; the section is usable with JavaScript disabled; and
  no explanatory copy or parallel label list is introduced.

### Step 6 — Extend focused tests

- **Files:**
  - `tests/Pegasus.Core.Tests/Cases/CaseDataOperationsTests.cs`
  - `tests/Pegasus.IntegrationTests/AssessmentWorkspaceTestData.cs`
  - `tests/Pegasus.IntegrationTests/InspectionAddressChoicesPersistenceTests.cs`
  - `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs` (serialized with
    CASE-039, CASE-040, CASE-029)
  - `tests/Pegasus.IntegrationTests/CaseTasksWebTests.cs`
  - `tests/Pegasus.IntegrationTests/Browser/InspectionAddressChoiceBrowserTests.cs`
- **Reuse:** existing address/mode invariant tests, the reusable
  `LocalDbTemplateDatabase` harness (the per-class `CaseDataHarness` is private
  to its own test class and is not the seam), `RecordingCaseDetailsStore`,
  `InspectionAddressEditorPostsEveryEditableValueWithTheTypedAddressFirst`, and
  the existing `[Trait("Category", "Browser")]` Playwright pattern.
- `AssessmentWorkspaceTestData.cs` constructs `CaseInspectionData` directly and
  must be updated for the new member.
- Cover storage normalization and persistence; the history query's distinct,
  newest-first, current-case-excluding and sentinel-excluding behaviour; and the
  exact Inspect at option ordering and read-mode source output.
- Assert unavailable choices are disabled with ` · not recorded`, including the
  permanently unavailable repairer option. Do not test a repairer choice as
  selectable: that would contradict D33 and current persisted data.
- Prove storage location survives (a) an Inspection save, (b) an Overview save
  through `_CaseWorkflow.cshtml`, and (c) an unrelated
  `pegasus_case_update_details` MCP update.
- A Browser-category test proves the selection interaction itself — recorded
  choice fills the address, Image Based Assessment shows Provider default,
  Manual entry preserves the typed physical address — because the Web tests
  inspect rendered HTML and cannot prove client behaviour.
- **Acceptance:** tests prove the observable D33 flow and protect against any
  full-save caller silently clearing storage location.

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

**Test UI snapshots.** AGENTS.md requires the regenerated
`docs/design/test-ui/` to be committed **with** the routed Razor change, and CI
verifies it on every change set; deferring the diff to UIIMP-014 would leave
CASE-041's own PR red. CASE-041 therefore takes the capacity-one
`docs/design/test-ui/**` lease for exactly the states its markup moves —
`pages/case-details--default.html`, `pages/case-details--conflict.html`, and
`catalogue.json` only if an existing branch description no longer describes the
section — exactly as CASE-038 does. New per-section catalogue states remain
UIIMP-014's.

## Stop condition

Open the CASE-041 PR against `dev`, write the post-implementation report, and
move the ticket to Review. Do not merge the PR or begin UIIMP-014.

## Wrapper check (Claude, 2026-09-02)

Reused the gpt-5.6-terra (effort high) plan produced earlier this session in
`.worktrees/research` at `897db953` (origin/dev; `git status --porcelain` clean
afterwards). Checked against the research checkout: `CaseDataSnapshotFactory.cs`,
`CaseMutationPageModel.cs`, `_CaseDataHiddenFields.cshtml`,
`InspectionAddressResolution.cs`, the `Text(...)` normaliser in
`CaseDataOperations.cs`, and `CaseDataFieldNames.ClaimantAddress =
"claimant_address"` all exist; the migration head is still `20260829212237`.

Corrections and dependencies the executor must carry:

- `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` and
  `src/Pegasus.Web/Pages/Cases/CaseMutationPageModel.cs` are outside the
  ticket's named owned paths but are required for the storage-location binding.
  See the Constraints above for the full concurrent-overlap list and the
  pre-execution ordering requirement.
- `_CaseDataHiddenFields.cshtml` is under the capacity-one
  `Pages/Cases/Shared/*` lock; the hidden `storageLocation` input is a one-line
  serialized edit under the same rule. So is the matching `_CaseWorkflow.cshtml`
  input.
- The ticket's own verification line "Choosing Repairer fills the address"
  cannot be satisfied in this programme: no repairer address is recorded
  anywhere in production, so under D33 the option is always disabled
  (` · not recorded`). This is now an explicit operator open question rather
  than a wrapper-level reinterpretation. The working observable acceptance is:
  choosing Claimant address, Storage location or a previous address fills the
  address; Repairer location is present and disabled; Manual entry keeps the
  typed input; history lists distinct previous addresses newest first.

## Acceptance conditions

- Inspection edit form shows the Inspect at select in exactly the D33 order;
  options without a recorded value are disabled with the ` · not recorded`
  suffix, nothing else on the section is drawn disabled.
- Choosing a recorded option copies its value into the bound inspection address;
  Manual entry keeps a typed physical address; Image Based Assessment stores the
  exact `Ext18InspectionAddressPolicy.ImageBasedAssessment` value and shows
  Provider default. With JavaScript disabled the address input still works as a
  plain field.
- Read mode shows Inspect at, Source and Provider default; an unmatched value
  reads as Manual entry.
- Storage location is entered in the Inspection edit form, persisted as the
  `storage_location` confirmed field, and survives an Inspection save, an
  Overview save and an Automation MCP details update.
- Same-principal history is distinct, excludes the current Case, blanks and the
  Image Based Assessment value, and is ordered newest first.
- One additive migration with no grant SQL and no permission delta;
  `./scripts/Test-MigrationGrants.ps1` passes.
- Regenerated `docs/design/test-ui/` Case-details snapshots are committed with
  the page change and `-Verify` plus `Test-UiCatalogue.ps1` pass.
- No literal label in the partial beyond dynamic address values; all new
  constants live in `Presentation/OperatorLabels.cs`.

## Plan review (2026-09-03, gpt-5.6-sol xhigh; dispositions Claude Opus)

Verdict as received: REQUEST CHANGES. Every finding was re-verified against the
working checkout before disposition.

| # | Severity | Step | Finding | Disposition |
| --- | --- | --- | --- | --- |
| 1 | blocker | 2, 5, 6 | The plan rewrites the ticket: D33 disables an option only when its value is absent, but the ticket's Verification line "Choosing Repairer fills the address" is declared unsatisfiable and Step 6 refuses to test it. | **Operator question.** Confirmed: the only repairer value in production is `costs.repairer_vat_registered`, and repairer reference data is TICK-034 (backlog, post-alpha, not designated) — no EPIC-012 lane adds a repairer address. A plan cannot amend its own acceptance line, so this is now an unticked question in `open-questions/`. The disabled rendering stays as the recorded working default; adding a repairer address field would be a separate ticket, not CASE-041 scope. |
| 2 | blocker | 5 | Storage preservation is incomplete: `_CaseWorkflow.cshtml` carries its own hidden-field list, and `AssessmentMcpTools.cs` builds the whole `CaseEditableData`. | **Fixed.** Confirmed both (`_CaseWorkflow.cshtml` lines 162-183 duplicate the full list including `inspectionAddress`/`claimantAddress`; `AssessmentMcpTools.cs:439` merges positionally). Step 5 now names three full-save callers and Step 6 adds a regression test for each. |
| 3 | blocker | 5, 6 | No mechanism implements the selection behaviour: the plan forbids JavaScript while requiring select-copy, and HTML-only Web tests cannot prove it. The "mockup `storageAddress` placement" claim is false. | **Fixed.** Confirmed the deployed CSP is `default-src 'self'` with no nonce (site.js header) and that `site.js` has no inspect-at binder. Step 5 now claims the serialized `site.js` lock after CASE-038, reuses its root-scoped idempotent `bind(root)` and the INTK-022 select-handler idiom, states the no-JavaScript fallback, and drops the false mockup-placement claim; Step 6 adds a Browser-category test. |
| 4 | blocker | verification | Handing the snapshot diff to UIIMP-014 contradicts AGENTS.md, which requires `docs/design/test-ui/` to be committed with the routed Razor change; CI would fail CASE-041's own PR. | **Fixed.** Confirmed AGENTS.md lines 168-173 and that CASE-038 takes the same lease for the same reason. The verification section now takes the capacity-one lease for the two Case-details snapshots (and `catalogue.json` only if a description goes stale); new per-section states stay UIIMP-014's. |
| 5 | blocker | 1, 3, 5, 6 | Lane ownership is not disjoint: `Details.cshtml.cs`, `DependencyInjection.cs` and `CaseDetailsWebTests.cs` overlap other wave-3 lanes, and "report the overlap afterwards" is not authorization. | **Fixed.** Confirmed from the board: CASE-039 owns `DependencyInjection.cs`; CASE-039/040/029 all edit `CaseDetailsWebTests.cs`. Also found and added: PLAT-070 edits `CaseDataOperations.cs`, `EfCaseDataStore.cs`, `CaseMutationPageModel.cs`, `_CaseWorkflow.cshtml` and `CaseDataOperationsTests.cs`, so it is now a named base dependency. Step 1 requires a written single-owner ordering before any edit. |
| 6 | should-fix | 1, 5, 6 | The `inspection-address` section key is stale — CASE-038 deletes it without an alias in favour of `inspection`. | **Fixed.** Confirmed in CASE-038's plan (keys "deleted, not aliased"; form renamed `case-inspection-address-form`, `data-edit-save` removed). Constraints, Step 1, Step 5 and the test URLs now use `?section=inspection` and the new form id. |
| 7 | should-fix | decision, 4 | The Case-data-field route does not match the ticket Approach's "one column"; and re-asserting unchanged table grants is ritual that `Test-MigrationGrants.ps1` cannot prove. | **Fixed.** Confirmed the script only asserts grants for migration-created tables. The decision section now reconciles the Approach wording explicitly, and Step 4 drops all grant SQL, recording the verified reason (no table created; existing object grants cover new rows; so no permission delta). |
| 8 | should-fix | 3, 6 | Reuse and file mapping are incomplete: the harness is not a named helper, `AssessmentWorkspaceTestData.cs` is missing, and `CaseDataSnapshotFactory.cs` has no identified change. | **Fixed, with one correction to the finding.** The reusable helper is `LocalDbTemplateDatabase`, not `LocalDbTestDatabase` (no such file exists); the plan names the correct one. `AssessmentWorkspaceTestData.cs:38` does construct `CaseInspectionData` and is added. `CaseDataSnapshotFactory.cs` is removed — verified it runs only at acceptance and has no storage-location source. |

`files.md` was corrected in the same pass: `_CaseWorkflow.cshtml`,
`AssessmentMcpTools.cs`, `site.js`, `AssessmentWorkspaceTestData.cs`, the
Browser test and the two Test UI snapshot rows added; `PegasusDbContext.cs`
(the abandoned column route) and `CaseDataSnapshotFactory.cs` removed.

The reviewer confirmed no new package, no speculative abstraction, no
explanatory copy, no duplicated label list, and no remaining assumption of a
D44 staff-review flag or a D45 damage type.

## Resolutions (2026-09-03) — Repairer location

The operator answered the open question: the repairer location is in general
extractable from the instruction document, and that extraction is filed as
[[INTK-058]]. For CASE-041:

1. **Ship the Repairer location option disabled** with its condition
   (` · not recorded`) under D33, exactly as the plan's working default
   assumed. No repairer field is added by this ticket.
2. **The ticket's Verification line is amended** so that "Choosing Repairer
   fills the address" is not an acceptance condition of this ticket; the
   accepted behaviour is that Repairer location is offered disabled until a
   repairer address exists on the case.
3. **No code change is needed when INTK-058 lands.** The option's
   enabled/disabled state is derived from whether the case carries the value,
   so CASE-041's resolver must read the repairer address through the case
   data contract rather than hard-coding the option as unavailable.
4. CASE-041 is not blocked by INTK-058 and does not wait for it.

## Simplification pass (2026-09-04)

Ran gpt-5.6-sol (effort low) over the working-tree diff against `origin/dev`
(the four lenses: reuse, simplification, efficiency, altitude). Four findings
returned, all in `InspectionAddressChoicesQueries.cs`:

| # | Lens | Finding | Disposition |
| --- | --- | --- | --- |
| 1 | Reuse | `CurrentText` duplicated the existing `CaseField.Current` precedence (`Confirmed ?? Fact ?? Suggestion`) already produced by `EfCaseDataStore.Map`. | **Fixed.** Removed `CurrentText`; claimant address and storage location are now read from the same `projection` (`projection.Claimant.Address.Current?.Value`, `projection.Inspection.StorageLocation?.Current?.Value`) used for the repairer field, instead of a second hand-rolled precedence implementation. |
| 2 | Simplification | Building the whole `CaseDataProjection` solely to read `RepairerAddress` (which is always null — no repairer-address source exists in Core or Infrastructure) was unnecessary ceremony. | **Fixed as a consequence of #1.** The projection is no longer built "solely" for `RepairerAddress` — it now also supplies claimant address and storage location, replacing the duplicated `CurrentText` logic, so building it is no longer ceremony. |
| 3 | Efficiency | The `CaseWorkflows` query needed only to call `EfCaseDataStore.Map` was avoidable if `RepairerAddress` were read directly instead. | **Accepted risk, not applied as originally proposed.** Removing the query would require either duplicating the `Confirmed ?? Fact ?? Suggestion` precedence a second time (reintroducing finding #1) or extracting a new partial-mapper abstraction from `EfCaseDataStore` with only one caller — both rejected under the "no abstraction without a second caller" and "one list per concept" rails. The query is now justified: it backs three projected fields (claimant, storage, repairer), not one always-null one. |
| 4 | Altitude | No finding. | Choice policy stays in Core, persistence lookup in Infrastructure, labels/rendering in Web. |

Re-ran after the fix: `dotnet build ./Pegasus.slnx --configuration Release
--no-restore` (exit 0, 0 warnings), `Pegasus.Core.Tests` (1,231 passed),
`Pegasus.ArchitectureTests` (100 passed), and the same scoped integration
filter (`InspectionAddressChoicesPersistenceTests|CaseDetailsWebTests|CaseTasksWebTests|InspectionAddressChoiceBrowserTests`,
71 passed) — all green, no behaviour change (tests unmodified, all still
pass unchanged).
