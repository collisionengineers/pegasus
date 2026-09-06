# B06 phase 2 — implementation record

Integrated into `task/pegasus-v1-casework` as `cc717989f` (squash of helper branch `b-work/b06p2`: e325d8d71, e8ea0e99f, a8775675d, 4d2c05b71; base `8795c1581`). Opus implementer under claude-fable-b orchestration, 2026-09-06.

## Delivered

- `Details.cshtml.cs`: `ICaseAssetPreparationQueries`/`ICaseAssetPreparationStore` injected; `AssetPreparations` and `PreparedReportImages` (`CaseAssetPreparationPolicy.ForReport`) loaded on every full GET (the Report section is never deferred) and on the `files` fragment. Handlers `OnPostSaveAssetPreparationAsync(id, expectedVersion, operationKey, editLeaseToken, AssetPreparationEditForm[] edits)` and `OnPostResetAssetPreparationAsync(…, Guid[] occurrenceIds)` under the section-command guard (`CanOpen`, Engineer only, operation key, lease), fixed server-side reasons, PRG to `?section=files`; `Order` forwarded only for Supporting. The report, valuation and preparation guards share one `GuardSectionCommandAsync` (behaviour preserved).
- `_CaseFiles.cshtml`: "Report images" panel (rendered only when preparations exist) with role, order, rotation, crop as values; in edit mode script-off forms per action — Save (role/order/crop), Rotate left/right, Move up/down (both neighbours' orders exchanged in one submit), Reset. `_CaseReport.cshtml`: prepared cards in report order from the same loaded set, Not used omitted. Labels in `CaseWorkspaceLabels.ReportImages` (role/rotation/crop value formatters included). `case-workspace.css`: three rules.
- Tests: new B partial `CaseAssetPreparationWebTests.cs` (fake implements the two preparation ports and `ICaseEvidenceImageQueries`): read-only values without controls; edit-mode controls and the exact bound save; Move up exchanges orders; Reset; refusal reported on Files with the lease kept; Report cards in order without the unused image. `CaseValuationWebTests.EnterEngineerEditModeAsync` takes the port-substitution callback like the staff harness.

## Verification (Windows, PowerShell 7, Release)

| Where | Check | Result |
| --- | --- | --- |
| helper worktree | solution build | 0 / 0 |
| helper | Core `~CaseAssetPreparation` | 30 passed |
| helper | integration `~CaseAssetPreparationPersistenceTests` | 14 passed |
| helper | Architecture; `Test-UiCatalogue.ps1` | 100 passed; valid |
| squashed tree cc717989f | build; full Core; Architecture; preparation persistence | 0/0; 1477/1477; 100/100; 14/14 |
| combined tree (A 9028aa12b + B cc717989f) | `CaseDetailsWebTests` family + `CaseCustodyWebTests` + `ImageViewingWebTests` + `CaseEngineerSectionsWebTests` | 103 / 103 |

The helper proved its six web tests locally with a temporary, reverted stub of the four unregistered ports (`IGetCaseDocumentMetadata`, `IReadLogicalDocumentVersion`, `ICaseArtifactCustody`, `ICaseArtifactCustodyStatus`); nothing of that is committed. The orchestrator's combined run is the retained evidence.

## Deviations and follow-ups

1. Controls live in the Files section; the Report section shows read-only cards. `docs/design/README.md` places the `report-image` preparation with cropper in the Report section, and plan B08 expects the Files viewer to expose the same preparation. Follow-up (B08): render the same forms on the Report cards from the one partial, so both sections offer the controls; no policy change.
2. No drag-and-drop or crop canvas; keyboard-usable server-rendered forms only. B08 "keyboard/drag controls" — drag is a progressive enhancement for a later slice in `case-workspace.js`.
3. `wwwroot/css/case-workspace.css` is not linked by `_Layout.cshtml` (C-owned); the new rules (and B03's `.valuation-card`/`.suggestion-chip`) are inert until C adds the link. Requested on PR 672.
4. Each successful preparation command ends edit mode (`ClearLeaseState`) like every sibling handler; repeated rotate/move presses re-enter edit mode. Product decision to record for B08.
5. `RetainableFormFields` unchanged: these handlers do not run `ExecuteCommandAsync`, and their fields are identifiers, versions and numeric choices.

## Simplification pass (2026-09-06)

Applied in the helper's `4d2c05b71` (label alias, form type name) and in the guard de-duplication. Nothing outstanding.
