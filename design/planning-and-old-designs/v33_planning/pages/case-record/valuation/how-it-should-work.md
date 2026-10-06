# Valuation: how it should work

Decided with the operator on 6 October 2026: design D, and the answers recorded under "Decided" below. The lettered items are in [v33-notes.md](../../../current/v33-notes.md).

## What the operator has already settled

- **D1.** The calculation is too far from the Engineer's Value box, the "Guide retail / Proposed Engineer's Value" box is a separate copy, and "Applied Engineer's Value" appears always empty; the redesign must answer all three (operator, 6 October 2026).
- **D2.** Earlier rulings stand unless an item below reverses one: no Apply on the calculator and no Save on a source (23 September 2026); the three values open the section (26 September 2026); Use this value is the one visible decision to use a source's figure (28 September 2026); changes save as they are made (29 September 2026).

## Proposed rules

- **D3.** The Engineer's Value box is the only place the figure stands in the section; there is no "Proposed Engineer's Value" total and no "Applied Engineer's Value" block.
- **D4.** The sources are rows. The chosen one opens: the calculation, then the three values, stand under its row. With no source chosen that block closes the list.
- **D5.** The section head's figure and the recorded source's word are redrawn by each save.
- **D6.** While the Engineer's Value holds a recorded calculation's figure, its label carries that calculation's source as one word, the calculator opens on that calculation and that source opens while reading. A different figure saved over it shows none of that, and the page never reads "None yet" beside a figure.
- **D7.** AI market research has its own standing row with its figures, its Valuation month and its own Get valuation.
- **D8.** On the report closes the section.
- **D9.** A source row holds the same three boxes, the same two buttons and the same notice a card held.
- **D10.** What a save records is unchanged.

## Where this lands

| Page | Entry |
| --- | --- |
| Case record, Valuation section | The chosen layout; the head and the record following a save |
| Case record, save answer | Carries what the section needs to redraw its head and record |
| Report, Decisions, Repair Spec | Unchanged: they read the same Case field |

## Documentation impact when the FRD is written

- FRD-16, the Valuation paragraphs: the layout, the removal of the lines box and the applied block, the record's place, AI market research's place.
- FRD-24: the preview paragraph (what is shown in place of the lines), and "None yet".
- `docs/design/README.md`, Valuation: cards or rows, the calculation's place.
- `.agents/skills/pegasus-ui-guardrails/references/case-workspace.md`, Valuation: the same, and the three values' place if item H reverses it.
- Tests that pin today's markup: `CaseValuationV26WebTests`, `CaseValuationWebTests`, `CaseWorkspaceScriptContractTests`.

## Decided 6 October 2026

- Design D ("Option D").
- The recorded calculation shows as the source's word on the Engineer's Value label (item D).
- AI market research has its own row (item F).
- A figure typed over a recorded calculation hides the record (item E).
- The recording rule stays (item L).
- Items B, C, G, J, K and M: taken as drawn or as today; the operator was told so and asked for no change.
- Open: whether this planning folder is kept or removed (item O).
