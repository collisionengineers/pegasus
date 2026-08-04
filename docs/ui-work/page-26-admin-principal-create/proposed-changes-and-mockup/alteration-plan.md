# Page 26 — Create principal: alteration plan

Source: `src/Pegasus.Web/Pages/Administration/Principals/Create.cshtml`.
Review: `../review.md`. Standards: `../../ui-standards-and-review.md`.

## Review summary

A structurally sound three-field form burdened by narration: an immutability lecture as the
lede, "The organization selector is bounded…" as overflow copy, "normalized to uppercase"
as a hint, and no consequence sentence at the one place it matters — the submit button. The
no-Work-Provider blocking card is already well designed and is kept.

## Container restructure (operator decision, 2026-08-04)

This screen is one record, so it takes the shape every record screen now takes
(`../../ui-standards-and-review.md` §4 rule 13): **one container** holding a header band, an
action bar, and a body with **no tab row and no state chip** — this screen creates a
record rather than showing one.

The page-12 case detail is the reference implementation. What that changes here, over and
above the numbered changes below:

- The lone form panel becomes the container body, and **Create principal** moves to the right of
  the action bar so the commitment is visible without scrolling the form. **Cancel** sits at the
  left of the same bar.
- The consequence sentence stays with the fields it concerns rather than being pinned to the
  button that has moved.
- The no-organisation alternate state removes the container entirely, as before — the blocking
  card is the only content.

Nothing in the change list is withdrawn — the copy, vocabulary, state and evidence decisions all
stand; they are re-housed.

## Changes

1. **Remove the lede.** Old: *"A principal code becomes an immutable identity. Later
   correction uses a linked successor rather than editing this code or moving existing
   cases and references."* New: nothing — the permanence consequence moves to the submit
   button (change 5).
2. **Rewrite the selector-overflow note.** Old: *"The organization selector is bounded. Use
   the Organizations workspace to confirm an organization that is not shown."* New:
   **"Showing the first 50 organisations — search in Organisations to find one that is not
   listed."** (link "Organisations" to the Organisations page; N reflects the real page
   size). Render it directly under the select, not as a page footer, and only when more
   Work Provider organisations actually exist (see Dependencies).
3. **Rewrite the code hint.** Old: *"Letters and numbers only. The code is normalized to
   uppercase and cannot be edited."* New: **"Letters and numbers only — saved in
   capitals."** Permanence is stated once, at the button, not per field.
4. **Shorten and defer the Inspection mode hint.** Old two-line hint shown always. New:
   shown only while "Image Based Assessment" is selected: **"Fills in the inspection
   address on every new case for this principal; staff can change it on a case with a
   reason."** (Static mockups show it in place under the select.)
5. **Add the single consequence sentence at the button.** New line directly above "Create
   principal": **"The code is permanent — a wrong code is corrected by replacing the
   principal, not by editing it."**
6. **One heading stack.** Drop the "ADMINISTRATION" eyebrow; breadcrumb "Administration /
   Create principal" replaces eyebrow + back link (standards §4.7). H1 stays "Create
   principal".
7. **Keep the blocking card** for the no-Work-Provider state verbatim in structure; copy
   becomes "No Work Provider organisation exists yet. Create one before creating a
   principal." with the existing "Go to Organisations" action.
8. **Hint styling**: help text uses a dedicated `field-hint` style, not the `empty-state`
   class (see review lens 3).
9. **Designed success state**: after creation, return to Principals with a status card
   "Principal ALPHA1 created for Organisation A." (pattern already used by
   `TempData["AdministrationStatus"]` elsewhere in Administration).

## Dependencies

- The "more organisations exist" flag must count **Work Provider** organisations beyond the
  page, not all organisations — today `HasMoreOrganizations` is computed before the
  role filter, so the note can show when nothing relevant is hidden. Needs a Core/query
  change or a filtered count.
- Conditional display of the Inspection mode hint (change 4) needs a few lines of
  progressive-disclosure script or an always-visible compromise; the plan accepts either,
  mockups show the hint in place.
- New navigation (Dashboard · Inbox · Upload · Queues · Cases · Administration) is the
  whole-application IA change owned by the standards file, not this page.

## Open questions

- Page size for the organisation select (mockups assume 50): confirm the real cap so the
  overflow sentence states the true number.
- Should Principal creation live as a slide-in on the Principals list rather than a
  separate page? The mockups keep the separate page; merging is an IA decision for the
  Principals folder (page 25).
