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
