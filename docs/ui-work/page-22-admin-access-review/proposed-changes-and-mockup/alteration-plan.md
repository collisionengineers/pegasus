# Alteration plan — Access review (page 22)

## Review summary

Structurally the cleanest Administration page, with three real defects under the chrome: a
`0001-01-01 00:00:00Z` minimum-value timestamp rendered as a completed review, times shown as
raw UTC sorting strings under a "(UTC)" header while the rest of the app renders London time,
and a single idempotency key shared across every row's form (the sibling Roles page correctly
issues one per row). The chip copy narrates ("Due — no review recorded") and the per-row reason
input has no guidance.

## Changes

1. **Navigation and orientation.** New global nav; breadcrumb `Administration / Access review`
   replaces eyebrow + back-link. H1 "Access review". Table caption becomes screen-reader-only.
2. **Time rendering.** Old header **"Last reviewed (UTC)"**, values `2026-07-14 09:42:00Z` →
   new header **"Last reviewed"**, values in London time in the app's standard format:
   **"14 Jul 2026 09:42"** (`<time datetime>` keeps the ISO instant). Uses the shared
   Europe/London conversion extracted from `_FreshnessBanner.cshtml`.
3. **Minimum-value defect.** A default/minimum timestamp never renders as a date: any value
   before a sanity floor renders as **"Not yet reviewed"** with the **"Due"** chip. (Root cause
   belongs in the page model — a recorded review must carry a real instant.)
4. **Chip copy.** Old: "Due — no review recorded" / "Recorded" → New: **"Due"** (amber) /
   **"Reviewed"** (green). The date column carries the detail.
5. **Column merge.** "Last reviewed" and "Review state" stay separate columns (chip is
   scannable; date is the evidence) but the chip column narrows and loses its header
   redundancy: header becomes **"Review"**.
6. **Reason ergonomics.** Per-row Reason input keeps `required maxlength="1000"` with a
   screen-reader label "Reason for {username}"; one shared hint under the table: **"Reasons are
   kept on the administration record."**
7. **Idempotency.** Each row form gets its own operation key (adopt the Roles page pattern);
   page-model change, no markup difference.
8. **Handler binding.** Move `asp-page-handler="Review"` from the button to the form element,
   matching sibling pages.
9. **Row action weight.** "Record reviewed" stays the row's primary action but at compact
   size; Reason input and button sit on one line so rows stay near 40px.

## Dependencies

- Shared London-time formatter (extract from `_FreshnessBanner.cshtml`; shared with page 20).
- `.hint` class and breadcrumb pattern shared across Administration pages.
- Change 3's real fix is upstream of the view (page model / data): the view-side floor is a
  guard, not the cure.
- Per-row operation keys: page-model change to `IndexModel` (mirror `Roles/IndexModel`).

## Open questions

- Is there a policy review cadence (e.g. quarterly)? If so, the page should show *when* each
  account fell due (e.g. "Due since 1 Jul 2026") and sort due rows first; the current model has
  no due date to render. Needs an operator statement before any cadence is implied in copy.
- Should disabled accounts appear here at all? Reviewing access for an account with no access
  is arguably noise; screenshot data never shows the case. Operator call.
