# Page 21 review — Staff roles

Screenshot: `staff-roles.png` · Source: `src/Pegasus.Web/Pages/Administration/Roles/Index.cshtml`
Route: `/Administration/Roles` · Reviewed against `docs/ui-work/ui-standards-and-review.md`.

## 1. Aesthetics

- This page has the heaviest uppercase load in the Administration section: eyebrow
  ("ADMINISTRATION"), section label ("CURRENT ROLE ASSIGNMENTS"), visible caption ("Current
  staff role assignments" — the same words in lowercase), four column headers, and then a
  *fieldset legend rendered in uppercase inside every row* ("ROLES FOR
  DEVELOPMENT-OFFLINE-ADMINISTRATOR"). The legend alone is longer than the row's data.
- The inline per-row form makes each row ~200px tall: a bordered checkbox fieldset, a
  full-width Reason input, and a red button stacked inside a table cell. One account renders as
  a form wearing a table costume; ten accounts would render ten submit buttons and ten amber
  attention contexts' worth of visual weight.
- The amber attention card is correctly the only warm element, but it carries three sentences
  and sits between the section label and the table, pushing data below the fold before the
  operator sees a single row.
- "Current roles" as a text column *and* the same information as checkbox state two cells to
  the right is the same fact stated twice per row.

## 2. Practicality

- The attention card reads: **"Every enabled staff member needs at least one role. Removing the
  final enabled Administrator is denied. Role changes invalidate existing browser sessions."**
  All three facts are real consequences, but "is denied" and "invalidate existing browser
  sessions" are the system talking about itself. This is legitimate consequence copy (the one
  category the standards allow) — it just needs to be one sentence in business language.
- The per-row form is actually the right interaction shape for this job — role assignment is a
  per-person act with a per-person reason — but the Reason input is a bare single-line
  `<input>` with no hint about what a good reason looks like or where it goes, and it renders
  even when the operator has changed nothing.
- Save affordance is identical for every row ("Save roles"), so with several accounts on screen
  nothing ties a button to a person except proximity; the legend does this for screen readers
  but visually the fieldset border is doing all the work.
- There is no unsaved-changes signal: tick a checkbox, scroll, and the only way to know a row
  is dirty is remembering. Acceptable server-rendered behaviour, but the button could at least
  sit inside the same visual group as the checkboxes it commits (it does) — the gap is that
  nothing distinguishes a modified row from an untouched one.
- The empty state ("No staff accounts are available for role assignment.") is serviceable and
  business-worded — fine.

## 3. Performance, design and good practice

- Each row generates a fresh `OperationKey` GUID per render (`Guid.NewGuid().ToString("N")` in
  the loop). Correct per-row idempotency — notably *better* than the Access review page, which
  shares one key across all row forms (see page 22 review). The two pages should share one
  pattern, and it should be this one.
- Role names come from `StaffRoleNames.All` and render raw ("Administrator", "Engineer",
  "User") — these are readable business words today, so no operator-label map is strictly
  needed, but the page silently depends on the C# constant names staying human.
- The visible legend per row is good accessibility ("Roles for jane.smith") rendered badly —
  it should stay in the accessibility tree and leave the visual table.
- Checkbox inputs carry no per-row disabled logic for the last-Administrator rule: the page
  lets the operator uncheck the final Administrator and submit, then serves a failure. Honest
  server-side enforcement, but the UI invites the exact action the attention card just said is
  denied — the checkbox could be disabled with the reason shown on hover/adjacent text when the
  account is the last enabled Administrator.
- Single query, no scripts, no polling — no performance concerns at office scale.
