# Page 9 — Vehicle images list — alteration plan

> Vocabulary note: the legacy term is written `in·take` where current identifiers must be named.

## Review summary

The current screen is an orphaned list (no navigation entry, ~4 clicks deep) titled with a
banned internal term, led by a doctrine paragraph, dominated by a red full-width Search button,
and rendered with unlabelled filter pills and sentence-length row states. It cannot answer the
operator's real question — "what image material is waiting?" — because nobody can find it, and
once found it makes the waiting state a bold headline instead of a scannable chip.

## Changes

1. **Title and identity**: "Image in·takes" → **"Vehicle images"** (H1, `<title>`, breadcrumb).
   Records are "image references"; the visible key stays the existing `AB12CDE-01` format under
   the label **Image reference**.
2. **Reachability**: orphaned URL → reached as an **Inbox filter chip ("Vehicle images")** and a
   direct route (`/VehicleImages`); the legacy route 301-redirects. The screen sits under the
   **Inbox** section of the new nav (Dashboard · Inbox · Upload · Queues · Cases ·
   Administration).
3. **Heading stack**: eyebrow + H1 + lede → H1 only. The lede ("Pre-Case records for image-only
   material… association keeps both identities permanently.") is deleted, not reworded; the one
   consequence that matters (permanence of the reference) moves to the linking action on the
   detail page, next to the control it concerns.
4. **Search**: label "Image In·take Reference or vehicle registration" → **"Registration or
   image reference"**; single inline field with a secondary (not red) Search button on the same
   row. Red is withdrawn from this page entirely — it has no commitment action.
5. **Filters**: "All / Awaiting instruction / Associated with Case" pill links → chips **All ·
   Awaiting instruction · Linked to a case**, each carrying a live count, with a visible selected
   state and `aria-current`.
6. **Rows**: two stacked strong/small pairs → a four-column row: **Image reference ·
   Registration · Received · state chip**. The whole row remains the link to the detail page.
7. **State copy**: "Image in·take registered" sentence → chip **"Awaiting instruction"** (amber,
   pending semantics); associated records → chip **"Linked to Case 26001"** (neutral) naming the
   case reference, and matching the filter's wording exactly. One label map serves list, chips,
   and detail.
8. **Empty states**: "No Image in·takes match this view." → two designed states in business
   language: no records at all — **"No vehicle images are waiting."**; no matches — **"No
   vehicle images match this search."** with a one-click "Clear search" action.
9. **Ordering**: rows explicitly ordered newest-received first; the Received column header says
   so.

## Dependencies

- Nav rework (new IA) in the shared layout — owned by the whole-application IA change, not this
  page alone.
- Inbox page must render the "Vehicle images" filter chip with a count → needs a count query for
  unlinked image records (does not exist today).
- The list row model must carry the linked case *reference* (today the list computes only a
  state sentence; the case reference is available on the detail model only).
- A shared state-label map for image-reference states, reused by page 10.
- Legacy route redirect kept for bookmarks.

## Open questions

- Do linked records stay in the default ("All") list indefinitely, or drop out after the linked
  case passes report delivery? Operator input needed.
- Is a top-level route enough, or does the operator want Vehicle images as its own nav item?
  Current proposal follows the standards file: Inbox chip + direct route, no seventh nav item.
- Should the Received column show time as well as date? Rows today show both; the mockups keep
  date only and rely on the detail page for the timestamp.
