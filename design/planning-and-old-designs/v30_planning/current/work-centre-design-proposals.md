# Three Work Centre designs

Temporary design review artifact, requested on 25 September 2026. Open the
[visual comparison](pegasus_work_centre_designs_v30.html) and explore the
three independent previews. These are design and interaction proposals;
they do not change application code or establish accepted requirements.

## The proposals

| Design | Composition and interaction | Best fit | Tradeoff |
| --- | --- | --- | --- |
| [A — Priority desk](pegasus_work_centre_a_v30.html) | Compact queue totals; grouped attention list beside persistent selected-work details; New cases and AI jobs below | Daily casework, moving between inspection and action while keeping the queue visible | The detail pane takes list width; supporting feeds need some scrolling |
| [B — Office ledger](pegasus_work_centre_b_v30.html) | Full-width attention table; a row expands to show facts and actions; tabs switch between Needs attention, New cases and AI jobs | Comparing references, owners and due dates across the office | Supporting feeds are one tab away; an expanded row interrupts the compact table |
| [C — Due-date board](pegasus_work_centre_c_v30.html) | Flat Overdue, Due today and Later lists side by side; selection opens a detail drawer | Understanding the distribution of the day's work at a glance | Long lanes need more scanning; the drawer covers part of the board while open |

**Recommendation: A.** It brings the largest usability improvement with a
familiar list-and-details relationship. B is preferable when comparing many
records is the main task. C is preferable when the distribution by due date
is the main thing to monitor.

These are three separate page designs, not display settings proposed for
the finished product. Selection should lead to one coherent implementation.

## Shared visual improvements

The existing shell, Pegasus mark, Inter font, palette and navigation order
remain the visual foundation. The page uses a compact, horizontal strip of
five queue totals, restrained borders, aligned metadata, natural-width
actions and a clearer distinction between queue overview and selected work.
Status has a text label as well as its colour.

| Geometry | Treatment |
| --- | --- |
| Shared shell | Existing 220px rail, 64px collapsed rail, 48px utility bar and 18px page padding |
| Page heading | 25px, with the existing Office-wide work eyebrow |
| Controls | Existing 36px controls and 32px secondary buttons; compact 28px filter controls |
| Corners | Existing 3–4px vocabulary |
| Queue totals | Five equal columns; label and figure on one line at desktop widths |
| A | Approximate 59/41 split; bounded scroll in the attention list; two supporting feeds below |
| B | Full-width table with fixed semantic columns and inline details |
| C | Three equal due-date lanes and a 480px detail drawer |
| Narrow windows | Existing shell reflow; A stacks details after its list, C stacks its lanes, B keeps every table column at 760px |

The selected-work pane, inline expansion and drawer all show the selected
record's own information. They no longer reuse the earlier mockup's fixed
Unassigned-item details for unrelated selections.

## Functionality proposals

### Find within Needs attention

A local search field narrows the current Office or Mine scope by reference,
vehicle or other subject detail, owner, Principal, task title and kind.
It combines with the existing multi-select kind filters. Clear filters
resets both; changing scope keeps the query. A no-match result says so and
offers Clear filters.

For implementation, apply this filter to the complete query **before
paging**, alongside the current scope and kind filters. Kind counts still
describe the whole scope before filters. The five metric counts remain
office-wide Cases queue totals. The preview searches its complete synthetic
dataset; it does not demonstrate a backend query implementation.

### Call the selection “Selected work”

The current “Today” pane can display an overdue or later item. A and C call
it **Selected work**; B places the facts beneath the selected row. Due-date
status retains its existing meaning and is shown explicitly.

### Keep work context while acting

Each selection shows its own kind, reference, facts, due text, owner and
permitted next action. A holds it beside the queue. B expands it in place.
C opens it in a keyboard-operable drawer, with Escape returning focus to
the row. Closing the drawer or expanded row makes no business change.

Assign Engineer opens the existing compact assignment interaction. Choosing
an Engineer or Assign to me updates only the synthetic item, shows a
confirmation, and reconciles the attention list. A previously Unassigned
Case now appears as Review; the Case's Review state has not changed.

The conflict preset preserves the work and shows the existing refusal
message. The preview does not emulate the server's edit lease, version
check or operation-key mechanisms; those remain implementation obligations.

### Give the feeds a deliberate place

A and C place New cases and AI jobs in two supporting panels. B gives each
its own tab, including a count or unavailable indicator, and keeps the
attention scope, filters and selected row when switching tabs. Counts and
freshness are specific to the section they describe.

The compact AI job rows retain job kind, instruction, record, started by,
creation time, state, lease expiry or failure reason, and the supported
actions. **Complete job** works on the sample Draft ready Query response;
it removes that job and its corresponding attention item. The estimate's
action remains **Review estimate**.

### One Create Case action on this page

The Work Centre header retains **Create Case**. These previews omit the
duplicate **New case** action in the utility bar while on this page. This
is a proposed page-specific shell change, requiring explicit selection;
the remaining rail and utility controls keep their current placement.

### Make refresh outcomes clear

Refresh and F5 demonstrate a short busy state and recovery to current data.
They retain scope, kind filters, search, selection and the New cases
divider. Refresh is deferred while a dialog is open. A stale read keeps
last-good values and time visible. An unavailable initial attention read
shows its notice and omits the five unavailable counts. Partial failure
keeps the two current sections available and identifies the unavailable
AI jobs section.

These freshness rules implement existing FRD intent in the mockups. The
automatic five-minute and focus-return refresh triggers are not simulated.

### Hide empty sections — decided 25 September 2026

At the operator's instruction, an empty Overdue, Due today or Later group
is omitted. A and B remove its heading and rows; C removes the lane and
uses the available width for the remaining lanes. No zero-count group or
“Nothing overdue” placeholder is drawn.

Empty New cases and AI jobs panels are omitted, including their tabs in B.
If the complete attention dataset is empty, that section and its selected
work pane are also omitted. An entirely empty page retains its header,
five zero-valued office queue totals and the single line “No work to show.”

Filters remain available when they produce no matching work, with Clear
filters and a short no-results message. Unavailable is a distinct read
outcome: its section and failure notice remain visible. The `quiet` preset
demonstrates no Overdue work and empty supporting feeds while Due today
and Later still have items.

## Current rules retained

Fetched `origin/dev` on 25 September 2026; its head remains `32dabfc59`.
Read the Work Centre Razor body, PageModel, presentation labels, Core
`NeedsAttentionPolicy`, shared shell, CSS and refresh script against
[FRD-15](../../../../docs/frd/frd-15-work-centre-queues-and-search.md#work-centre),
[FRD-12](../../../../docs/frd/frd-12-operator-experience.md) and
[FRD-27](../../../../docs/frd/frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#ai-job-list).

| Live control or fact | Coverage in every alternative |
| --- | --- |
| Page title, Office-wide work, update time, Refresh, Create Case | Present; fresh/stale/partial/unavailable are distinct |
| Not ready, Review, Held, Unidentified, Triages | Same order and exact `/Cases?tab=` destinations; office totals do not follow Mine |
| Office / Mine | Working; Mine includes owned work and eligible unowned Unassigned/Triage items |
| Seven kind chips | Working multi-select; counts are over the whole scope before filtering |
| Clear kind filters | Expanded to Clear filters for the combined search and kinds |
| Overdue, Due today, Later | Same due-group membership and order; empty groups omitted under the operator's new instruction |
| Attention row | Title, kind, reference, detail, due words, owner and received age retained |
| Paging | “Page 1 of 1 · earliest due first”; all ten fixture rows are present. Production remains 50 per page |
| Selected-item facts | Kind-specific fields, reference, owner, due and received retained in the detail view |
| Next action | Same action per kind; destinations explicitly open a mockup boundary |
| Assign Engineer | Facts, required select, Assign to me, Cancel and Assign; validation and conflict states |
| Assign to me | Only on takeable unowned Unassigned or Triage work, for all enabled staff roles |
| New cases | Last seven calendar days; no Triage rows; reference, vehicle, claimant, Principal, origin and time |
| Automation changes | Separate Changed by automation label and named change |
| Since you last looked | Divider retained through filtering and refresh |
| AI jobs | Unfinished and recent failed jobs; Market research excluded; required facts and supported actions retained |
| Failed external work | Remains on Operations; no retry or service-health work is moved into Needs attention |

The specimens include invented Cases, a `t.` Triage reference and an
Unidentified `U` reference. The old mockup's “Mine” fixture incorrectly
included unowned Held and Unidentified work; these new fixtures follow
`NeedsAttentionPolicy.IsMine` and `CanTake`.

## Review decisions

**WA.** Confirm A, B or C as the preferred Work Centre layout, or specify
the pieces to combine. The chosen version would replace the current page's
presentation. A is recommended; no selection is recorded.

**WB.** Confirm Find within Needs attention, or keep scope and kinds as
the only filters. Acceptance adds a full-query filter to FRD-15 and its
owning query/caller; the existing 50-row page size remains.

**WC.** Confirm “Selected work” in place of “Today”, or retain the current
heading. This is page copy, not a new work category.

**WD.** Confirm the selected design's placement of New cases and AI jobs,
including compact job rows; for B this includes the three page-section
tabs. Acceptance updates FRD-15 and the design authority's page contract.

**WE.** Confirm that Create Case appears once, in the Work Centre header,
or retain the utility-bar duplicate. Acceptance updates the page-specific
shell rule in the design authority.

**WF.** Confirm the compact five-count strip, or keep the existing taller
tiles. Their query definitions and exact queue destinations stay the same.

**WG. Settled, 25 September 2026.** Hide empty groups and sections. This
follows the operator's direct instruction; it needs no further approval.
The detail is recorded above and in the page's how-it-should-work document.

Existing v30 items Q–T remain historical open proposals. This pass develops
them into full alternatives; it does not record their acceptance.

## How to try them

Open the [comparison page](pegasus_work_centre_designs_v30.html). Each
design has a collapsed **Mockup controls** strip at the bottom left; it is
review tooling, outside the proposed product UI.

1. Switch to Mine, combine Held and Review, then clear the filters.
2. Search for `BH17RZV` and select Assign Engineer.
3. Try an empty assignment, Cancel, Assign to me, and assigning R. Khan.
4. Turn on Demonstrate assignment conflict, then repeat an assignment.
5. Select Unidentified and Triage items to compare their facts and actions.
6. In AI jobs, use Complete job on the query-response example.
7. Select a stale, partial or unavailable preset, then Refresh.
8. In B, use the three section tabs; in C, close the drawer with Escape.

Every state is also available via `?state=`: `default`, `mine`, `filtered`,
`empty`, `stale`, `partial`, `unavailable`, `assign`, `conflict`,
`new-cases`, `ai-jobs`, `quiet`. `&embed=1` hides the review strip. `&role=User`
or `&role=Engineer` checks staff actions without Administration in the rail.
`&selected=r3` selects a fixture explicitly.

## Evidence and reproduction

The [self-check](v30-work-centre-selfcheck.html) exercises all three designs,
twelve states, and 1580×1000, 1440×900 and 760×1000 layouts, followed by
filtering, correct Mine membership, per-item details, assignment, validation,
conflict, refresh retention, AI-job completion and role coverage.
The [runner](check-work-centre-designs.py) adds actual keyboard traversal,
dialog focus containment, Escape/focus return and B's tab navigation.

See the [evidence record](v30-work-centre-shots/verification.json) and
[screenshot inventory](../pages/work-centre/README.md#three-designs--25-september-2026).

```powershell
node design/planning-and-old-designs/v30_planning/current/build-work-centre-designs.mjs
python design/planning-and-old-designs/v30_planning/current/check-work-centre-designs.py
node design/planning-and-old-designs/v30_planning/current/build-work-centre-designs.mjs
```

The final build embeds the captured thumbnails in the comparison page.
All three surface files contain their styles, script, font and marks and
open offline. The earlier Work Centre baseline remains alongside them.

## Implementation handoff and limits

After selection, update the owning design/FRD clauses and implement one
page layout, using the current PageModel, Core queries, assignment handlers
and refresh behaviour. The query-wide search is the only proposed new
query capability. Layout, selection and feed placement are presentation
changes; they do not require new services or business-policy owners.

This evidence covers the local mockups only. It does not validate database
queries, real account assignment, Case leases, concurrency, idempotency,
server authorization, actual job completion or network failure recovery.
Each fixture list fits on one page, so multi-page data retrieval is not
simulated. Linked destinations identify where they go and stop at an
explicit mockup boundary; the Case workspace, queues and creation form are
outside this design task. The existing shell's broader dialogs are retained
for context, not claimed as new application evidence.

In-memory mutations reset on reload. Only the versioned Office/Mine and
rail-collapse preferences use local browser storage. These temporary
review artifacts are retained or removed under the operator's instruction
when the accepted result is implemented.
