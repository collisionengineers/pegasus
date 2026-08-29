## Plan — PLAT-026 Mail settings administration port

1. **Re-skin `Mailboxes.cshtml` onto the admin-layout shell.** Wrap the
   existing two tables (Approved mailboxes, Mail categories) in
   `<div class="admin-layout">` + `<partial name="Shared/_AdminNav" />` +
   `<section class="panel">` with a `panel-head` carrying an `h2` area label,
   a description line and a meta line (mailbox/category counts), following
   `Pages/Operations/Index.cshtml`'s (PLAT-023) house style for a multi-table
   admin panel. Reuses `_AdminNav` (PLAT-029, read-only) and `_StatusChip`.
2. **Move every literal label/copy string into `OperatorLabels.MailSettings`**
   (new nested static class, appended at the end of `OperatorLabels.cs`, no
   reordering of existing members). One list per concept: no label lives in
   both the `.cshtml` and inline C#.
3. **Preserve behaviour exactly.** Every existing handler
   (`OnPostUpdateAsync`, `OnPostResolveFoldersAsync`, `OnPostSaveCategoryAsync`,
   etc.) keeps calling the same Core ports
   (`UpdateApprovedMailbox`, `IResolveApprovedMailboxIdentity`,
   `UpdateApprovedOutlookCategory`, …) with the same optimistic-concurrency
   (`ExpectedVersion`) and operation-key replay-guard behaviour. No business
   rule changes; this is presentation-only.
4. **Preserve the Activated and Subscription columns' meaning** — same data
   sources (`ActivatedAtUtc`, `IApprovedMailboxSubscriptionStore`), same
   `OperatorLabels.MailSettings.PollStatus` / `SubscriptionStatus`
   projections, just relabelled/reflowed under the new shell.
5. **Keep `MailCategories` as the redirect stub it already is** (no route
   removal — that is UIIMP-009's job in wave 5). Report it explicitly as the
   superseded surface for UIIMP-009's deletion list.
6. **Update the two web-test files** to assert the real rendered
   `admin-layout`/`_AdminNav` markup, every handler, and that a
   non-administrator is forbidden — never weaken or delete an existing
   assertion; replace an assertion on old markup with one on the new correct
   markup.
7. **Structural `catalogue.json` edit** if the Mailboxes/MailCategories page
   entries' descriptions are now wrong (no snapshot capture — orchestrator
   regenerates once per merge).
8. **Build** (`dotnet build ./Pegasus.slnx --configuration Release`) and run
   the two focused test classes above; record real pass/fail counts.

### Reuse named per step

- Step 1: `Pages/Shared/_AdminNav.cshtml`, `Pages/Shared/_StatusChip.cshtml`,
  the `Pages/Operations/Index.cshtml` panel-head pattern.
- Step 2: the existing `OperatorLabels` static-class-per-area convention
  (e.g. `OperatorLabels.Nav`, `OperatorLabels.RouteScope`).
- Step 3–4: the existing Core ports and query interfaces, unchanged.

## Disposition of external review findings

(See "Simplification / cross-lane findings — 2026-08-29" below, added after
codex's implementation pass and this session's own verification.)

## Simplification pass — 2026-08-29

Single-pass inline review (no Agent fan-out available) over
`git diff origin/dev...HEAD` for `Mailboxes.cshtml(.cs)`, `MailCategories.*`,
`OperatorLabels.cs` (MailSettings), the two web test files, and
`catalogue.json`.

**Fixed:**

- **Reuse/simplification** — `RequireMailboxForm()` and `RequireCategoryForm()`
  in `Mailboxes.cshtml.cs` were structurally identical apart from their DTO
  type. Merged into one generic `RequireForm<TForm>(TForm? form,
  Action<TForm> assign)` helper; both call sites updated. Two existing
  concrete callers justify the generic (not speculative). Rebuilt (`dotnet
  build`, exit 0) and re-ran the focused filter (17/17 passed, same as
  before) to confirm behaviour-preserving. Committed as `ce3fbd66`.

**Considered and skipped (with reason):**

- **Efficiency** — `LoadAsync` runs four independent awaits
  (`listApprovedMailboxes`, `pollStatusQueries`, `subscriptionStore`,
  `listCategories`) sequentially; `Task.WhenAll` would cut wall-clock time on
  page load. Skipped: these queries likely share one scoped EF `DbContext`
  per request, and running EF operations concurrently on one `DbContext`
  throws at runtime ("a second operation was started on this context
  instance before a previous operation completed"). Verifying which of these
  four ports do or don't share a context is a correctness question outside a
  behaviour-preserving simplification pass, and the pre-existing three-call
  sequential pattern predates this ticket (only the fourth call was added,
  following the existing convention) — a correctness review, not this pass,
  is the right place to change it.
- **Altitude** — `PrepareFormState()` has two parallel blocks (mailbox /
  category) doing the same three things (refresh `ExpectedVersion` against
  the current store row, issue a new `OperationKey`, pick the next `New*Id`).
  Skipped: the two blocks write to different named public properties
  (`NewMailboxId`/`NewMailboxOperationKey` vs `NewCategoryId`/
  `NewCategoryOperationKey`) consumed directly by the Razor view; collapsing
  them would need a tuple-returning generic or an out-param pattern that
  reduces call-site clarity for a four-line saving. Not worth it.
- **Reuse** — the two "Add mailbox" / "Add category" `<details>` disclosures
  in `Mailboxes.cshtml` share a shape (grid-2 form, address/display-name
  field, state select, reason field, submit) but differ in field composition
  (mailbox also has the route-scope fieldset). Skipped: extracting a shared
  partial for a two-occurrence, field-set-varying block trades a small markup
  saving for a parameterised partial that is harder to read than the two
  inline forms — judged not worth it for two call sites.
- **ValidateForm + `[ValidateNever]`** — hand-rolled `Validator
  .TryValidateObject` re-implements what ASP.NET Core's built-in recursive
  `[BindProperty]` validation would likely already do for a posted nested
  form, with `[ValidateNever]` on the DTOs used to suppress the framework's
  own pass. This may be redundant, but confirming that safely needs new
  tests probing the binder's behaviour for a null vs. present nested
  property — a correctness investigation, not a same-pass simplification;
  changing it risks altering actually-validated behaviour. Left alone.

Disposition: fixed the one clear, low-risk, verified duplication; the rest
are correctness-adjacent questions flagged for a future review rather than
touched under a behaviour-preserving pass.
