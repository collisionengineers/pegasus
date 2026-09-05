# Post-implementation report — CASE-041 (2026-09-04)

Branch `task/case-041-inspect-at-choices`, worktree
`.worktrees/case-041`, branched from `origin/dev` at `ddbbc5e8c` (already
containing PLAT-070 and CASE-038); merged `origin/dev` once more mid-task
(fast-forward to `90a759184`, docs-only, no conflicts). PR:
https://github.com/collisionengineers/pegasus/pull/664. Head SHA
`d5b1123c88cb015a34e98f3c113498c6b93b83b4`.

## Files changed

- `src/Pegasus.Core/Address/InspectionAddressResolution.cs` — narrow
  `IInspectionAddressChoicesQueries` port, D33 choice ordering, and
  address-to-mode inference for the no-JavaScript path.
- `src/Pegasus.Core/Cases/CaseDataContracts.cs` — nullable
  `StorageLocation`/`RepairerAddress` on `CaseInspectionData`; storage
  location on `CaseEditableData`.
- `src/Pegasus.Core/Cases/CaseDataOperations.cs` — normalize storage
  location with the existing Case text policy.
- `src/Pegasus.Infrastructure/Persistence/CaseDataEntities.cs` — added
  `CaseDataFieldNames.StorageLocation = "storage_location"` to the
  single `All` list.
- `src/Pegasus.Infrastructure/Persistence/EfCaseDataStore.cs` — wired
  storage location through save/read projection and `ApplyEditableData`.
- `src/Pegasus.Infrastructure/Persistence/InspectionAddressChoicesQueries.cs`
  (new) — reads current claimant/storage values and repairer
  availability via the existing `EfCaseDataStore.Map` projection, and
  distinct same-principal confirmed prior inspection addresses
  newest-first, excluding the current case and the Image Based
  Assessment sentinel.
- `src/Pegasus.Infrastructure/DependencyInjection.cs` — registers the
  new focused query port.
- `src/Pegasus.Infrastructure/Persistence/Migrations/20260904183440_CaseInspectionAddressChoices.cs`
  + `.Designer.cs` + `PegasusDbContextModelSnapshot.cs` — one additive
  migration adding `storage_location` to
  `CK_CaseDataFields_FieldName` by the established drop/re-add pattern.
  No table created, no grant SQL, no permission delta.
- `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs`,
  `CaseMutationPageModel.cs` — load choices for the `inspection`
  section; bind/retain `storageLocation`.
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseInspectionAddress.cshtml` —
  renders the ordered D33 select, ` · not recorded` disabled states,
  the storage input, and read-mode Source/Provider default; controls
  are `form="case-edit-form"`-associated, no nested form.
- `src/Pegasus.Web/Mcp/AssessmentMcpTools.cs` — carries storage
  location through the whole-record `pegasus_case_update_details`
  merge.
- `src/Pegasus.Web/wwwroot/js/site.js` — the Inspect-at selection
  binder, mounted through CASE-038's root-scoped idempotent
  `bind(root)`.
- `src/Pegasus.Web/Presentation/OperatorLabels.cs` — new label
  constants in a CASE-041-delimited block.
- `tests/Pegasus.Core.Tests/Cases/CaseDataOperationsTests.cs`,
  `tests/Pegasus.IntegrationTests/AssessmentWorkspaceTestData.cs`,
  `tests/Pegasus.IntegrationTests/InspectionAddressChoicesPersistenceTests.cs`
  (new), `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs`,
  `tests/Pegasus.IntegrationTests/CaseTasksWebTests.cs`,
  `tests/Pegasus.IntegrationTests/Browser/InspectionAddressChoiceBrowserTests.cs`
  (new), `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs`
  (applied-migrations list) — coverage for normalization, persistence,
  history ordering, full-save preservation, and the client selection
  interaction.
- `docs/design/test-ui/pages/case-details--default.html`,
  `--conflict.html` — regenerated (scoped capacity-one lease).

## Commands run and exit codes

```
dotnet restore ./Pegasus.slnx --locked-mode                                   # exit 0
dotnet build ./Pegasus.slnx --configuration Release --no-restore              # exit 0, 0 warnings, 0 errors
dotnet test tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj \
  --configuration Release --no-build                                          # exit 0, 1231 passed
dotnet test tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj \
  --configuration Release --no-build                                          # exit 0, 100 passed
dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj \
  --configuration Release --no-build \
  --filter "FullyQualifiedName~InspectionAddressChoicesPersistenceTests|FullyQualifiedName~CaseDetailsWebTests|FullyQualifiedName~CaseTasksWebTests|FullyQualifiedName~InspectionAddressChoiceBrowserTests" \
  -- xUnit.MaxParallelThreads=2                                               # exit 0, 71 passed (both before and after the simplification fix)
pwsh -NoProfile -File ./scripts/Test-MigrationGrants.ps1                      # exit 0, 92 migration files checked
pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Scope case-details \
  -CaptureFilter "FullyQualifiedName~CaseDetailsWebTests|FullyQualifiedName~CaseTasksWebTests|FullyQualifiedName~TestUiFocusedRenderTests"   # exit 0
pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Scope case-details \
  -CaptureFilter "FullyQualifiedName~CaseDetailsWebTests|FullyQualifiedName~CaseTasksWebTests|FullyQualifiedName~TestUiFocusedRenderTests" \
  -Verify -SkipCapture                                                        # exit 0
pwsh -NoProfile -File ./scripts/Test-UiCatalogue.ps1                          # exit 0, 54 routed sources / 59 prototypes / 0 broken refs
```

## Snapshot artifact facts

- `docs/design/test-ui/pages/case-details--default.html`: 65,562 bytes;
  begins `<!doctype html>`; contains `class="case-sticky"` (1 match) and
  17 `id="section-"` hosts; 0 `<img src="#">`.
- `docs/design/test-ui/pages/case-details--conflict.html`: 40,380
  bytes; begins `<!doctype html>`; contains `class="case-sticky"` (1
  match) and 17 `id="section-"` hosts; 0 `<img src="#">`.

## Deviations from the plan

- **`_CaseDataHiddenFields.cshtml` does not exist on the merged
  CASE-038 base**, and the Case page is unified under one
  `case-edit-form` rather than the Inspection section carrying its own
  form with a separate hidden-field list and a duplicate list in
  `_CaseWorkflow.cshtml`. This is the corrected CASE-038 contract
  (2026-09-04): Inspection's controls are `form="case-edit-form"`-
  associated, no nested form. Storage location is carried by that one
  form's post and by the MCP whole-record merge — there was no second
  hidden-field list to duplicate it into, so no `_CaseWorkflow.cshtml`
  edit was needed. Confirmed the storage location survives an Overview-
  triggered save because it is one shared form, not two.
- No written single-owner ordering document was recorded for
  `Details.cshtml.cs`/`DependencyInjection.cs`/`CaseDetailsWebTests.cs`
  as Step 1 called for: the operator's 2026-09-04 EPIC-012 Build policy
  supersedes that wave-3 exclusive-lock rule for this epic — lanes edit
  any path their plan/files document names concurrently and only the
  merge is ordered by the queue.
- Migration timestamp `20260904183440` reflects the actual generation
  date, not the plan's placeholder `<timestamp>`.

## Simplification pass

Recorded in the ticket plan under "Simplification pass (2026-09-04)":
one finding fixed (removed a duplicate `CaseField.Current` precedence
implementation in `InspectionAddressChoicesQueries.cs` in favour of
reusing the existing `EfCaseDataStore.Map` projection for claimant
address, storage location, and repairer address); one finding accepted
as a reasoned trade-off (the `CaseWorkflows` query needed for `Map` is
now justified across three fields, not one always-null one; avoiding it
would require duplicating precedence logic again or a one-caller
abstraction extraction, both rejected).

## Not done / handed off

- Nothing outstanding for this ticket. The review/verify stages and
  `dev`→`main` release remain for the reviewer and the release process.

## Review round fixes (2026-09-04)

Blocker: `tests/Pegasus.IntegrationTests/Browser/LayoutIntegrityTests.cs:249`
still located the read-mode inspection address by the old label
("Recorded value"), which `_CaseInspectionAddress.cshtml` renamed to
"Inspect at" as part of this ticket, breaking the browser and
test-ui CI jobs.

- Fixed. Repointed the xpath locator at `Inspect at` without weakening
  the assertion — it still asserts the saved `inspectionAddress`
  appears (via `Assert.Contains`, `StringComparison.Ordinal`) in the
  read-mode `<dd>` value.
- Removed the now-dead
  `OperatorLabels.CaseWorkspace.RecordedInspectionAddress` constant in
  the same change (no other reference to it existed outside build
  artifacts).
- Findings 2–6 from the review: no change on this branch, per their
  existing dispositions in the review record.

Commands run and exit codes:

```
dotnet restore ./Pegasus.slnx --locked-mode                                   # exit 0
dotnet build ./Pegasus.slnx --configuration Release --no-restore              # exit 0, 0 warnings, 0 errors
dotnet test tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj \
  --configuration Release --no-build                                          # exit 0, 1240 passed
dotnet test tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj \
  --configuration Release --no-build                                          # exit 0, 100 passed
dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj \
  --configuration Release --no-build \
  --filter "FullyQualifiedName~LayoutIntegrityTests" \
  -- xUnit.MaxParallelThreads=2                                               # exit 0, 70 passed
```

No routed Razor page, partial, or `catalogue.json` changed by this fix
(only `OperatorLabels.cs` and the test locator), so Test UI snapshots
were not regenerated.

Head SHA after this fix: `42b38752a`. Pushed to
`task/case-041-inspect-at-choices`; PR #664 unchanged (same branch).

## Record correction at the final head (reviewer, 2026-09-05)

The "Files changed", "Commands run" and "Snapshot artifact facts" sections
above were written at head `d5b1123c…`, before the `origin/dev` merge
(`7f035307`) regenerated the migration and the snapshots and before the
review-round fix (`42b38752a`). Measured in the review worktree at the final
head `42b38752a6ab38c4efe745cba87cc757a118ad7b`, the accurate figures are:

- Migration: `20260904233144_CaseInspectionAddressChoices` (not
  `20260904183440`). `Test-MigrationGrants.ps1` reports **93** migration files
  checked (not 92).
- `docs/design/test-ui/pages/case-details--default.html`: **67,734 bytes**;
  begins `<!DOCTYPE html>`; one `class="case-sticky"`; **16 distinct
  `id="section-…"` ids** — the eleven section hosts (overview,
  engineer-notes, inspection, vehicle, damage, valuation, estimate,
  settlement, report, files, notes) plus five `-title` ids; zero
  `<img src="#">`.
- `docs/design/test-ui/pages/case-details--conflict.html`: **40,971 bytes**;
  same doctype, one `case-sticky`, the same 16 ids, zero `<img src="#">`.

`tests/Pegasus.IntegrationTests/Browser/LayoutIntegrityTests.cs` and the
removal of `OperatorLabels.CaseWorkspace.RecordedInspectionAddress` belong in
the "Files changed" list; they are recorded in the "Review round fixes"
section above.
