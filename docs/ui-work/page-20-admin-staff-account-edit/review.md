# Page 20 review — Manage staff account

Screenshot: `staff-account-edit.png` · Source: `src/Pegasus.Web/Pages/Administration/Accounts/Edit.cshtml`
Route: `/Administration/Accounts/Edit/{id:guid}` · Reviewed against `docs/ui-work/ui-standards-and-review.md`.

## 1. Aesthetics

- The heading works: "Manage development-offline-administrator" with the Enabled chip aligned
  right is the clearest page-heading composition in the Administration section. The chip states
  the fact; nothing narrates it.
- Chrome stack is still doubled: "Back to Staff accounts" link plus "ADMINISTRATION" eyebrow
  plus H1 — three orientation devices where a breadcrumb would do all of it in one line.
- The split layout leaves a large dead zone: a three-row detail list on the left versus a
  Reason-plus-button form on the right, then ~500px of empty paper. For a page this small the
  split is doing very little; it survives mostly because its siblings use it.
- Status appears twice — as the heading chip and again as the first row of the detail list
  ("Status: Enabled"). One of them is redundant.
- "ACCOUNT DETAIL" and "ACCOUNT ACTION" section labels are near-content-free; with only two
  panels on screen, the panels explain themselves.

## 2. Practicality

- The consequence copy under the disable form — **"Disabling revokes existing browser sessions.
  The account is retained permanently."** — is the right kind of guidance (consequence before
  commitment) but sits *after* the submit button, where it reads as an afterthought. It belongs
  between the Reason field and the button, and "revokes existing browser sessions" is systems
  phrasing for "signs the person out everywhere".
- The disabled-state panel reads: **"This account is disabled and retained in permanent
  administration history. There is no delete or password-display action."** Listing the actions
  the page does *not* have is narrating the mechanics (§1.1); a disabled account needs one
  sentence of fact, not a catalogue of absent buttons.
- "Last access review" renders `reviewedAtUtc.ToString("u")` — a raw `2026-07-14 09:42:00Z`
  sorting format in UTC. Every operator-facing time elsewhere renders in London time
  (`_FreshnessBanner` pattern); this page should match, and "Not recorded" for a never-reviewed
  account is fine but there is no link to the Access review page where the review would happen.
- "First password change: Required / Complete" — same double-negative reading as page 19; the
  fact is whether the password is still the issued temporary one.
- There is no re-enable action anywhere. If disabling is genuinely one-way, the page never says
  so before the click; if it is not, the capability is missing. Either way the operator cannot
  tell from this screen.
- The red "Disable account" button uses the same primary treatment as every constructive action
  in the app. Red-as-brand and red-as-danger collide precisely here, where the action is
  destructive-ish; the one page where the colour means both things at once.

## 3. Performance, design and good practice

- The defensive `@if (Model.Account is not null)` wrapper with the comment that "the not-found
  surface is owned by the error page" is honest, but the error page it defers to is one of the
  raw-404 surfaces flagged in the root review — so an expired bookmark lands on an unstyled
  browser error.
- The consequence copy is a `<p class="empty-state">` — the empty-state class misused as help
  text again (shared defect with pages 19, 24, 25; needs one `.hint` class).
- The back-link reuses the right-arrow sprite for a back affordance, as on the other admin pages.
- `OperationKey` hidden input for idempotent disable — correct, invisible.
- No performance concerns: single record load, no scripts, no polling.
- Accessibility is largely right (labelled sections, `<time datetime>`, validation summary), but
  the status chip in the heading is the only place the enabled/disabled fact is stated visually
  ahead of the fold — chip colour plus text passes the never-colour-only rule.
