# Page 19 review — Staff accounts

Screenshot: `staff-accounts.png` · Source: `src/Pegasus.Web/Pages/Administration/Accounts/Index.cshtml`
Route: `/Administration/Accounts` · Reviewed against `docs/ui-work/ui-standards-and-review.md`.

## 1. Aesthetics

- The page carries three tiers of grey uppercase chrome before any content: "ADMINISTRATION"
  eyebrow, then "CURRENT ACCOUNTS" and "CREATE STAFF ACCOUNT" section labels, then a full row of
  uppercase table headers ("USERNAME / STATUS / ROLES / FIRST PASSWORD CHANGE / ACTION"). With a
  one-row table, the labelling outweighs the labelled content roughly three to one. The root
  standards (§4.7) allow one uppercase label per card cluster; this page spends four.
- The visible table caption "Staff accounts and their current access" renders as a grey sentence
  inside the table chrome, directly under a section label that already says "Current accounts".
  Three names for the same table (label, caption, headers) within 90 vertical pixels.
- The "Back to Administration" link and the "ADMINISTRATION" eyebrow say the same thing twice
  within two lines. One orientation device is enough.
- The split layout is unbalanced in the common case: a one-row table on the left against a tall
  three-field form on the right leaves the left column ~70% empty. The balance only works once
  the office has 8+ accounts.
- The create form panel is clean and the red primary button is correctly the only saturated
  element on the page. The chip treatment for "Enabled" is restrained and works.

## 2. Practicality

- The empty state reads: **"No staff accounts are available. Application initialization must
  complete before ordinary administration can begin."** This is dev-speak — an operator who hits
  it (realistically only on a first run) is told about "application initialization" instead of
  being told what to do. It also contradicts the page itself: the create form is sitting right
  there.
- "First password change" with values "Required" / "Complete" makes the reader do a double
  negative: "Required" actually means *the person has not signed in and replaced their temporary
  password yet*. A column about the password state ("Temporary" vs "Set") says it directly.
- The action column header is "Action" but the link is "Manage" — header and content disagree;
  either the header labels the verb or it should be silent.
- The temporary-password hint ("At least eight characters. The staff member must replace it at
  first sign-in.") is genuinely useful consequence copy — the one piece of guidance on the page
  that earns its place. It should stay.
- There is no indication of how many accounts exist or any way to filter; acceptable at current
  scale, worth revisiting only if the table grows past a screen.
- The Reason field gives no cue about who sees the reason or where it goes. Administration
  actions are permanently recorded; one quiet phrase ("kept on the administration record") would
  justify the mandatory field to the person filling it.

## 3. Performance, design and good practice

- The temporary-password hint is marked up as `<small class="empty-state">` — an empty-state
  style class reused as generic help-text styling. Same misuse appears across the Administration
  pages; the stylesheet needs a real `.hint`/help-text class instead of overloading a
  state-communication class.
- The table is server-rendered from a single query and the page carries no scripts — good; there
  is nothing to fix on the performance lens at this scale.
- `OperationKey` is a hidden idempotency input — correctly invisible to the operator. No raw
  identifiers leak on this page; the Manage link routes on a GUID but never displays it.
- Accessibility basics are present (`aria-labelledby` sections, `scope="col"`, labelled inputs,
  validation spans). The caption duplication noted above is the accessible-name equivalent of
  saying the table name three times to a screen reader too.
- The back-link reuses `#icon-arrow-right` for a left-pointing affordance (flipped by CSS);
  fragile if the sprite is ever used unflipped.
- The status card (`TempData["AdministrationStatus"]`) renders post-action confirmation at the
  top of the page — the pattern is fine, but success and failure share one visual treatment, so
  a failed create and a successful create look identical at a glance.
