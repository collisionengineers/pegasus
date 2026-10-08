# How the Case aside should work

## Decided 8 October 2026

The operator chose design 8 and settled the items as recorded in the [notes](../../../current/v35-notes.md), section 12.

- D1. Next action holds one step: the first outstanding Case requirement while the Case is Not ready or Held, else the state's own step, else the report's first blocker in page order.
- D2. A requirement or blocker as the step is drawn in full (what is missing, its source and reason, what clears it) with one full-width secondary control to where it is cleared.
- D3. A step whose control says what the step says (Assign Engineer, Create audit) is that control alone. Any other step names itself above its section's control.
- D4. Report not ready is a card of its own below Next action, folded until the browser opens it and remembered per browser.
- D5. The card lists everything outstanding, the step's item included: the Case requirements, then every report blocker under its section's name, in page order.
- D6. Each item's requirement is its link. Source and reason share one line, followed by what clears it. An amber dot marks each item; there is no amber fill.
- D7. Original report missing links to Files; the other Case requirements link to Case details.

Documentation impact, landed with the code: FRD-16 (the aside), FRD-13 (readiness and Review), the design README (the aside paragraph and its classes), and the Case-workspace guardrail.

## Before the decision

Not yet decided with the operator. Designs 1 to 12 and items A to U are in the [notes](../../../current/v35-notes.md), sections 6, 9, 10 and 11.

Open: which design (1 grouped by section, 2 one line each, 3 its own card, 4 one row per section, 5 first item in full, 6 follows the page; with one step in Next action: 7 dialog, 8 closed card, 9 on the page, 10 in Report, 11 checklist of sections, 12 bar across the page).

Open: items A to U.
