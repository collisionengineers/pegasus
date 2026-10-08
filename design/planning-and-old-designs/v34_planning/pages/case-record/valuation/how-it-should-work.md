# Valuation: how it should work

Proposed on 8 October 2026 from the operator's two screenshots and four answers given that day. The lettered items are in [v34-notes.md](../../../current/v34-notes.md). Nothing here is decided until the operator signs the list.

## What the operator has already settled

- **D1.** The section takes the screenshots' shape: a grid of guide cards, a Value increases list, a deductions row, an Engineer's Value panel and the report switches. The screenshots' narration is not drawn (operator, 8 October 2026).
- **D2.** There is no Apply. Choosing a card or changing the calculation fills the Engineer's Value box at once, as since 23 September 2026 (operator, 8 October 2026).
- **D3.** Get valuation stays on each connected card: Glass's today, any other card once its provider is connected (operator, 8 October 2026).
- **D4.** The Retail value and Trade value boxes leave the section. The report's Retail and Trade are the chosen card's figures, and only the Engineer's Value box remains (operator, 8 October 2026).
- **D5.** AI market research is a card in the grid with its Valuation month, its own Get valuation, Researching while a job runs, and each earlier research as a card of its own (operator, 8 October 2026).

## Proposed rules

- **D6.** The guide sources and AI market research are cards, three to a row at 1181px and above, two to 1180px and one at 760px and below. The order is Glass's, Brego, Super CAP, CAP, Cazana, AI market research, then earlier research (item A).
- **D7.** Each card shows Retail, Trade and Guide month: text while reading, inputs while editing in the same place (item D). The AI card's figures are never typed.
- **D8.** The chosen card has the red border and tint and one word in its head: "Selected" or "Basis" (item B).
- **D9.** A click anywhere on a card with a retail chooses it, as do Enter and Space. That includes its name, labels, figures, boxes and padding (operator, 8 October 2026). Only the card's own buttons and links do something else. The choice is the Engineer's decision to use the card, and the save then records the calculation against it. The Use this value button leaves (item E). A card typed in this edit can be chosen the same way. A box clicked to choose its card keeps its focus and caret.
- **D9a.** A click on a guide card with no retail shows "Enter the retail value on this card to use it." on that card; typing a retail removes it (item E).
- **D10.** A source with no connected provider shows either the word "Manual" beside its name or the standing sentence of 23 September 2026 (item C).
- **D11.** Value increases is one list in two columns: each active preset with its figure, then Add 20 % VAT with its amount, then two Other… rows. A VAT-registered claimant's VAT row is disabled with its tag (item F). Reading lists only what was applied, each with a tick.
- **D12.** Condition deduction, then a Previous total loss tick box with a −10 % / −20 % choice that is live only while ticked. Ticking starts at −10 %, and the amount stands in the label line (item G).
- **D13.** The Engineer's Value panel holds the one box, labelled "Engineer's Value" (item J), with the recorded source's word as today. Beside it is "Calculation · from {source} retail", "None yet" when no card is chosen, or Core's refusal.
- **D14.** On the report closes the section as one line: the three tick boxes while editing, the summary while reading. There is no composed preview of the report line (item H).
- Open: what the report's Retail and Trade are when no card is chosen. Today they are typed (item I).

## Where this lands

| Page | Entry |
| --- | --- |
| Case record, Valuation section | The card grid, the increases list, the deductions row, the Engineer's Value panel |
| Case record, save answer | Unchanged: the head figure and the recorded word follow a save |
| Report | Retail and Trade read from the chosen card's figures through the same Case fields |
| Decisions, Repair Spec | Unchanged: they read the same Case field |

## Documentation impact when the FRD is written

- FRD-16, Valuation: cards in place of rows. A click is the decision to use, and Use this value is gone. The Retail and Trade boxes are gone.
- FRD-24: the "Use this value" paragraph becomes the card click. "Retail value, Trade value and Engineer's Value are three boxes" becomes one box, plus the chosen card's figures. Their report-blocker rule needs the item I answer.
- `docs/design/README.md`, Valuation: the card grid, and the "Manual" word if item C takes it.
- `.agents/skills/pegasus-ui-guardrails/references/case-workspace.md`, Valuation: replace the design D description.
- Tests that pin today's markup: `CaseValuationV26WebTests`, `CaseValuationWebTests`, `CaseWorkspaceScriptContractTests`.
