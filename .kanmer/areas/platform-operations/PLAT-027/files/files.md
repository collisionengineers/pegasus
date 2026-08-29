# File map — PLAT-027

Lane boundary: `src/Pegasus.Web/Pages/Administration/{Accounts,Roles,Access}/**`,
their web tests, and one nested static class in `OperatorLabels.cs`.

## Changed

| Path | Change |
| --- | --- |
| `src/Pegasus.Web/Pages/Administration/Accounts/Index.cshtml` | Rewritten as the one "Staff accounts & roles" area: `admin-layout` + `_AdminNav`, the accounts table (Username, Role multi-select, State, Last reviewed, Save, Account) and the Create staff account panel. |
| `src/Pegasus.Web/Pages/Administration/Accounts/Index.cshtml.cs` | Reads `IListStaffAccounts` + `IGetAccessReview`; handlers `Create`, `Roles`, `Disable`, `Review` calling `ICreateStaffAccount`, `IAssignStaffRoles`, `IDisableStaffAccount`, `IReviewStaffAccess`. |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | Append-only: new nested `static class StaffAccounts` with the state, role and column words. No existing member reordered. |
| `tests/Pegasus.IntegrationTests/TestUiFocusedRenderTests.cs` | The `/Administration/Accounts` empty-state assertion is retargeted at the new markup (the old sentence was explanatory copy and is deleted). Assertion strengthened, not weakened. |

## Added

| Path | Purpose |
| --- | --- |
| `tests/Pegasus.IntegrationTests/StaffAccountsAndRolesWebTests.cs` | The area's own web tests: the consolidated table renders, every drawn control posts to a real handler, the role select carries the whole Core role set, the access-review readout survives the fold, and the superseded routes are still reachable (UIIMP-009 deletes them, not this ticket). |

## Read, not modified (evidence only)

- `Pages/Administration/Shared/_AdminNav.cshtml` — four other admin lanes share it.
- `Pages/Administration/Roles/Index.cshtml{,.cs}`, `Pages/Administration/Access/Index.cshtml{,.cs}`, `Pages/Administration/Accounts/Edit.cshtml{,.cs}` — superseded, left in place for UIIMP-009.
- `Pages/Shared/_ReasonDialog.cshtml`, `_StatusChip.cshtml`, `_PageHeader.cshtml`, `_ErrorSummary.cshtml`.
- `src/Pegasus.Core/Identity/StaffAccountAdministration.cs`, `StaffAuthorization.cs`, `CaseEngineerEligibility.cs`.
- `src/Pegasus.Web/wwwroot/css/site.css`, `wwwroot/js/site.js` — vocabulary and behaviour only.
- `docs/design/README.md`, `docs/frd/frd-04-parties-accounts-and-access.md`.

## Deliberately not touched

- `docs/design/test-ui/catalogue.json` and `docs/design/test-ui/pages/*` — no page is deleted here, and snapshot regeneration happens once per merge on the merging branch.
- `wwwroot/css/site.css`, `wwwroot/js/site.js`, `Pages/Shared/**`, `Pages/Administration/Shared/**`.
- `Pages/Administration/{Configuration,Mailboxes,MailCategories,Principals,Organizations,Automation}*` — PLAT-025/026/028 and AUTO-006 are in flight.

## Verifier remediation file-map update — 2026-08-29

This section supersedes the earlier statement that
`Pages/Shared/_ReasonDialog.cshtml` was read but not modified.

| Path | Change |
| --- | --- |
| `src/Pegasus.Web/Pages/Shared/_ReasonDialog.cshtml` | Adds a caller-supplied reason maximum while retaining the 500-character default. No local or remote task branch changed this path when checked, so the small shared-file correction uses disposition 2. |
| `tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs` | Deliberately not changed. `task/plat-052-eva-submission-route` and `task/uiimp-005-test-ui-gate` both change it. The accounts page instead restores the matcher's existing empty-state token as a heading. |
