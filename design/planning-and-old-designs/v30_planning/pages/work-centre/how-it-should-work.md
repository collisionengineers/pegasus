# Work Centre: how it should work

Stage 1 proposals, **not yet decided**. The lettered items are in
[v30-notes.md](../../current/v30-notes.md#sign-off-list) (items Q to T).

1. "Updated HH:MM" moves into the header's action group, beside Refresh,
   as FRD-15 reads it; the metric strip loses the stray line above it and
   the panels below keep their own "Updated" metas.
2. The metric tiles stay label and figure. As a switch (`opt=metrics:toned`)
   each tile may carry a thin top bar in its state tone (amber for Not
   ready, Held and Unidentified; navy for Review and Triages); the text
   label stays the cue.
3. A Needs attention row's right column is a fixed-width, right-aligned
   pair: the due text, then owner and received, tabular figures.
4. Pane and panel heads put every meta in one right-aligned group, so the
   three heads on the page read the same.
5. The Today pane with nothing selected uses the shared `pane-empty`
   treatment ("Select an item", centred, muted) instead of a bordered line.

Open: Q (Updated placement), R (metric tone switch), S (row right column
and head metas), T (Today empty treatment).

## Three alternatives — 25 September 2026

The operator explicitly included appropriate functionality changes in the
design scope. The [comparison](../../current/pegasus_work_centre_designs_v30.html)
presents three proposals, not operator decisions:

1. A keeps a grouped queue and persistent Selected work pane side by side.
2. B uses a full-width table with inline details and tabs for its three
   work sections.
3. C uses three due-date lists with a detail drawer; group membership
   continues to come from due instants.
4. All three propose search within the complete scoped attention query,
   retain multi-select kinds and correct Office/Mine membership, and keep
   the five office-wide queue totals.
5. All three show validation, assignment conflict, stale/partial/unavailable
   reads and local action feedback, while preserving the current command
   permissions and accounting for the selected item after a change.
6. All three propose one Create Case action in the page header.

Open: WA–WF in the [proposal decisions](../../current/work-centre-design-proposals.md#review-decisions).
The layout choice remains open. Implementation follows selection and the
normal Stage 2 handoff, with changes to the relevant FRD/design owners.

## Decided — 25 September 2026: empty sections

Operator: "if there are no items in a section, e.g. no overdue items, that
section should simply be invisible/not shown".

1. Hide each empty Overdue, Due today and Later group, including its heading.
2. Hide empty New cases and AI jobs panels and their section tabs.
3. Hide Needs attention and selected-work panels when the complete attention
   dataset is empty; a wholly empty page has one brief overall message.
4. Retain active filter controls and Clear filters when the current filter
   produces no results.
5. Keep an unavailable section's failure notice visible; an unsuccessful
   read is not proof that a section has no items.
6. Reflow the remaining board lanes and supporting panels into the space.

This settles WG in the design proposals. FRD-15's current requirement for
per-group empty sentences is superseded for these mockups; its implementation
handoff must reflect the accepted omission rule.


## Decided — 25 September 2026: B, with WB, WD, WE and WF

Operator: design B (Office ledger), "a full end to end wiring this into the
active codebase, replacing the current pages function and design"; asked
and confirmed: Find, the section tabs, the compact strip and Create Case
once all ship.

1. The head reads "Updated HH:MM" beside Refresh and Create Case (Q lands
   here); the utility bar's New case is omitted on this page (WE).
2. The five queue totals are one compact strip, label and figure on one
   line (WF), in their own refresh section; a failed read draws no figure.
3. One panel carries the tabs Needs attention, New cases and AI jobs with
   counts (WD). A tab is omitted when its section is empty (WG); a section
   that could not be read keeps its tab with a dash and its notice. On Mine
   the Needs attention section stays even when empty, so Office is one click
   away. A wholly empty page reads "No work to show." The tab travels in
   `?tab=`.
4. Needs attention is a table: Next action (kind beneath), Record / detail
   (subject beneath), Owner, Due, Received. Group rows Overdue / Due today /
   Later appear only when the group has rows on the page.
5. Find in Needs attention (WB) is a term on the Core query, matched
   case-insensitively on reference, title, detail and owner before paging;
   chip counts stay over the scope; Clear filters clears kinds and term;
   no match reads "No work matches these filters."
6. Choosing a task opens the row in place beneath it: kind eyebrow, an
   Overdue or Due today chip, title, the six facts and the next action
   (Assign Engineer dialog, Review Case, Open Triage, the AI draft's action,
   Assign to me). Choosing it again closes it; nothing opens by itself.
7. AI jobs are compact rows with every fact and action FRD-27 names.
8. Refresh, F5 and the background refresh keep scope, kinds, term, tab,
   pages and the open row; the four sections (metrics, attention, new-cases,
   ai-jobs) are retained independently on a partial failure as before.

R, S and T fall away with the pane layout. Implemented in the Stage 2 PR
from this branch; FRD-15's Work Centre section owns the behaviour.
