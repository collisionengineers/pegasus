# Research — PLAT-027 consolidate Staff accounts, roles and access review

Every premise below was checked read-only against the worktree at
`origin/dev` = `b92cb9a7` (merged into `task/plat-027-staff-accounts-roles`
as a fast-forward, 0 ahead / 60 behind before the merge). Nothing here is
assumed unless it says so.

## The contract being implemented

EPIC-011 `context.md` §1.12 and `docs/design/README.md`
§"Administration `/Administration/{area}`" state the same table:

> Accounts: table Name, Username, Role (inline select), State, Save
> (disabled until changed; reason prompt), Account (Disable danger /
> Review); Create staff account.

§1.14 and `docs/design/README.md` §"Removed surfaces" fold Organisations,
Access review and Roles out of the area rail.

## Verified — the seam is already half-cut

- `Pages/Administration/Shared/_AdminNav.cshtml` (PLAT-029) lists exactly
  five areas and links only `/Administration/Accounts/Index` for staff
  access. There is **no** nav entry for `/Administration/Roles` or
  `/Administration/Access`. Verified by reading the partial.
- `Pages/Administration/Index.cshtml` (the area landing) links Accounts,
  Principals, Configuration, Mailboxes, MailCategories, Organizations and
  Automation. It does **not** link Roles or Access either.
- So `/Administration/Roles` and `/Administration/Access` are already
  unreachable from navigation; they are live routes with no inbound link.
  The consolidation therefore needs no route change and no stub — the
  smallest honest seam is "build the one area at
  `/Administration/Accounts`, leave the three superseded routes in place
  for UIIMP-009 to delete".

## Verified — what the three pages actually do

`src/Pegasus.Core/Identity/StaffAccountAdministration.cs` owns all of it.

| Page | Read port | Mutation port | Core right on the mutation |
| --- | --- | --- | --- |
| `Accounts/Index` | `IListStaffAccounts` | `ICreateStaffAccount` | `ManageStaffAccounts` |
| `Accounts/Edit` | `IGetStaffAccount` | `IDisableStaffAccount` | `ManageStaffAccounts` |
| `Roles/Index` | `IGetRoleAssignments` | `IAssignStaffRoles` | `AssignStaffRoles` |
| `Access/Index` | `IGetAccessReview` | `IReviewStaffAccess` | `ReviewStaffAccess` |

- All four read ports call the same `IStaffAccountQueries.ListAsync` /
  `GetAsync`. `GetRoleAssignments` and `GetAccessReview` are projections of
  the very same `StaffAccountSummary` rows.
- `StaffAuthorization.IsAuthorized` maps `ManageStaffAccounts`,
  `ReviewStaffAccess` and `AssignStaffRoles` to the **same** predicate:
  `actor.Kind == Staff && actor.IsInRole(Administrator)`. Consolidating the
  three surfaces therefore widens nobody's access, provided each use case
  is still called (each re-checks its own right inside Core).
- `StaffAccountAdministrationPolicy.MaximumReasonLength = 1000` and
  `ActionHistory.Reason` is `HasMaxLength(1000)`
  (`PegasusDbContext.cs:883`). The two agree — no DOCS-012-class overflow.

## Verified — access review is a real capability, and what it carries

`GetAccessReview` (line 287) projects each `StaffAccountSummary` into
`StaffAccessReviewProjection(StaffId, UserName, IsEnabled, CurrentRoles,
LastReviewedAtUtc, ReviewIsOutstanding)` where

```csharp
ReviewIsOutstanding = account.IsEnabled && account.LastAccessReviewAtUtc is null
```

`IReviewStaffAccess` records a reasoned, attributable review against the
staff account; it changes no access. So the capability is exactly two
things: **the Review action** and **the outstanding/last-reviewed readout**.

- The action fits the contract's table directly: the `Account` column
  already draws `Review` beside `Disable`.
- The readout has no column drawn for it. It cannot be re-derived in the
  Web layer without duplicating a Core business rule (a stop condition
  under `docs/engineering.md#one-core-owner`), so the consolidated page
  keeps calling `IGetAccessReview` and renders its `LastReviewedAtUtc` /
  `ReviewIsOutstanding` in one added column. Nothing is dropped.

## Verified — the role model is a *set*, and a single select would lose access

- `StaffRole` is `{ Administrator, Engineer, User }`;
  `AssignStaffRolesRequest.Roles` is `IReadOnlyCollection<StaffRole>` and
  `StaffAccountSummary.Roles` is a list. The existing `Roles/Index` page
  therefore renders a checkbox set, not a select.
- `EfStaffAccountAdministration.cs:67` creates every new account with
  exactly `[StaffRole.User]`, so multi-role accounts only ever arise
  through the Roles page.
- **Administrator does not subsume Engineer.**
  `Core/Identity/CaseEngineerEligibility.cs` gates engineer assignment on
  `HasEngineerRole` specifically. Collapsing `{Administrator, Engineer}` to
  a single most-privileged role would silently remove that person from
  engineer eligibility.

Conclusion: a single-valued `<select>` is lossy. A `<select multiple>` is
still "an inline select", carries the Core set exactly, needs no script and
loses nothing. That is what this ticket ships; the divergence is recorded
in the plan.

## Verified — two contract items cannot be built inside this lane

1. **"Save (disabled until changed)".** `wwwroot/js/site.js` has no
   enable-on-change behaviour and no data hook for one (`data-auto-submit`
   submits a filter form on change — the opposite). Adding one means editing
   `site.js`, which this lane must not touch (TICK-223 and UIIMP-009 are on
   it). A permanently `disabled` Save would be an inert control, banned by
   `context.md` D7 and `docs/design/README.md` §"Absent versus disabled".
2. **The role Save's reason as a `_ReasonDialog`.**
   `Pages/Shared/_ReasonDialog.cshtml` renders its own self-contained
   `<form>` with no `id`, and carries only server-known
   `DialogHiddenFields`. The role a user picks in an inline select is a
   client-side value; without a script to copy it into the dialog form, or
   an `id` on that form to associate the select with (`Pages/Shared/**` is
   out of this lane), the dialog cannot carry it. `_ReasonDialog` is used
   for Disable and Review, whose payloads are fully known at render.

## Verified — class vocabulary traps

`site.css` line 851 opens `/* ==== LEGACY (wave 5 deletes) ==== */`.
`.row-confirm` (line 1980) and `details > summary.btn` (line 1984) are
**inside** that block. The row-level `<details>` reason disclosure used by
`Pages/Operations/Index.cshtml` therefore loses its styling in wave 5.
Recorded as an out-of-lane defect; this page does not use those classes.

In-vocabulary and non-legacy: `admin-layout`, `admin-nav`, `panel`,
`panel-head`, `panel-body`, `table-wrap`, `no-border`, `status`, `btn`,
`btn--primary`, `btn--danger`, `field`, `stack`, `section-gap`, `muted`,
`empty`, `sr-only`, `validation-summary` (line 304), `notice`.

## Verified — no display name exists

`StaffAccountSummary` is `(Id, UserName, IsEnabled, MustChangePassword,
Roles, LastAccessReviewAtUtc)`. There is no person-name field anywhere in
`Core/Identity`, and `Core/Actors/ActorDisplayNames.cs` resolves a staff
actor to its **username**. The contract's separate "Name" column has no
backing data; inventing one would be fabricated domain data (AGENTS.md
rule 13).

## Verified — the existing test surface

- `tests/Pegasus.IntegrationTests/TestUiFocusedRenderTests.cs` GETs
  `/Administration/Accounts/Edit/{id}` (asserts `"Manage "`) and
  `/Administration/Accounts` empty (asserts
  `"No staff accounts are available."`).
- `tests/Pegasus.IntegrationTests/Browser/AccessibilityTests.cs:29` lists
  `/Administration/Accounts` among its routes (Browser category — not run
  by this lane).
- `docs/design/test-ui/catalogue.json` has entries for all three routes plus
  `Accounts/Edit`. Because none of those pages is deleted here, the
  catalogue needs no structural change, and per the 2026-08-29 decisions a
  lane must not regenerate snapshots in its own worktree.
- **No web test anywhere covers `/Administration/Roles` or
  `/Administration/Access`** — grep for `Save roles`, `Record reviewed`,
  `Access review`, `/Administration/Roles`, `/Administration/Access` over
  `tests/` returns nothing. The consolidated area therefore needs new tests
  of its own.

## Assumed, not verified

- That `<select multiple size="3">` renders acceptably inside the ported
  table styling. `site.css:189` styles `select` generically with
  `min-height:40px`, which `size` overrides; not confirmed in a browser
  because this lane does not run Browser tests.
- That the four parallel administration lanes (PLAT-025/026/028, AUTO-006)
  each set `ViewData["AdminAutomationComposed"]` themselves. Only
  `Administration/Index.cshtml` does so today.
