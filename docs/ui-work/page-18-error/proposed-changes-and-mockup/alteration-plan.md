# Page 18 — Error — alteration plan

## Review summary

The error page states its one fact three times (h1 + red-tinted panel + "Request failed" h2),
promotes a 55-character trace identifier to hero content, and points at a nav item ("Return to
Operations") that the IA renames. Meanwhile the family is missing its sibling entirely: unknown
record URLs return raw browser 404s. The redesign is one calm card — plain apology, retry/return
actions, the identifier demoted to a small copyable "Support reference" — plus the designed
not-found page.

## Changes

1. **Structure**: eyebrow "PEGASUS" + h1 + lede + separate red-tinted "Request failed" card → one
   navless centered card (`role="alert"`), single h1 **"We could not complete that request"**. The
   kicker, the lede, and the duplicate h2 are removed.
2. **Body copy**: *"No changes should be assumed. Return to Operations and try the action
   again."* → *"What you submitted may not have been saved. Try again, and if it keeps failing,
   tell your administrator the reference below."*
3. **Actions**: single red "Return to Operations" → primary **"Try again"** (returns to the page
   the operator came from) + secondary **"Return to Dashboard"**. The advice and the affordance
   now match.
4. **Request ID demoted**: bold `Request ID:` + `<code>` hero string → small muted line at the
   card foot, labelled in business language: **"Support reference"**, monospace at 13px, with a
   **Copy** button. Full value kept (support needs the exact string); prominence removed.
5. **New: not-found page** (same family, navless card): h1 **"We could not find that page"**, one
   sentence — *"The link may be out of date, or the address may have been mistyped."* — and a
   secondary **"Return to Dashboard"**. No support reference (nothing failed; there is nothing to
   correlate). Served for unknown routes *and* unknown record URLs.
6. **Red usage**: full-width red-tinted panel → a 3px red left rule on the error card only; the
   not-found card carries no red at all (not a fault).

## Dependencies

- Status-code-pages middleware (re-execute) so 404s — route-level and unknown-record — render the
  not-found page; today only the exception handler routes to `/Error`. The oversized-upload raw
  HTTP 400 belongs to the Upload page's plan but should reuse this family's card.
- Home nav item rename to Dashboard (root standards document owns the IA).
- "Try again" needs a safe return target (referrer or a server-provided return URL — never a POST
  replay).
- Copy button requires a few lines of page JS (or falls back to select-on-click; both acceptable).

## Open questions

- Should "Try again" appear when there is no known referrer (deep-linked error)? Proposal: hide
  it and show only "Return to Dashboard".
- Do unknown record URLs deserve distinct wording ("We could not find that case") where the route
  knows the record type, or is the generic page enough? Generic recommended first; specialise only
  if support traffic shows confusion.
- Whether the Support reference should also appear in the page `<title>` for screen-reader and
  screenshot convenience.
