# Alteration plan — Staff roles (page 21)

## Review summary

The right interaction (per-person role checkboxes with a per-person reason) under the heaviest
chrome in the section: five layers of uppercase labelling, a duplicated "Current roles" text
column, a visible per-row uppercase legend, and a three-sentence attention card. The
last-Administrator protection is stated but not enforced in the controls — the page invites the
click it will refuse. Compress the chrome, keep the interaction, make the protection visible at
the checkbox.

## Changes

1. **Navigation and orientation.** New global nav; breadcrumb `Administration / Staff roles`
   replaces eyebrow + back-link. H1 "Staff roles". One uppercase section label maximum.
2. **Attention card copy.** Old (three sentences): "Every enabled staff member needs at least
   one role. Removing the final enabled Administrator is denied. Role changes invalidate
   existing browser sessions." → New (one sentence, consequence copy — allowed): **"Saving
   signs that person out everywhere, and the last enabled Administrator always keeps the
   Administrator role."**
3. **Drop the "Current roles" text column.** The checkbox states *are* the current roles; the
   column said the same thing twice per row.
4. **Per-row legend.** "Roles for {username}" stays as a screen-reader-only legend; the visible
   uppercase in-row heading goes.
5. **Last-Administrator enforcement in the control.** When an account is the last enabled
   Administrator, its Administrator checkbox renders checked and disabled with adjacent text
   **"Last Administrator"** — the page no longer invites a refused action. (Server-side rule
   unchanged; this mirrors it.)
6. **Row layout.** Single-line form per row: checkboxes inline, Reason input, "Save roles"
   button — 40px-ish row instead of ~200px. Reason keeps `required maxlength="1000"`.
7. **Reason hint.** Column header "Reason" plus shared hint once under the table: **"Reasons
   are kept on the administration record."** (Once per page, not once per row.)
8. **Caption.** Table caption becomes screen-reader-only (was visible and duplicated the
   section label).
9. **Idempotency pattern kept.** Per-row operation keys stay as-is (this page is the good
   example; page 22 should adopt it).

## Dependencies

- Breadcrumb + `.hint` class shared with the other Administration pages.
- Change 5 needs the page model to know which account is the last enabled Administrator — a
  small view-model flag computed from data already loaded (accounts + roles); no new Core
  query, but it is a page-model change, not markup-only.
- No route or handler changes; `Assign` handler contract untouched.

## Open questions

- Should a role change on your *own* account warn that you will be signed out immediately on
  save (the session-invalidation consequence lands mid-task)? Proposed: yes, a per-row inline
  note when the row is the signed-in user — not mocked, needs operator confirmation.
- Are the raw role names ("Administrator", "Engineer", "User") settled business vocabulary, or
  should they pass through an operator-label map like every other enum-derived string (§4.3)?
  They read fine today; flagging only because the page depends on C# constants staying human.
