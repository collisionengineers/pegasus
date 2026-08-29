## Post-implementation report — PLAT-026 Mail settings administration port

### Summary

Ported the Administration Mail settings area onto the `admin-layout` shell
per EPIC-011 `context.md` §1.12, folding the pre-existing separate
`Mailboxes` and `MailCategories` pages into one area (Approved mailboxes
table + Mail categories table) as the contract requires. Implemented by the
driven agent, then independently verified, corrected, and simplified by this
session.

### What changed (file-by-file)

- `src/Pegasus.Web/Pages/Administration/Mailboxes.cshtml` — re-skinned onto
  `<div class="admin-layout">` + `<partial name="Shared/_AdminNav" />` +
  `panel`/`panel-head` (h2/description/meta), following the
  `Pages/Operations/Index.cshtml` (PLAT-023) multi-table house style. Both
  tables (Approved mailboxes: Mailbox, Scope, Last update, State, Activated,
  Subscription, Review folders/Refresh; Mail categories: Category, State,
  Review) now live on this one page, each with its own "Add" disclosure.
- `src/Pegasus.Web/Pages/Administration/Mailboxes.cshtml.cs` — added the
  Mail-categories journey (`OnPostSaveCategoryAsync`, `ListApprovedOutlookCategories`,
  `UpdateApprovedOutlookCategory` DI) alongside the existing mailbox journey.
  Introduced `MailboxFormInput`/`CategoryFormInput` nested `[BindProperty]`
  DTOs (replacing flat top-level bound properties) so the two forms on one
  page model don't collide, with per-form validation via a shared generic
  `ValidateForm<TForm>`. `AutomationComposed`/`ViewData["AdminAutomationComposed"]`
  wired to match the existing `_AdminNav`/`Administration/Index.cshtml`
  convention that decides whether "Automation & AI" is listed.
  **Simplification fix (`ce3fbd66`):** merged `RequireMailboxForm()` /
  `RequireCategoryForm()` — structurally identical apart from DTO type —
  into one generic `RequireForm<TForm>(TForm? form, Action<TForm> assign)`.
- `src/Pegasus.Web/Pages/Administration/MailCategories.cshtml` /
  `.cshtml.cs` — the old bespoke page (own layout, own `Save` handler
  duplicating category business calls) replaced with a two-line
  `[Authorize(Policy = StaffRoleNames.Administrator)]`
  `RedirectToPagePermanent("/Administration/Mailboxes")`. Left in place as
  the smallest honest seam; **reported below for UIIMP-009's deletion list**,
  not deleted by this ticket.
- `src/Pegasus.Web/Presentation/OperatorLabels.cs` — new `MailSettings`
  nested static class appended at the end of the file (line 989 onward); no
  existing member reordered. Carries every label, the `Meta`/`MailboxState`/
  `CategoryState`/`FolderState`/`PollStatus`/`SubscriptionStatus` projections
  moved out of the page model, one list for this concept.
- `tests/Pegasus.IntegrationTests/ApprovedMailboxAdministrationWebTests.cs` /
  `ApprovedOutlookCategoryAdministrationWebTests.cs` — updated the bound
  form-field names in every POST (`MailboxId` → `MailboxForm.MailboxId`,
  etc.) to match the new nested DTOs; updated markup assertions to the new
  `<dt>/<dd>` definition-list structure (previously an em-dash string);
  routed the category tests' POSTs/GETs at `/Administration/Mailboxes` with
  `?handler=SaveCategory`; added a redirect test
  (`SupersededCategoryRouteRedirectsToMailSettings`) and new admin-layout /
  non-administrator-forbidden assertions
  (`AdministratorSeesTheAdminLayoutBothTablesAndEveryHandler`,
  `NonAdministratorCannotOpenMailSettings`,
  `NonAdministratorCannotPostMailSettingsHandlers`). No assertion weakened,
  skipped, or deleted — every changed assertion targets the legitimately
  changed markup/route.
- `docs/design/test-ui/catalogue.json` — `MailCategories` entry reclassified
  `"redirect"` with a `reason` string, matching the existing convention used
  by every other redirect entry in the file (checked: 6 other `redirect`
  entries use the same `classification`/`reason` shape). `Mailboxes` entry's
  scenario `branch` description updated to mention the folded categories
  table. Structural only — no snapshot capture run.

### Why / reuse

- `_AdminNav.cshtml`, `_StatusChip.cshtml`, `_PageHeader.cshtml` (all
  PLAT-029, untouched) — reused, not modified.
- `MailLogicalFolders.All` — reused for the folder-bindings disclosure,
  unchanged.
- `Pages/Operations/Index.cshtml` panel-head convention — followed, not
  duplicated with a new pattern.
- `Administration/Index.cshtml`'s `AutomationComposed`/
  `ViewData["AdminAutomationComposed"]` pattern — followed exactly (same DI
  service, `AutomationClientRegistry`, same ViewData key).
- Every Core port (`ListApprovedMailboxes`, `UpdateApprovedMailbox`,
  `IApprovedMailboxPollStatusQueries`, `IApprovedMailboxSubscriptionStore`,
  `IResolveApprovedMailboxIdentity`, `ListApprovedOutlookCategories`,
  `UpdateApprovedOutlookCategory`) unchanged — no new Core port, no business
  rule change, no migration, no package.

### Verification performed by this session (not just the driven agent)

- Merged `origin/dev` (`b92cb9a7`) into the branch first — clean
  fast-forward, no divergent commits, no conflicts.
- Confirmed every changed file is inside PLAT-026's allowed set (`git diff
  --stat origin/dev...HEAD`): 8 files, all owned by this ticket.
- Read `git diff origin/dev...HEAD -- tests/` line by line: every changed
  assertion targets legitimately changed markup/routes/field names; nothing
  weakened, skipped, or inverted.
- **Build** (real, independent re-run): `dotnet build ./Pegasus.slnx
  --configuration Release` → exit code `0`, 0 warnings, 0 errors. (First
  attempt hit a transient MSBuild node-reuse file lock from a leftover
  process; cleared with `dotnet build-server shutdown` and rebuilt clean.)
- **Tests** (real, independent re-run): `dotnet test ./Pegasus.slnx
  --configuration Release --no-build --filter
  "FullyQualifiedName~ApprovedMailboxAdministrationWebTests|FullyQualifiedName~ApprovedOutlookCategoryAdministrationWebTests"`
  → **17 passed, 0 failed, 0 skipped** (matches the driven agent's reported
  11+6=17).
- Ran a single-pass simplification review (no Agent-tool fan-out available)
  over the full diff; applied the one clear, verified, low-risk fix
  (generic `RequireForm<TForm>`), rebuilt (exit 0) and re-ran the same
  filter (17/17, unchanged) to confirm it was behaviour-preserving. Full
  disposition list recorded in `plan` under "Simplification pass —
  2026-08-29".
- Corrected an inaccuracy in this ticket's own `research` document: an
  earlier draft, written after reading the worktree mid-edit, wrongly
  described the already-ported markup as the pre-existing baseline. Fixed
  against `git show origin/dev:<path>`.

### Activated / Subscription columns — explicit regression check

Both columns' backing calls are present unchanged in the merged diff:
`OperatorLabels.OfficeTime(mailbox.ActivatedAtUtc, OperatorLabels.MailSettings.NotActivated)`
for Activated, and `Model.SubscriptionStatusFor(mailbox)` →
`OperatorLabels.MailSettings.SubscriptionStatus(subscription)` (backed by
`IApprovedMailboxSubscriptionStore`, `LifecycleState`/`ExpiresAtUtc`/
`LastMaintenanceFailureCode`) for Subscription. Confirmed present as
distinct `<th>`/`<td>` columns in `Mailboxes.cshtml` and asserted by
`AdministratorSeesTheAdminLayoutBothTablesAndEveryHandler`. No regression:
these remain real, load-bearing Graph-backed state, not decoration.

### Out-of-scope defects found (reported, not fixed — another ticket's file)

- `src/Pegasus.Web/Pages/Administration/Index.cshtml` still draws a separate
  "Outlook categories" card pointing at the now-superseded route. This file
  is not in PLAT-026's owned set (it is the Administration hub page); **for
  UIIMP-009's wave-5 deletion list**, alongside the `MailCategories` redirect
  route itself.
- Kanmer live-board orientation (`get_status`) was unavailable to the driven
  agent because the configured tunnel returned an SSE probe 404 — no board
  state was touched by that agent; this session drove the board directly
  through MCP instead.

### Commits

- `95ea1ce6` — `feat(admin): port mail settings area (PLAT-026)` (driven
  agent).
- `ce3fbd66` — `refactor(admin): merge duplicate form-required helpers
  (PLAT-026)` (this session's simplification pass).

Both pushed to `origin/task/plat-026-mail-settings`.

### Risks / open questions

- UIIMP-009 (wave 5) must delete the `MailCategories` redirect route and the
  superseded card on `Administration/Index.cshtml`.
- The orchestrator still owns Test UI snapshot regeneration for this page
  (once per merge, per the epic's merge-ordering constraint) — not run here.
- Two lower-confidence simplification findings (parallelizing `LoadAsync`'s
  four queries; the hand-rolled `ValidateForm` vs. built-in binder
  validation) were deliberately left unfixed as correctness-adjacent
  questions rather than same-pass simplifications — see the dated heading in
  `plan`.
