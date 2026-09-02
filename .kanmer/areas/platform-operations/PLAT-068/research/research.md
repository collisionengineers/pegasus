# Research — PLAT-068 (2026-09-02, gpt-5.6-terra high, wrapper-checked)

Research was read-only at `cad00be9` (`origin/dev`) in the detached
`.worktrees/research` checkout; `git status --porcelain` was empty before and
after. Board state (ticket body, D29–D43, EPIC-011 context and waves) was
supplied verbatim by the Claude wrapper; Codex has no Kanmer access.

## Premise ledger

| Premise | Status and evidence |
| --- | --- |
| Staff accounts, ports, role changes, reasons, and idempotency exist. | **VERIFIED** — `Get-Content src/Pegasus.Core/Identity/StaffAccountAdministration.cs`; `rg -n -C 8 'ActionHistory\|EventKind\|AssignAsync' src/Pegasus.Infrastructure/Persistence/EfStaffAccountAdministration.cs`. |
| Accounts page is the PLAT-027 consolidated surface. | **VERIFIED** — `rg --files src/Pegasus.Web/Pages/Administration/Accounts`; `Get-Content src/Pegasus.Web/Pages/Administration/Accounts/Index.cshtml`. |
| Account changes are permanently recorded. | **VERIFIED** — `rg -n -C 4 'ActionHistoryEntity\|AggregateType\|EventKind' src/Pegasus.Infrastructure/Persistence/{PegasusDbContext,EfIdentityAuditStore}.cs`. |
| Brand signatures exist. | **VERIFIED** — `Get-Item docs/design/brand/signatures/*.png`: Andy 3,972 bytes, Ed 80,989 bytes, Neil 30,418 bytes. |
| Renderer currently reads a fixed engineer tuple. | **VERIFIED** — `rg -n -C 10 'TryResolveAcceptedEngineer\|ReportEngineer' src/Pegasus.Core/Reports/AssessmentReportProjection.cs`. |
| Case documents use `IDocumentContentStore`, not a staff-image store. | **VERIFIED** — `rg -n -i -C 3 'IDocumentContentStore\|LocalDocumentContentStore\|BoxDocumentContentStore' src/Pegasus.Infrastructure`. |
| Migration/grant conventions and current tail migration are known. | **VERIFIED** — `Get-Content scripts/Test-MigrationGrants.ps1`; `Get-ChildItem .../Migrations | Sort-Object Name`; `pwsh -File scripts/Test-MigrationGrants.ps1`. |
| Accounts Test UI states are known. | **VERIFIED** — `rg -n -C 4 'administration-accounts\|account-confirm' docs/design/test-ui/catalogue.json`. |
| Existing account tests are known. | **VERIFIED** — `rg -n 'public (async )?Task' tests/Pegasus.{Core,Integration}Tests/...`. |
| Engineer is an enum role and case eligibility checks enabled status plus role. | **VERIFIED** — `Get-Content src/Pegasus.Core/Identity/{IdentityContracts,CaseEngineerEligibility}.cs`; `Get-Content src/Pegasus.Infrastructure/Persistence/EfCaseEngineerEligibility.cs`. |
| A Patterson, Ed Mawdsley, and Neil O'Reilly are not seeded staff accounts. | **VERIFIED** — `rg -n -i 'a\.patterson\|mawdsley\|n\.oreilly' src tests`; results are report assets/tests, not identity bootstrap accounts. |
| An administrator password-reset command/dialog already exists. | **VERIFIED false** — `rg -n -i 'Reset.*Password\|PasswordReset\|ResetStaff' src tests --glob '*.cs'`; only development bootstrap resets exist. The `temporaryPassword` parameter in `Accounts/Index.cshtml.cs` belongs to `OnPostCreateAsync`, not a reset. |

## Current behaviour

**VERIFIED** — `StaffAccountSummary` contains only ID, username, enabled state,
must-change-password state, roles, and last access review. The Core commands
are `CreateStaffAccount`, `DisableStaffAccount`, `AssignStaffRoles`, and
`ReviewStaffAccess`. `StaffAccountAdministrationPolicy` requires an
Administrator, reason, and operation key for mutations.

`EfStaffAccountAdministration` runs serializable transactions and records
`ActionHistory` aggregate type `staff_account`. Existing event kinds are
`staff_account_created`, `staff_account_disabled`, `staff_roles_changed`, and
`access_reviewed`; snapshots are in `BeforeJson`/`AfterJson`.

**VERIFIED** — the Accounts index is an Administrator-only table with inline
role checkboxes and a reasoned `OnPostRolesAsync` save. Disable and review use
the shared `_ReasonDialog`. There is no current `account-settings` dialog or
save-disabled-until-changed client behaviour. The old `Edit` route exists but
is only exercised by Test UI focused rendering
(`tests/Pegasus.IntegrationTests/TestUiFocusedRenderTests.cs`); the ticket's
"extend the PLAT-027 account settings dialog" therefore means introducing the
per-account settings surface, not editing an existing dialog.

**VERIFIED** — `OperatorLabels.StaffAccounts` currently provides enabled,
disabled, password, review, disable, reason, confirm, and disable-consequence
labels. It has no sign-off labels.

**VERIFIED** — the renderer receives `ReportEngineer(Name, Qualifications,
SignatureKey)` in `AssessmentReportSnapshot`. It reads the three values from
assessment text fields (`engineer.name`, `engineer.qualifications`,
`engineer.signature` in `AssessmentVocabulary`) and
`AssessmentReportRendering.AcceptedEngineers` rejects any tuple except
`A Patterson | M.Inst.IAEA | andy_patterson`. Only Andy is embedded in
`Pegasus.Infrastructure.csproj`; all three source PNGs are governed under
`docs/design/brand/signatures/`.

**VERIFIED** — current account storage is `PegasusIdentityUser : IdentityUser<Guid>`
in `AspNetUsers` (`PegasusDbContext.cs`), with `IsEnabled` and
`MustChangePassword`. The Web runtime already has `SELECT, INSERT, UPDATE` and
the Worker has `SELECT, INSERT, UPDATE, DELETE` on that table through
`20260729199000_RuntimeRoleReconciliation`. Adding columns needs no new table
grant; `Test-MigrationGrants.ps1` only enforces grants for created tables.

**VERIFIED** — current Test UI catalogue states are Accounts default/empty,
Account Edit default, and Account Confirm disable/review. The snapshot flow is
`Update-TestUiSnapshots.ps1`, then `-Verify`, then `Test-UiCatalogue.ps1`.

## Mockup

**VERIFIED** — `17-admin.js` shows a Staff accounts table with a Sign-off
Engineer column (`—` for non-Engineers; `Yes` green, `Yes · qualifications
missing` amber, `Signature missing` amber, `No` neutral). The
`account-settings` dialog shows role, Sign-off Engineer Yes/No (Engineer role
only), Qualifications, Signature image On file/Not on file, Upload/Replace
signature, a reason, and Save. Its mock action log records role changes but
not sign-off changes.

**VERIFIED** — `05-state.js` `signoffEngineers()` selects only enabled
Engineer accounts having both `signs` and a signature; `defaultSignoff()` is
the assigned engineer when eligible, otherwise username `a.patterson`, then
the first eligible signer.

**VERIFIED** — fixtures define A Patterson (`M.Inst.IAEA`), Ed Mawdsley
(`ATA VDA AQP`), and Neil O'Reilly (blank qualifications) as Engineers with a
signature on file.

## Gaps and seam

- **VERIFIED:** no staff sign-off flag, qualifications, signature bytes, or
  signature content type exists in Core, EF, migration, or Accounts UI.
- **VERIFIED:** no account settings dialog exists in the Razor implementation;
  the mockup's dialog must be introduced within the Accounts lane without
  changing shared partials.
- **VERIFIED:** the current renderer's fixed dictionary and embedded Andy asset
  are DOCS-017 work, not PLAT-068 work.
- **VERIFIED:** CASE-040 owns the case Sign-off Engineer field, its default
  rule, ribbon, and EVA dialog. PLAT-068 must expose account-backed,
  Administrator-maintained sign-off data through the existing staff-account
  Core boundary; it must not implement case selection or defaulting.
- **ASSUMED:** a compact `SignOffEngineerProfile` value exposed by
  `IStaffAccountQueries` is the smallest seam for CASE-040 and DOCS-017. It
  should contain account identity, display name, qualifications, signature
  bytes, and content type; CASE-040 supplies the selected account ID and
  DOCS-017 constructs the renderer tuple.
- **ASSUMED:** store one signature directly on `AspNetUsers` as nullable
  image bytes plus content type and nullable qualifications. It is a single
  account attribute, unlike retained case evidence, and needs no new document,
  custody, blob, or runtime unit.
- **ASSUMED:** Core must prohibit making a non-Engineer account sign-off
  eligible. Recommended plan default (follows `CaseEngineerEligibility`,
  which derives eligibility from enabled + role at read time): sign-off data
  is retained on the account, and eligibility is derived as enabled +
  Engineer role + flag, so removing the Engineer role makes the account
  ineligible without a destructive clear or a rejected role change.

## Reuse

- `StaffAccountAdministrationPolicy.Normalize` for Administrator, reason, and
  operation-key validation.
- `EfStaffAccountAdministration` for serializable mutation, replay detection,
  before/after history, and the `staff_account` action vocabulary.
- `EfStaffAccountQueries.Summary` and `IStaffAccountQueries` for the account
  read model.
- `IndexModel.RunAsync`, `Validate`, and `NewOperationKey` for account posts.
- `OperatorLabels.StaffAccounts` for all new operator strings.
- `Test-MigrationGrants.ps1` and `20260829095336_CaseValuations.cs` for
  migration style.
- Existing `docs/design/brand/signatures/*.png` as the approved source assets;
  do not copy them into Web decoration.

## Risks

- The governing decision says "flagged accounts" are selectable, while the
  mock additionally requires a stored signature. Eligibility must be one Core
  rule, not duplicated in page, Case, and renderer code.
- `new StaffAccountSummary(` appears in four Core test files
  (`ActorDisplayNamesTests`, `RetainedMailTests`, `EngineerActivityReportTests`,
  `GetTriageDisplayNameTests`); adding positional fields to the record changes
  every fixture construction.
- The uploaded image has no current staff-account MIME, byte-limit, or image
  dimension policy. Do not infer the case-document limits.
- The D28 administrator reset (PLAT-064) is not implemented. Reuse only the
  established Administrator/reason/action-history shape; do not claim an
  existing reset handler or fold PLAT-064 into this ticket.
- The next migration must serialize after
  `20260829212237_GrantProviderSubmissionAcceptRecovery`; the
  `Persistence/Migrations/**` lock has capacity one.

## Wrapper spot-checks (Claude, main checkout at the same SHA)

Confirmed by independent grep: no `OnPost*Reset`/`ResetPassword` handler in
Web or Core; `AspNetUsers` grant tuples at lines 106 and 218 of
`20260729199000_RuntimeRoleReconciliation.cs`; the five `staff_account*` /
`access_reviewed` event-kind strings in `EfStaffAccountAdministration.cs`;
`Pages/Shared/_ReasonDialog.cshtml`; `RunAsync`/`Validate`/`NewOperationKey`
in `Accounts/Index.cshtml.cs`; `EfCaseEngineerEligibility.cs`;
`PegasusIdentityUser` fields; the embedded Andy resource at
`Pegasus.Infrastructure.csproj` lines 53–54; `AcceptedEngineers` at
`AssessmentReportRendering.cs` line 160. No claim was dropped.

## Operator questions

- Are the three PNGs under `docs/design/brand/signatures/` loaded by an
  Administrator through the new upload control (no migration seed), or must
  the migration seed them onto named production accounts? The repository
  holds no mapping from those names to production account IDs.
- Is an account offered as sign-off when the flag is Yes but qualifications
  or the signature are missing? D31 says "flagged"; the mockup requires a
  signature on file.
