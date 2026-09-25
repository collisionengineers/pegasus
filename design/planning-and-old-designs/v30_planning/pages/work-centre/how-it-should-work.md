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
