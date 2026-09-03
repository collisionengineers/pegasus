# Research — CASE-041 (2026-09-02, gpt-5.6-terra medium, wrapper-checked)

## Wrapper check (Claude, 2026-09-02)

Codex ran read-only in `.worktrees/research` at `cad00be9` (origin/dev);
`git status --porcelain` was clean afterwards. Spot-checked in the main
checkout and confirmed: `ICaseDataQueries` has twelve consumers across
Core/Infrastructure/Web/tests (`grep -rl ICaseDataQueries src tests`);
`CaseTasksWebTests.InspectionAddressEditorPostsEveryEditableValueWithTheTypedAddressFirst`
exists (line 117); `20260729199000_RuntimeRoleReconciliation` grants
`Cases` `SELECT, INSERT, UPDATE` (web) and `SELECT, UPDATE` (worker) and
`20260814092852_AddWorkerCaseCreationGrants.cs` exists;
`RecordingCaseDetailsStore` and `RetainableFormFields` exist;
`CaseDataFieldEntity.ConfirmedAtUtc` exists; the resolution store writes
`AddressHistoryValue` into `ActionHistory`.

Corrections and additions:

- The comment "SaveCase writes every editable value" was not found verbatim
  in `Details.cshtml.cs`; the behaviour is confirmed by the partial's own
  comment ("The save overwrites every editable value") and by
  `_CaseDataHiddenFields.cshtml`. The risk stands.
- Codex's "storage location should be a `Cases` column, not a Case-data
  field" is a design recommendation, not a verified fact. VERIFIED
  alternative: every other editable Case value (including
  `claimant_address`) is a typed field in `CaseDataFields` guarded by
  `CK_CaseDataFields_FieldName`, and
  `20260828185508_ProviderDeclaredInstruction` shows the pattern for adding a
  name to that constraint (drop and re-add). A `storage_location` field keeps
  provenance (`CaseField<string>` with source) and one editable-data
  vocabulary; a column does not. Either route needs exactly one migration;
  the plan decides and records why. `Cases` and `CaseDataFields` already
  carry object-level grants for both runtime roles, so the migration adds
  no new grant but must not reduce them.
- VERIFIED (`grep -rni repairer src --include=*.cs --include=*.cshtml`):
  the only repairer concept in production is the Assessment flag
  `costs.repairer_vat_registered`. No repairer name or address is recorded
  anywhere, and no EPIC-012 ticket adds one, so the "Repairer location"
  option renders disabled (" · not recorded") in every current state under
  D33. The plan must say so and must not infer a repairer from estimates or
  reference data.
- VERIFIED: the partial today has no "Previous values" select even though
  EPIC-011 §1.8 lists one; CASE-027 shipped Recorded value, Provider
  default and Inspection mode only.
- Governing doc: `docs/frd/frd-06-vehicle-and-engineering-evidence.md`
  (§Inspection address) linked as `refs` by this research; D33 lands in the
  governing docs through DELIV-041 (in review), so `docs_todo` stays true
  until that merges.

## Scope and evidence status

- **VERIFIED** — this checkout is at `cad00be9` and is a Git worktree:
  `git rev-parse --is-inside-work-tree; git log -1 --oneline`.
- **VERIFIED** — CASE-027 introduced the current Case workspace partial and
  later fixed its full-save binding:
  `git log --oneline --all --grep=CASE-027` and
  `git log --oneline -- src/Pegasus.Web/Pages/Cases/Shared/_CaseInspectionAddress.cshtml`.
- **VERIFIED** — available SDKs are 10.0.204 and 10.0.303:
  `dotnet --list-sdks`.
- **ASSUMED** — D29, D30, D33, D43, ticket ownership, and the EPIC-012 lane
  constraints are operator-provided governing context for this research.

## Current behaviour

### Core ports and policy

- **VERIFIED** — `InspectionAddressResolution.cs` owns the intake-resolution
  contract, `IInspectionAddressResolutionStore`, resolution states, and the
  rule that image-based principals satisfy case creation without a settled
  physical address:
  `rg -n -C 3 'IInspectionAddressResolutionStore|SatisfiesCaseCreation' src/Pegasus.Core/Address/InspectionAddressResolution.cs`.
- **VERIFIED** — `Ext18InspectionAddressPolicy.ImageBasedAssessment` is the
  exact stored value, and the policy distinguishes it from physical addresses:
  `rg -n -C 3 'ImageBasedAssessment|PhysicalAddress' src/Pegasus.Core/Address/Ext18InspectionAddressPolicy.cs`.
- **VERIFIED** — `CaseEditableData` currently carries claimant address,
  inspection address, and inspection mode, but no storage location;
  `CaseInspectionData` exposes date, deadline, address, and mode:
  `rg -n -C 3 'CaseClaimantData|CaseInspectionData|CaseEditableData' src/Pegasus.Core/Cases/CaseDataContracts.cs`.
- **VERIFIED** — `CaseDataPolicy.Normalize` normalizes claimant and inspection
  addresses, requires inspection address and mode together, and enforces the
  exact Image Based Assessment value:
  `rg -n -C 3 'ClaimantAddress|InspectionAddress|ValidateInspection' src/Pegasus.Core/Cases/CaseDataOperations.cs`.
- **VERIFIED** — the existing generic read port is
  `ICaseDataQueries.GetAsync`; it is used by several non-Case-page consumers,
  so adding history to it would widen a shared contract:
  `rg -l 'ICaseDataQueries' src tests -g '!**/bin/**' -g '!**/obj/**'`.

### Infrastructure adapters and persistence

- **VERIFIED** — `InspectionAddressResolutionStore` persists intake
  resolution and writes an `ActionHistory` record containing the previous
  receipt-draft address; it is not a per-principal Case history query:
  `rg -n -C 3 'previousValue|AddressHistoryValue|ActionHistory' src/Pegasus.Infrastructure/Persistence/InspectionAddressResolutionStore.cs`.
- **VERIFIED** — `EfCaseDataStore` already loads the Case and Principal with
  the snapshot, saves confirmed claimant and inspection values, and maps the
  inspection address/mode into `CaseDataProjection`:
  `rg -n -C 3 'SnapshotQuery|ClaimantAddress|InspectionAddress|InspectionMode'
  src/Pegasus.Infrastructure/Persistence/EfCaseDataStore.cs`.
- **VERIFIED** — the principal code is available through
  `CaseEntity.Principal`, allowing a same-principal query without a new
  principal-history table:
  `rg -n -C 3 'class CaseEntity|PrincipalId|Principal' src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs`.
- **VERIFIED** — `CaseEntity` has no storage-location property, and the EF
  model has no storage-location mapping:
  `rg -n -i 'storage.{0,20}(location|address)|storageaddress'
  src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs`.
- **VERIFIED** — claimant address is a typed Case-data field; no repairer
  address field exists in Core, Infrastructure, or Web:
  `rg -n -i -C 2 'repairer.*address|address.*repairer'
  src/Pegasus.Core src/Pegasus.Infrastructure src/Pegasus.Web
  -g '!**/bin/**' -g '!**/obj/**'`.
- **VERIFIED** — the current `CaseDataFields` allowed-name constraint does not
  contain storage location (see the wrapper check above for the two
  persistence routes this leaves open):
  `rg -n -C 2 'CK_CaseDataFields_FieldName'
  src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs`.
- **VERIFIED** — Infrastructure registers `EfCaseDataStore` as both
  `ICaseDataStore` and `ICaseDataQueries`:
  `rg -n -C 3 'EfCaseDataStore|ICaseDataQueries'
  src/Pegasus.Infrastructure/DependencyInjection.cs`.

### Web pages, partials, and labels

- **VERIFIED** — the existing partial is a read panel for Recorded value,
  Provider default, and Inspection mode; edit mode contains only a free-text
  `inspectionAddress` input:
  `rg -n -C 4 'RecordedInspectionAddress|ProviderDefaultInspectionAddress|inspection-address'
  src/Pegasus.Web/Pages/Cases/Shared/_CaseInspectionAddress.cshtml`.
- **VERIFIED** — `DetailsModel.OnPostSaveAsync` binds every editable value and
  forwards them to `SaveCase`; omitted values are cleared, which is why the
  partial posts `_CaseDataHiddenFields`:
  `rg -n -C 4 'OnPostSaveAsync|inspectionAddress|claimantAddress'
  src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` and
  `rg -n -C 3 'CaseDataHiddenFields' src/Pegasus.Web/Pages/Cases/Shared/_CaseInspectionAddress.cshtml`.
- **VERIFIED** — `DetailsModel` can receive a focused address-choice query
  port and load it only for the inspection section; it already controls the
  routed `inspection-address` section:
  `rg -n -C 3 'DetailsModel\(|inspection-address|OnGetAsync'
  src/Pegasus.Web/Pages/Cases/Details.cshtml.cs`.
- **VERIFIED** — label ownership is centralized in
  `OperatorLabels.CaseWorkspace`; the reusable constants are
  `InspectionAddressPanel`, `RecordedInspectionAddress`, and
  `ProviderDefaultInspectionAddress`:
  `rg -n -C 3 'InspectionAddressPanel|RecordedInspectionAddress|ProviderDefaultInspectionAddress'
  src/Pegasus.Web/Presentation/OperatorLabels.cs`.
- **VERIFIED** — `OperatorLabels.InspectionMode(ImageBasedAssessment)` already
  renders "Image Based Assessment":
  `rg -n -C 2 'InspectionMode\(' src/Pegasus.Web/Presentation/OperatorLabels.cs`.
- **ASSUMED** — new constants should be named
  `InspectAt`, `InspectionAddress`, `InspectionSource`, `ClaimantAddress`,
  `RepairerLocation`, `StorageLocation`, `PreviousAddress`, `ManualEntry`,
  and `NotRecorded`; this is a proposed naming set, not an existing API.
- **ASSUMED** — `OperatorLabels.cs` is a capacity-one shared lock. CASE-038
  owns it for its wave, so CASE-041 must wait for that lock or coordinate one
  serialized edit; it must not duplicate labels in the partial.

### Tests, migrations, and EVA

- **VERIFIED** — `CaseDataOperationsTests` proves the address/mode pairing and
  Image Based Assessment invariants:
  `rg -n -C 3 'NormalizeRequiresInspectionAddress|ImageBasedAssessment'
  tests/Pegasus.Core.Tests/Cases/CaseDataOperationsTests.cs`.
- **VERIFIED** — `CaseTasksWebTests` proves the CASE-027 partial posts the
  visible inspection input before all hidden values:
  `rg -n -C 3 'InspectionAddressEditorPostsEveryEditableValue'
  tests/Pegasus.IntegrationTests/CaseTasksWebTests.cs`.
- **VERIFIED** — `CaseDetailsWebTests` owns the Details-page replacement
  store and is the focused Web test seam for a new query port:
  `rg -n -C 3 'RecordingCaseDetailsStore|ICaseDataQueries'
  tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs`.
- **VERIFIED** — the latest migration has an SQL Server guard and validates
  both fixed runtime roles before grants:
  `rg -n -C 3 'RequireRuntimeRoles|GRANT|IsSqlServer'
  src/Pegasus.Infrastructure/Persistence/Migrations/20260829212237_GrantProviderSubmissionAcceptRecovery.cs`.
- **VERIFIED** — both runtime roles already have object-level permissions on
  `Cases`; a new column inherits those permissions, but CASE-041's migration
  should retain the role guard and explicitly reassert the required Case
  grants as required by the ticket:
  `rg -n -C 2 '\("Cases"' src/Pegasus.Infrastructure/Persistence/Migrations/20260729199000_RuntimeRoleReconciliation.cs`
  and
  `src/Pegasus.Infrastructure/Persistence/Migrations/20260814092852_AddWorkerCaseCreationGrants.cs`.
- **VERIFIED** — `Cases` is already in the runtime-role bootstrap census, so
  that migration need not change:
  `rg -n -C 2 '"Cases"' src/Pegasus.Infrastructure/Persistence/Migrations/20260729199000_RuntimeRoleReconciliation.cs`.
- **VERIFIED** — EVA maps the stored inspection address, including its
  Image-Based export conversion, and does not depend on the surrounding
  option/source shape:
  `rg -n -C 3 'Inspection Address|ImageBasedAssessment'
  src/Pegasus.Core/Eva/CaseEvaMapping.cs`.

## Mockup behaviour

- **VERIFIED** — `inspectAtOptions` orders Image Based Assessment, claimant,
  repairer, storage, principal history, and Manual entry. Repairer is empty
  without an address and Manual uses `__manual__`:
  `rg -n -C 12 'function inspectAtOptions'
  C:/Users/PC/Downloads/Pegasus_UI_v2_src/src/05-state.js`.
- **VERIFIED** — edit mode disables empty options and adds " · not recorded";
  selection copies a value, while Manual clears only Image Based Assessment:
  `rg -n -C 15 "SECTIONS.inspection|CHANGES\['inspect-mode'\]"
  C:/Users/PC/Downloads/Pegasus_UI_v2_src/src/21-case-sections.js`.
- **VERIFIED** — read mode shows Inspect at, Source, and Provider default:
  the same `SECTIONS.inspection` command above.
- **VERIFIED** — the notes call for distinct newest-first principal history,
  no new history table, and a Case storage-location field:
  `rg -n -C 5 -i 'Inspect at|address history|storage-location'
  C:/Users/PC/Downloads/Pegasus_UI_v2_notes.md`.

## Gap list

1. **VERIFIED** — no focused Core port or Infrastructure adapter returns the
   current Case's claimant address, storage location, repairer location, and
   distinct same-principal inspection-address history:
   `rg -n -i -C 2 'previous.*address|address.*history|repairer.*address|storage.*location'
   src/Pegasus.Core src/Pegasus.Infrastructure src/Pegasus.Web
   -g '!**/bin/**' -g '!**/obj/**'`.
2. **VERIFIED** — storage location has neither a Case property, a Case-data
   field name, nor a database column.
3. **VERIFIED** — repairer location has no persisted source today, so its
   option must be disabled rather than inferred or fabricated.
4. **VERIFIED** — the partial has neither a select nor source display.
5. **VERIFIED** — current labels do not contain the required option/source
   vocabulary except Image Based Assessment.
6. **VERIFIED** — current Test UI snapshots include Case-details states, but
   do not own the CASE-041 implementation:
   `rg --files docs/design/test-ui | rg -i 'case|inspection'`.

## Reuse and implementation direction

- **VERIFIED** — reuse `CaseDataPolicy.Normalize` and `SaveCase` rather than
  a special address-write command.
- **VERIFIED** — reuse `EfCaseDataStore.SnapshotQuery`'s Case/Principal join
  pattern for the same-principal history query.
- **VERIFIED** — reuse `CaseDataFieldNames.InspectionAddress` and confirmed
  field values to select prior addresses; order by the confirmed timestamp
  descending, exclude the current Case, remove blanks, exclude the exact
  `Image Based Assessment` value, and de-duplicate. The source field and
  timestamps are present:
  `rg -n -C 3 'ConfirmedAtUtc|InspectionAddress'
  src/Pegasus.Infrastructure/Persistence/CaseDataEntities.cs
  src/Pegasus.Infrastructure/Persistence/EfCaseDataStore.cs`.
- **ASSUMED** — introduce a narrow
  `IInspectionAddressChoicesQueries` Core port rather than extending
  `ICaseDataQueries`; this prevents unrelated EVA, AI, MCP, and test doubles
  from acquiring a UI-only history method.
- **VERIFIED** — reuse `_CaseDataHiddenFields.cshtml` and the existing
  Details save handler so the section continues to post the complete
  `CaseEditableData` shape.
- **VERIFIED** — reuse the role-guard/grant style from
  `GrantProviderSubmissionAcceptRecovery` and the generated EF migration,
  designer, and model snapshot convention.

## Risks

- **VERIFIED** — `SaveCase` overwrites all editable fields; failure to add
  the storage location to both binding and hidden fields would silently
  clear it on an Overview save (partial comment "The save overwrites every
  editable value"; `OnPostSaveAsync` in `Details.cshtml.cs`).
- **VERIFIED** — migrations are a shared serialized path, and CASE-039 and
  PLAT-068 also require migrations. CASE-041 must refresh from `origin/dev`
  and generate its one migration only after the preceding serialized
  migration lands. This scheduling constraint is operator-provided; the
  current migration head (`20260829212237`) is verified with:
  `Get-ChildItem src/Pegasus.Infrastructure/Persistence/Migrations -Filter '*.cs'`.
- **VERIFIED** — CASE-038 owns the current frame and `OperatorLabels.cs`;
  CASE-041 must not touch `Details.cshtml`, site CSS, or site JS.
- **VERIFIED** — existing role permissions are object-level, not
  column-level; a migration must not accidentally reduce the established Web
  or Worker Case access.
- **VERIFIED** — no repairer address can be inferred from estimates or
  provider-reference data without violating the explicit-address boundary:
  `rg -n -C 3 'physical vehicle/repairer location|never select an address'
  docs/frd/frd-06-vehicle-and-engineering-evidence.md`.

## Open questions for the operator

none
