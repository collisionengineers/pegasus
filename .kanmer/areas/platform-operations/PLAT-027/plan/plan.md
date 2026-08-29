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

## Adversarial verifier remediation and dispositions — 2026-08-29

1. **High — accounts empty snapshot could not be selected. Fixed.**
   Restored the existing `StateMatches` token,
   `No staff accounts are available.`, as the lane-owned page's `h2`.
   Retargeted the existing exact-markup assertion. The removed application-
   initialization explanation stays removed.
2. **Medium — default snapshot selection became ambiguous. Fixed by the same
   hunk.** The empty matcher now matches only the empty response again, so the
   matcher's negation excludes it from the default selection.
3. **Medium — Review reason narrowed from 1000 to 500. Fixed.** The shared
   reason dialog now accepts `DialogReasonMaxLength`; this page passes
   `StaffAccountAdministrationPolicy.MaximumReasonLength` to both Disable
   and Review. No task branch owned the shared partial when checked. A focused
   integration test submits a 1000-character Review reason.
4. **Low — rejected role assignment lost its reason. Fixed.** The page model
   retains the posted staff id and exact reason, and only the targeted row
   receives that value on the rejected render.
5. **Low — completed first-password-change state disappeared. Fixed.**
   `OperatorLabels.StaffAccounts.PasswordChangeComplete` was appended and the
   false branch now renders it through the existing status-chip partial.
6. **Low — the Infrastructure correction lacked a plan disposition. Fixed
   here.** Commit `774ff072` is accepted under disposition 2: the nullable
   projection is one token, the defect made this lane's access-review readout
   false, and branch inspection found no other task branch changing
   `EfStaffAccountQueries.cs`.
7. **Low — the report overclaimed checklist completion. Fixed.** The checklist
   remains deliberately incomplete: after this remediation it is 25/28, with
   Browser/snapshot regeneration, wave-5 route removal, and post-merge proof
   still unticked.

Honesty correction: CI's Browser job was green on prior head `a03e5e07`.
This lane did not run Browser locally, and the remediation head is not claimed
as browser-verified until its own CI completes.

## Remediation simplification pass — 2026-08-29

- **Reuse:** retained the existing Core reason constant, page model, operator-
  label list, status chip, reason dialog, and Test UI matcher. No parallel
  helper or markup copy was introduced.
- **Simplification:** the shared dialog receives one scalar limit instead of
  duplicating dialog markup in the accounts page. The two role-post values are
  the minimum state needed to restore the rejected render.
- **Efficiency:** no query, mutation, allocation policy, or client script was
  added. The existing bounded reads and redirect paths are unchanged.
- **Altitude:** no service, interface, package, route, configuration option, or
  architectural layer was added.
- **Disposition:** no further behaviour-preserving simplification was found.
  `git diff --check` passed; the remediation is 63 insertions and 10
  deletions across six files.

## Merge repair — 2026-08-29 (post AUTO-006 merge, PR #619 CONFLICTING)

`origin/dev` advanced (PLAT-054, TICK-058, INTK-001, PLAT-052, AUTO-006 merged)
while this PR was `ready-to-merge`. Ran `git fetch origin && git merge
origin/dev --no-edit` in the ticket worktree.

- **Conflict:** `src/Pegasus.Web/Presentation/OperatorLabels.cs` — this
  lane's `StaffAccounts` nested static class collided with AUTO-006's
  `AutomationAdmin` nested static class, both appended at the same location.
  Resolved by keeping both classes intact, side by side, each with its own
  closing brace; reordered nothing; no member renamed or dropped. Diffed
  both pre-merge versions (`HEAD` and `origin/dev`) against the resolved
  file to confirm every const/method survived verbatim.
- **Round-2 shape re-confirmed:** the accounts empty-state token ("No staff
  accounts are available.") in
  `src/Pegasus.Web/Pages/Administration/Accounts/Index.cshtml` still lives
  only inside `@if (Model.Rows.Count == 0) { ... }` (lines 28-33) — this file
  had no merge conflict, untouched by the incoming lanes.
- **Build:** `dotnet build ./Pegasus.slnx --configuration Release` fails —
  but on a pre-existing `origin/dev` defect, not on anything this merge
  touched (see Defects section below). Built
  `tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj` directly
  (Release) instead: 0 warnings, 0 errors.
- **Tests:** `dotnet test
  tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj
  --configuration Release --no-build --filter
  "FullyQualifiedName~StaffAccountsAndRolesWebTests"` →
  **Passed! Failed: 0, Passed: 4, Skipped: 0, Total: 4** (55s).
- Pushed `9b0b2132` (merge commit) to
  `task/plat-027-staff-accounts-roles`; PR #619 is back to `MERGEABLE`.
  Did not merge.

### Defect found outside this lane (reported, not fixed)

`dotnet build ./Pegasus.slnx --configuration Release` fails solution-wide:

```
tests\Pegasus.Core.Tests\ProviderApi\ProviderSubmissionTests.cs(284,13):
error CS1739: The best overload for 'QueuedIntakeStatus' does not have a
parameter named 'CaseId'
```

`QueuedIntakeStatus` (`src/Pegasus.Core/Intake/DurableIntake.cs:93`) has no
`CaseId` parameter; `ProviderSubmissionTests.cs:284` constructs one with
`CaseId: null`. Confirmed both files are byte-identical between this
branch's merge commit and `origin/dev` (`git show HEAD:<path>` vs `git show
origin/dev:<path>` — no diff) — this lane never touched either file. Traced
to TICK-058 (`63009b02 fix(provider-api): an Audit must attach exactly one
original report`, already on `origin/dev`). Outside PLAT-027's scope per
hard rule "touch only your lane's files" — reporting for TICK-058's owner to
fix, not fixing here.

## Cross-model review and remediation — 2026-08-29

A `gpt-5.6-sol` reviewer returned `REQUEST_CHANGES` with three blockers and
several findings; `gpt-5.6-luna` remediated in `561f4169`, `f9dffa48`,
`fda81e54`. The orchestrator verified the security-relevant blocker and the
assertion integrity independently rather than accepting the report.

### Blocker 1 — an account could disable and review itself · **FIXED IN CORE**

`docs/frd/frd-04-parties-accounts-and-access.md:66`: *"An account never disables
or reviews itself."* Neither Web nor Core enforced it — Disable
(`Index.cshtml.cs:141`) and Review (`:161`) forwarded the submitted `staffId`
straight through, so an administrator could disable their own account or sign off
their own access review.

**The rule now lives in `Pegasus.Core`, where business policy belongs** —
`src/Pegasus.Core/Identity/StaffAccountAdministration.cs:521`:

```csharp
internal static void RequireDifferentStaffAccount(ActionActor actor, Guid staffId)
{
    if (actor.Kind == ActorKind.Staff
        && Guid.TryParse(actor.SubjectId, out var actorStaffId)
        && actorStaffId == staffId)
    {
        throw new StaffAccountAdministrationException(
            StaffAccountAdministrationError.SelfAction);
    }
}
```

Called from **both** `DisableStaffAccount` (`:440`) and `ReviewStaffAccess`
(`:487`), beside the existing `RequireAdministrator` / `RequireStaffId` guards, so
there is one owner and no second implementation. `SelfAction` is added to
`StaffAccountAdministrationError`. The page additionally does not render either
control for the signed-in operator's own row, so no control is offered that would
be refused.

**Verified by the orchestrator**, `IdentityUseCaseTests.StaffAccountCannotDisableOrReviewItself`:
it asserts both calls throw `SelfAction` **and** that `store.DisableRequest` and
`store.ReviewRequest` are still null — proving the refusal happens *before* the
side effect, which is the rule that actually matters.

**Accepted risk, recorded:** `Guid.TryParse` returning false would skip the guard
rather than refuse — fail-open on a security check. It is unreachable in practice:
`IdentityContracts.cs:73` is the only staff-actor construction path and always
writes `staffId.ToString("D")`, and `CaseQueries.cs:345` uses the identical
`Kind: ActorKind.Staff && Guid.TryParse(...)` idiom. Matching the existing
convention was the right call; the alternative would be a new pattern in one
place. Noted so a future reader does not mistake it for an oversight.

### Blocker 2 — `<select multiple>` silently revoked roles · **FIXED BY REUSE**

A native `<select multiple>` clears every selection when an option is clicked
without Ctrl, and the handler then persisted the reduced set and revoked
sessions — an administrator lost roles by one ordinary click. The superseded
checkbox workflow had no such hazard.

Replaced with the **existing checkbox convention already in
`Administration/Roles/Index.cshtml`** rather than a new control: a
`<fieldset class="role-choices">` of `<label class="choice"><input
type="checkbox" name="selectedRoles" form="…">`. The `form` attribute keeps them
bound to the row's Save form. The existing convention wins.

### Blocker 3 — Disable and Review did nothing without JavaScript · **FIXED, matching TICK-223**

Both were `type="button"` controls whose only behaviour was `data-dialog-open`,
against `site.js`'s own rule that every enhancement sits over markup that already
works. They are now real links to
`/Administration/Accounts/Confirm/{Disable|Review}/{staffId}`, which serves a
working POST form targeting the existing Index handlers, and they keep
`data-dialog-open` for the JavaScript enhancement. This is the same
static-target-plus-enhancement shape [[TICK-223]] records, so the two do not
diverge.

### Remaining findings — dispositions

| Finding | Disposition |
| --- | --- |
| Rejected role post lost submitted roles | **Fixed** — both roles and reason survive |
| Rejected Create post lost username and reason | **Fixed** |
| Rejected Disable/Review lost the dialog reason | **Fixed** (continues the existing `_ReasonDialog.cshtml` branch change) |
| Disable/Review reason limit inconsistent with Core | **Fixed** — both use Core's 1000-character limit |
| Account count did not show truncation | **Fixed** — shows `+` when the bounded query has more rows |
| Password chips lacked explicit tones | **Fixed** |
| Save dirty-state behaviour | **Deferred to [[TICK-223]]** — it belongs in `site.js`, which this lane must not touch |
| Two bounded Core query contracts remain separate | **Risk accepted** — unifying the snapshots needs its own Core design ticket; duplicating one here would be worse |
| No plan section for the `<select multiple>` hazard originally | Superseded by blocker 2's fix |

### Assertion integrity — verified, not taken on report

Across `origin/dev...HEAD`: **1** removed `Assert.` line, **0** new `Skip`/
`[Ignore]`, **0** deleted test methods. The single removal is a **strengthening**:

```diff
- Assert.Contains("No staff accounts are available.", …)
+ Assert.Contains("<h2>No staff accounts are available.</h2>", …)
```

Same sentence, now pinned to its exact heading element. Nothing was weakened.

New coverage: +22 lines in `Pegasus.Core.Tests/Identity/IdentityUseCaseTests.cs`
and +349 in `StaffAccountsAndRolesWebTests.cs`.

### Verification

- `dotnet build ./Pegasus.slnx --configuration Release -nodeReuse:false` — exit
  0, 0 warnings, 0 errors, **0 `CS####` diagnostics**.
- `dotnet test … --filter "FullyQualifiedName~StaffAccountsAndRolesWebTests"` —
  **Failed 0, Passed 4**.

### Still open

TICK-223's Save dirty-state behaviour; the accepted two-query snapshot risk;
snapshot regeneration and route-removal work, which belong to the orchestrator
and wave 5 respectively; and the hosted CI run against current `dev`.
