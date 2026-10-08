# Valuation: how it should work

Decided with the operator on 8 October 2026: from the two screenshots, four answers before drawing, the click feedback and the settled list. The lettered items are in [v34-notes.md](../../../current/v34-notes.md).

## The rules

- **D1.** The section takes the screenshots' shape: a grid of guide cards, a Value increases list, a deductions row, an Engineer's Value panel and the report switches. The screenshots' narration is not drawn.
- **D2.** There is no Apply. Choosing a card or changing the calculation fills the Engineer's Value box at once, as since 23 September 2026.
- **D3.** Get valuation stays on each connected card: Glass's today, any other card once its provider is connected.
- **D4.** The Retail value and Trade value boxes leave the section. The report's Retail and Trade are the chosen card's figures, and only the Engineer's Value box remains. With no card chosen they are blank, and the report stays blocked until a card is chosen. A Retail or Trade figure that no card holds cannot be typed (item I).
- **D5.** AI market research is a card in the grid with its Valuation month, its own Get valuation, Researching while a job runs, and each earlier research as a card of its own.
- **D6.** The guide sources and AI market research are cards: three to a row at 1181px and above, two at 1180px and below, one at 760px and below. The order is Glass's, Brego, Super CAP, CAP, Cazana, AI market research, then earlier research (item A).
- **D7.** Each card shows Retail, Trade and Guide month: text while reading, inputs while editing in the same place (item D). The AI card's figures are never typed.
- **D8.** The chosen card has the red border and tint and the word "Selected" in its head (item B).
- **D9.** A click anywhere on a card with a retail chooses it, as do Enter and Space. That includes its name, labels, figures, boxes and padding; only the card's own buttons and links do something else. The choice is the Engineer's decision to use the card, and the save then records the calculation against it. The Use this value button leaves (item E). A card typed in this edit can be chosen the same way. A box clicked to choose its card keeps its focus and caret.
- **D9a.** A click on a guide card with no retail shows "Enter the retail value on this card to use it." on that card. Typing a retail removes it (item E).
- **D10.** A source with no connected provider keeps the standing sentence of 23 September 2026 under its figures, with its "report a problem" link (item C).
- **D11.** Value increases is one list in two columns: each active preset with its figure, then Add 20 % VAT with its amount, then two Other… rows (item F). A VAT-registered claimant's VAT row is disabled with its tag. Reading lists only what was applied, each with a tick.
- **D12.** Condition deduction comes first, then a Previous total loss tick box with a −10 % / −20 % switch that is live only while ticked. Ticking starts at −10 %, and the amount stands in the label line (item G).
- **D13.** The Engineer's Value panel holds the one box, labelled "Engineer's Value" (item J), with the recorded source's word as today. Beside it is "Calculation · from {source} retail", or "None yet" when no card is chosen, or Core's refusal.
- **D14.** On the report closes the section as one line: the three tick boxes while editing, the summary while reading. There is no composed preview of the report line (item H).

## Where this lands

| Page | Entry |
| --- | --- |
| Case record, Valuation section | The card grid, the increases list, the deductions row, the Engineer's Value panel |
| Case record, save answer | Unchanged: the head figure and the recorded word follow a save |
| Report | Retail and Trade come from the chosen card through the same Case fields. They are blank, and block the report, until a card is chosen |
| Decisions, Repair Spec | Unchanged: they read the same Case field |

## Documentation impact when the FRD is written

- FRD-16, Valuation:
  - cards in place of rows;
  - a click anywhere on a card is the decision to use it, and Use this value is gone;
  - the Retail and Trade boxes are gone.
- FRD-24:
  - the "Use this value" paragraph becomes the card click;
  - "Retail value, Trade value and Engineer's Value are three boxes" becomes one box plus the chosen card's figures;
  - Retail and Trade remain report blockers until a card is chosen.
- `docs/design/README.md`, Valuation: the card grid and the "Selected" word.
- `.agents/skills/pegasus-ui-guardrails/references/case-workspace.md`, Valuation: replace the design D description.
- `case-workspace.js`, `selectCard`: clicks on labels and inside boxes stop being skipped.
- Tests that pin today's markup: `CaseValuationV26WebTests`, `CaseValuationWebTests`, `CaseWorkspaceScriptContractTests`.

## Decided 8 October 2026

- A and E: the operator's request, not open decisions.
- B "Selected"; C the approved sentence; D keep Guide month; F, G, I and J confirmed; H no composed line; K keep this folder.
- No open lines remain.
