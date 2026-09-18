Read from the live source on 18 September 2026.

## What this page does not show

- No case's full record — every row's action opens the Case, Triage,
  Unidentified item or AI draft elsewhere; the Today pane is a fact summary,
  not an editable form.
- No count of everything in the office's Cases queues. The four metric tiles
  are only Not ready, Review, Held and Unidentified — not With Engineer,
  Completed, Query, Triage or Awaiting instruction, even though several of
  those feed the Needs attention list's kinds.
- No history of completed or dismissed work items; a row leaves the Needs
  attention list the moment its underlying state no longer qualifies, with
  no "recently actioned" trail on this page.
- No indication, in the New cases list, of *why* a row is new beyond its
  arrival channel chip and, for a change, the change kind — no diff of what
  changed.

## Source table

| File | What it owns |
| --- | --- |
| `Pages/Index.cshtml.cs` (`IndexModel`) | Reads the Needs attention snapshot, New cases feed and AI jobs list; owns scope/kind/paging state, the Assign Engineer and Assign-to-me handlers, and the AI job "Complete job" handler. |
| `Pages/_WorkCentreBody.cshtml` | The rendered markup: the four metrics, the Needs attention list and its Today pane, the Assign Engineer dialog, New cases and AI jobs panels. Both the full page and the `OnGetRefreshAsync` partial render this one view. |
| `Presentation/NeedsAttentionPresentation.cs` | Row title/detail text, the kind chip order and slugs, the Today pane's six-fact layout, and the next-action label per kind. |
| `Presentation/OperatorLabels.cs` (`WorkCentre` class) | Every static word on the page: section headings, empty states, the relative due/received text, the arrival chip words and tones. |

## Behaviours found

### The three regions load, and can fail, independently

`LoadAsync` reads Needs attention (+ metrics), New cases and AI jobs behind
three separate `try/catch` blocks (`Index.cshtml.cs:387-430`). Any one can set
its own `*Unavailable` flag and render its own "Unavailable" notice while the
other two still render live data — there is no single "Work Centre is down"
state. `RefreshOutcome` (`current` / `partial` / `failed`) is derived purely
from how many of the three failed, and only "failed" (all three) suppresses
`RefreshOutcomeLabel`'s claim of freshness.

### The four metric tiles are a fixed subset of the full workflow

The metric strip is hard-coded to Not ready, Review, Held and Unidentified
(`_WorkCentreBody.cshtml:97-112`) — not the full set of Cases-index tabs. With
Engineer, Completed, Query, Triage and Awaiting instruction have no metric
tile here even though With Engineer, Review, Held, Unidentified and Triage
all still surface as Needs attention kinds lower on the page.

### Default selection and default scope

The Today pane's selected row is the query string's `selected` id if it
still exists in the list, else simply `items[0]` — the first row in the
whole ordered (Overdue, then Today, then Later) list, not necessarily
anything to do with due-day priority within a kind
(`Index.cshtml.cs:461-463`). An Engineer's default scope is `Mine`; every
other role opens on `Office` (`DefaultScope`, `Index.cshtml.cs:358-360`).

### The Assign Engineer dialog only exists for one kind

`Assignment` is only populated, and the dialog only rendered, when the
selected row's kind is `UnassignedEngineer` (`ReadAttentionAsync`,
`Index.cshtml.cs:470-474`). Selecting any other kind's row while the dialog
was open from an earlier `UnassignedEngineer` selection does not carry the
dialog forward — `OpenAssignment` requires both `assign=true` on the request
and a non-null `Assignment`.

### AI jobs shown here are a narrow, kind-filtered slice

`ReadAiJobsAsync` (`Index.cshtml.cs:522-555`) takes the open ledger's
Queued/Taken/DraftReady jobs, unions in only the Failed jobs from the last 7
days (the same window as New cases), excludes every Market research job
outright, and de-duplicates by job id. Completed, Cancelled and Expired jobs
never appear here regardless of age. "Complete job" only exists
(`WorkCentreAiJobRow.CompletesByHand`) for a Draft ready Query response or
Unidentified-queue pass — an Estimate's Draft ready state is closed through
the Case's own action, not a button here.

### The New cases divider is a carried instant, not "since last page load"

`DividerUtc` is the feed's own `LastSeenUtc` on first load, but a script
refresh (`refresh=true`) instead carries forward whatever `since` value the
previous render sent, so the "Since you last looked" line does not move
merely because the page auto-refreshed (`Index.cshtml.cs:410-413`) — it only
advances when the person actually re-opens or navigates to the page fresh.

## Things the FRD does not settle

- Nothing in the read code states why the metric strip carries four of the
  nine workflow states specifically (Not ready, Review, Held, Unidentified)
  rather than, say, also With Engineer or Query — both of which have their
  own Needs attention kinds one section down. The choice reads as a
  deliberate design decision, but no comment or FRD reference ties it to a
  named requirement.
