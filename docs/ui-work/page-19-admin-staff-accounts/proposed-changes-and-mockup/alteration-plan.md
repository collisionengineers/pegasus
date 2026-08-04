# Alteration plan — Staff accounts (page 19)

## Review summary

A structurally sound split page (accounts table + create form) buried under four layers of
uppercase chrome and a visible caption that names the table three times. The empty state is
written in dev-speak ("Application initialization must complete…"), the "First password change"
column reads as a double negative, and the action column header disagrees with its own link.
Help text is styled with an empty-state class. Nothing is broken; everything is noisier and more
mechanical than it needs to be.

## Changes

1. **Navigation and orientation.** Adopt the new global nav (Dashboard · Inbox · Upload ·
   Queues · Cases · Administration). Replace the "ADMINISTRATION" eyebrow + "Back to
   Administration" pair with a single breadcrumb: `Administration / Staff accounts`
   (the "Administration" segment is the back link). One H1: "Staff accounts".
2. **Empty state copy.** Old: "No staff accounts are available. Application initialization must
   complete before ordinary administration can begin." → New: **"No staff accounts yet. Create
   the first account with the form on the right."** (Business language, points at the action.)
3. **Password column.** Old header "First password change" with "Required"/"Complete" → new
   header **"Password"** with chip **"Temporary"** (amber — the person has not yet replaced the
   issued password) or plain text **"Set"**. States the fact, not the obligation.
4. **Action column.** Header becomes visually hidden (screen-reader text "Manage"); the row link
   stays **"Manage"**. Header and link no longer disagree.
5. **Caption.** The table caption becomes screen-reader-only. On screen the section label
   "Current accounts" is the table's single name.
6. **Section labels.** Keep exactly two uppercase labels — "Current accounts" and "Create staff
   account" — per §4.7 (one per card cluster). Table headers stay uppercase per the established
   admin table pattern.
7. **Reason field.** Add one-line hint under the Reason field: **"Kept on the administration
   record."** Justifies the mandatory field without narrating architecture.
8. **Keep** the temporary-password hint verbatim ("At least eight characters. The staff member
   must replace it at first sign-in.") — earned consequence copy.
9. **Help-text markup.** Hints use a dedicated `.hint` class, not `.empty-state`.
10. **Status card.** Post-action confirmation gains success/failure variants (green hairline /
    red hairline) instead of one neutral treatment.

## Dependencies

- Global nav rename/split is owned by the whole-application IA change (root doc §3.1); this page
  only consumes it.
- Chip variant for "Temporary" (amber trio) already exists in the token set; needs a chip class,
  no new colour.
- `.hint` class addition to `site.css` is shared with pages 20–25 (same misuse everywhere).
- No page-model changes: all data shown is already on the view model
  (`MustChangePassword` drives the Password chip).

## Open questions

- Should the accounts table show "Last access review" here as well (it exists on the Edit page)?
  It would make this the one screen an administrator needs before a review cycle — at the cost
  of a fifth column. Deferred to the access-review workflow owner (page 22).
- Is there ever a legitimate operator-facing meaning for the first-run empty state, given
  accounts are seeded during initialization? If genuinely unreachable in production, the simple
  copy above is still the safer fallback.
