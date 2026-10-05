# Work Centre: how it should work

Decided with the operator on 5 October 2026 (the brief and the two answers); the rest is open until Stage 1 sign-off. The lettered items are in [v32-notes.md](../../current/v32-notes.md).

## Rules the operator has already settled

- **D1.** Issue 1017 closes through the redesign, not an interim fix (operator, 5 October 2026).
- **D2.** The activity figures to restore are New cases today, Sent to Engineer today and this week, Reports sent today and this week, Completed this week and E-mails received today (operator, 5 October 2026). Queries received and per-person figures are not drawn.
- **D3.** A dismissal keeps the 2 October 2026 rulings: global, record-level, no undo, no dismissed list, no notice, metrics unchanged.

## Proposed rules, one per design or shared

- **D4.** Every row of every list ends in the same always-visible Dismiss control, named "Dismiss {reference}", and the open row does not repeat it. *Open: item B (icon-only X, or a text button).*
- **D5.** After a dismissal, focus lands on the next row of the same list, or on that list's heading when none is left. *Open: part of item B.*
- **D6.** The activity figures are their own section with their own refresh state; a failed read shows the section's unavailable notice and no figure. *Open: items C to I (figures, windows, sources, scope, placement).*
- **D7.** One "Updated HH:MM" in the header; a section that failed says so in its own notice. *Open: item J.*
- **D8.** A Taken job reads "Taken until HH:MM". *Open: item K.*
- **D9.** The chosen design's structure (tabs kept, a rail, one list, or lanes) and any control that moves from its 25 September 2026 place. *Open: items A and L.*

## Where this lands

| Page | Entry |
| --- | --- |
| Work Centre (`/`) | The chosen layout, the Dismiss control, the Activity section |
| Administration → Reports | Unchanged; Reports sent on the Work Centre should agree with MI-01 for the same week (item F) |
| Cases | Unchanged; the five metrics keep their links |

## Documentation impact when the FRD is written

- FRD-15 "Work Centre": the Metrics paragraph, a new Activity paragraph (figures, windows, sources), the Sections paragraph if the tabs go, The open row, New cases, AI jobs and Dismiss (the control's place and the focus rule). The stale "`New cases today` … separate from Sent to Engineer and Reports sent" paragraph is replaced by the restored definitions.
- `docs/capabilities.md` UI-04 and `docs/open-decisions.md` UI-04 close.
- `docs/design/README.md`: the Work Centre reflow line and the component map.
- CONTEXT.md: "First sent to Engineer", "New cases today" and the metric names re-confirmed.
