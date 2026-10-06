# Valuation: how it should work

Nothing here is decided yet. The operator's brief of 6 October 2026 named three problems; the rules below are proposals, each open until Stage 1 sign-off. The lettered items are in [v33-notes.md](../../../current/v33-notes.md).

## What the operator has already settled

- **D1.** The calculation is too far from the Engineer's Value box, the "Guide retail / Proposed Engineer's Value" box is a separate copy, and "Applied Engineer's Value" appears always empty; the redesign must answer all three (operator, 6 October 2026).
- **D2.** Earlier rulings stand unless an item below reverses one: no Apply on the calculator and no Save on a source (23 September 2026); the three values open the section (26 September 2026); Use this value is the one visible decision to use a source's figure (28 September 2026); changes save as they are made (29 September 2026).

## Proposed rules

- **D3.** The Engineer's Value box is the only place the figure stands in the section; there is no "Proposed Engineer's Value" total and no "Applied Engineer's Value" block. *Open: item B.*
- **D4.** The calculation is drawn against the box it fills, in the place the chosen design gives it. *Open: items A and H.*
- **D5.** The section head's figure and the recorded calculation are redrawn by each save. *Open: item C.*
- **D6.** A recorded calculation is shown only while the box holds its figure, and never as "None yet". *Open: items D and E.*
- **D7.** AI market research has one standing place among the sources. *Open: item F.*
- **D8.** On the report closes the section. *Open: item G.*
- **D9.** The sources are drawn as the chosen design draws them, with the same boxes, buttons and notice. *Open: items I and J.*
- **D10.** What a save records is unchanged. *Open: items K, L and M.*

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
