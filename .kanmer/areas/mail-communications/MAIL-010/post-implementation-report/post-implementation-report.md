# Post-implementation report

**Branch:** `task/qdos26008-regressions` · **PR:** #505 · **Commit:** `7198c1c2`

## What was built

Three operator-facing sentences removed or trimmed from `Mail/Index.cshtml`. Nothing was
written to replace them.

| Line | Change |
| --- | --- |
| 137 | `No Deleted Items in the bounded approved scope matched "…"` → `No Deleted Items matched "…"` |
| 123 | `Enter a search term to read accepted Deleted Items within the selected approved mailbox scope.` — deleted |
| 115 | `Search includes retained messages in their current Outlook folders.` — deleted |

## How it was found, and why it had shipped

The Release 17 design check scanned `src/Pegasus.Web/Pages/**/*.cshtml` for every word on
the closed banned list, filtering out Razor comments and C# identifiers. One hit in
operator-visible text: `bounded`. Reading around it surfaced the other two.

The design authority says of that ban: *"This is a review rule, not an automated check —
nothing in CI enforces it today, and claiming otherwise would be the kind of false
assurance the evidence discipline above exists to prevent."* That is precisely why it
shipped in Release 16, and why the scan was run by hand here.

[[PR-053]] cleaned the categorised mail selector on the sibling page; the list page was
not covered by it.

## Two false positives cleared rather than reported

- The mailbox and folder `<nav>` elements at lines 19 and 35 look like pill rows, but they
  are navigation between mailbox and folder scopes with `aria-label`s, not table filters
  standing in for a dropdown. The view filter already uses a labelled `select`.
- The page's other `empty-state` paragraphs are search results ("no mail matched X"), not
  empty-state panels for sections with nothing recorded.

Reporting either would have been noise.

## Why nothing replaced the deleted sentences

The approved necessary-copy list is closed and operator-owned. Where a sentence was doing
real work — telling the operator the Deleted Items view needs a search term — the control
carries it: the field is `required` and the results area is simply empty until one is
entered, which is what "only populated, relevant sections render" asks for.

## Evidence

- `Pegasus.Web` builds clean
- Banned-word rescan over `Pages/**/*.cshtml` returns only Razor comments and C#
  identifiers
- Live: the Deleted Items search before, during and after a search — Phase 6
