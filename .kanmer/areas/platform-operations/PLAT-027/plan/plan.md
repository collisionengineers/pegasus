# Plan — PLAT-027

One area at `/Administration/Accounts` titled "Staff accounts & roles",
carrying everything the three superseded pages carried. No route change, no
deletion (UIIMP-009 owns wave-5 removal).

## Steps

1. **`OperatorLabels.StaffAccounts`** — append a new nested static class
   holding the account state words, the role words and the review words.
   Reuses `OperatorLabels.OfficeTime` for the last-reviewed stamp and
   `OperatorLabels.Admin.Accounts` for the area title. Append only; nothing
   existing is reordered.

2. **`Accounts/Index.cshtml.cs`** — one page model, four handlers, no
   `[BindProperty]`.
   - Reuses the existing Core ports unchanged: `IListStaffAccounts`,
     `IGetAccessReview`, `ICreateStaffAccount`, `IAssignStaffRoles`,
     `IDisableStaffAccount`, `IReviewStaffAccess`. No new port, no new
     policy, no re-implemented rule.
   - Reuses `AdministrationPageModel.IsOperationKeyValid` and
     `StaffPageModel.{TryGetActor,NewOperationKey}`.
   - Keeps `[Authorize(Policy = StaffRoleNames.Administrator)]`; each Core
     use case re-checks its own right, so the three rights survive the fold.
   - Handler parameters rather than bound properties: four forms post to one
     page, and a `[Required]` bound property belonging to one form would
     invalidate every other form's post. This is the shape
     `Pages/Operations/Index.cshtml.cs` already uses.
   - Error messages come from the existing
     `StaffAccountAdministrationError` switch, moved verbatim — one message
     table, not three copies.

3. **`Accounts/Index.cshtml`** — `_PageHeader` (eyebrow "Administration",
   h1 the area label), then `admin-layout` = `_AdminNav` + a `stack` of two
   panels.
   - **Accounts panel**: `table-wrap no-border` table, columns
     `Username | Role | State | Last reviewed | Save | Account`.
     - Role cell: `<select multiple>` bound to the row's form by the HTML
       `form=` attribute, one option per `StaffRole`, selected from
       `StaffAccountSummary.Roles`.
     - State cell: `_StatusChip` for enabled/disabled, plus a second chip
       when the account must still change its password.
     - Last reviewed cell: `<time>` when reviewed; the amber `Due` chip when
       Core says `ReviewIsOutstanding`; an em dash otherwise. This is the
       access-review readout, folded in.
     - Save cell: the row's `<form>` with hidden staff id + operation key, a
       labelled Reason input and the Save submit.
     - Account cell: `Disable` (`btn--danger`) and `Review`, each a
       `data-dialog-open` trigger for its own `_ReasonDialog`; the Disable
       dialog carries the one approved consequence sentence.
   - **Create staff account panel**: username, temporary password, reason,
     submit. The old "At least eight characters…" hint is deleted —
     explanatory copy.
   - `_ErrorSummary` for failures; the success line renders as the shell's
     `data-confirmation` toast.

4. **Tests** — a new `StaffAccountsAndRolesWebTests.cs` covering: the
   consolidated table renders every folded control; each control's handler
   exists and answers; the role select carries all three `StaffRole` values
   with the account's current set pre-selected; the access-review readout
   renders. Plus the retargeted empty-state assertion in
   `TestUiFocusedRenderTests.cs`.

5. **Build and focused tests**, then commit in slices and open the PR.

## Divergences from the drawn contract, and why

| Drawn | Shipped | Reason |
| --- | --- | --- |
| Columns "Name, Username" | one `Username` column | `StaffAccountSummary` has no person-name field and `ActorDisplayNames` resolves staff to their username. Inventing a name is fabricated domain data. **Accept the risk**: reported for an operator decision, not filed as a ticket. |
| — | added `Last reviewed` column | The access-review readout has nowhere else to live. Without it the fold would silently drop half of a real capability. |
| `Role (inline select)` — single | `<select multiple>` | `AssignStaffRoles` takes a set, and `CaseEngineerEligibility` gates on the Engineer role specifically, so Administrator does not imply Engineer. A single-valued select would silently strip engineer eligibility from a multi-role account. Still one inline select in the Role column. |
| `Save (disabled until changed)` | Save always enabled | The enable-on-change behaviour belongs in `site.js`, which this lane must not touch. A permanently disabled Save would be an inert control. **Reported**, not worked around. |
| role Save reason via `_ReasonDialog` | labelled Reason input in the row form | `_ReasonDialog` renders a self-contained form with no `id` and only server-known hidden fields; it cannot carry a client-chosen select value without a script or a `Pages/Shared/**` change. Disable and Review — whose payloads are fully server-known — do use `_ReasonDialog` as required. |
| `.row-confirm` / `details > summary.btn` (the Operations row-reason pattern) | not used | Both live in `site.css`'s LEGACY block, deleted in wave 5. |

## Simplification pass — 2026-08-29

Run over this branch's own diff before opening the PR; findings and their
dispositions are recorded below under "Review findings".

- **Reuse.** No new Core port, no new use case, no new CSS class, no new
  JS, no new partial, no new package. Six existing ports, five existing
  shared partials, the existing `AdministrationPageModel` helpers and the
  existing error-message switch are reused as-is.
- **Simplification.** Three page models collapse to one; three copies of
  the `StaffAccountAdministrationError` message switch collapse to one;
  three `LoadAsync` methods collapse to one.
- **Efficiency.** The page issues two bounded reads (`ListStaffAccounts`
  and `GetAccessReview`, ≤100 rows each) where the three old pages issued
  three. Both are kept because each carries a distinct Core right and
  `ReviewIsOutstanding` is Core policy that must not be re-derived in Web.
  A single `IGetStaffAccessOverview` in Core would remove the second read;
  that is a Core change outside this lane and is reported, not built.
- **Altitude.** No abstraction was added; the page composes existing ports
  directly.

## Review findings and dispositions — 2026-08-29

| # | Finding | Disposition |
| --- | --- | --- |
| 1 | `Pages/Operations/Index.cshtml` (PLAT-023, merged) styles its row reason with `.row-confirm` and `details > summary.btn`, both inside `site.css`'s LEGACY block that wave 5 deletes. The Operations rows lose their styling at UIIMP-009. | **Reported, not fixed** — `Pages/Operations/**` is PLAT-023's file and `site.css` is out of this lane. Named in the post-implementation report for the orchestrator. |
| 2 | "Save disabled until changed" needs a small enable-on-change behaviour in `wwwroot/js/site.js`. | **Reported, not fixed** — the lane brief forbids touching `site.js`, and TICK-223 is in flight on it. Shipped as an always-enabled Save rather than an inert control. |
| 3 | `_ReasonDialog` cannot carry a client-chosen value because its `<form>` has no `id`. Adding `ViewData["DialogFormId"]` would fix it in one line. | **Reported, not fixed** — `Pages/Shared/**` is explicitly out of this lane and several lanes render that partial. |
| 4 | `StaffAccountSummary` has no display name, so the contract's "Name" column cannot be filled. | **Accept the risk** — one `Username` column ships. Filling it needs a Core field, a migration and grants in one diff; that is another lane's work, and the epic is not to grow by another ticket for it. |
| 5 | `GetAccessReview` and `ListStaffAccounts` read the same rows twice. | **Accept the risk** — bounded at 100 rows, and collapsing them means new Core surface. Reported as the right long-term shape. |
| 6 | `Administration/Index.cshtml` still renders an area-card landing, where `docs/design/README.md` §Routes says bare `/Administration` opens `accounts`. | **Reported, not fixed** — not a lane file; UIIMP-009 owns the landing's removal. |
| 7 | `ViewData["AdminAutomationComposed"]` is probed independently by each administration page. | **Accept the risk** — the probe is one expression; hoisting it into `AdministrationPageModel` would collide with four in-flight admin lanes. Reported for whoever lands last. |

## Stop condition

PR open against `dev`, ticket in `review`. No merge, no `proof`, no `done`.
