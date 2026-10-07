# Valuation: how it works

Read from the live source on 6 October 2026 (`origin/dev` f5bc6e5f1).

## What the section does not show

- The Engineer's Value is stored in two unconnected places. The box is a Case field. "Applied Engineer's Value" is a separate record of a calculation. Nothing on the page says they are different things.
- "Proposed Engineer's Value" is not stored at all. It is a preview, and script copies it into the box.
- A save does not redraw the section. A record written by that save, and the figure in the section head, stay as they were until the page is loaded again.
- A figure can be saved in the box with no calculation recorded, on purpose. The page then reads "None yet" beside it.
- While editing, a source is drawn as chosen that nobody chose.

## Governing documentation

| Document | What it settles for this section |
| --- | --- |
| [FRD-24](../../../../../../docs/frd/frd-24-engineer-findings-damage-valuation-and-settlement.md) | Valuation sources, the three values, the order of the calculation, Use this value, the preview and its failure wording, the Engineer's own value, "None yet" meaning only that no card is chosen |
| [FRD-16](../../../../../../docs/frd/frd-16-case-record-workspace.md) | One save for the Case, the source cards, the calculator with no Apply of its own, the same value increases in read and edit |
| [FRD-11](../../../../../../docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md) | The report reads the Engineer's Value; its commentary falls back to the recorded calculation's reason |
| [Design authority](../../../../../../docs/design/README.md) | One route per source, five cards, Get valuation only where a provider is connected, saves made as changes are made |
| [Case-workspace guardrails](../../../../../../.agents/skills/pegasus-ui-guardrails/references/case-workspace.md) | The section's ownership, the three boxes first (26 September 2026), no Apply, no Save on a card |

## Source by layer

| Layer | File | What it owns |
| --- | --- | --- |
| Web | `Pages/Cases/Shared/_CaseValuation.cshtml` | The whole section in both modes: head, three boxes, research row, cards, On the report, calculation, lines host, applied block |
| Web | `Pages/Cases/Shared/_CaseValuationLines.cshtml` | The lines: Guide retail, each adjustment, Proposed Engineer's Value; the refusal; "None yet" |
| Web | `Pages/Cases/Details.Valuation.cs` | `LatestAppliedValuation`, `DefaultBasis`, `DefaultCalculation`, `ChosenCalculation`, the `PreviewValuation`, `GetValuation` and `StartMarketResearch` handlers |
| Web | `Pages/Cases/Shared/_CaseCommitResult.cshtml` | What a save answers with: notices, ribbon, save form, the valuation's opening calculation, aside, dialogs. Not the Valuation section |
| Web | `wwwroot/js/case-workspace.js` (valuation block) | Choosing a source, Use this value, the preview request, filling the three boxes, Get valuation |
| Web | `wwwroot/css/case-workspace.css` (`#section-valuation`) | The cards grid and the two-column `.calc` grid |
| Core | `Assessment/ValuationCalculations.cs` | `ValuationCalculationPolicy.Calculate`, `IsEngineerValueBox`, the `AppliedValuation` record |
| Infrastructure | `Persistence/EfValuationStore.cs` | `AdoptAsync`, the only writer of the applied record; the applied list read |
| Core | `Reports/AssessmentReportProjection.cs` | The report reads the Case field, not the applied record |

## The behaviours

### The four places the figure appears

| On screen | What it is | Written by |
| --- | --- | --- |
| The Engineer's Value box | The Case field `assessment.values.engineer` | The Case save, like any field. Script also fills it from the preview |
| "Proposed Engineer's Value" | The preview's last line (`_CaseValuationLines.cshtml`) | Nothing: it is computed and shown |
| "Applied Engineer's Value" | `LatestAppliedValuation.AcceptedEngineerValue`, from `AppliedValuationSnapshots` | `EfValuationStore.AdoptAsync`, inside the Case save |
| The section head's "Engineer's Value" | The Case field as it was when the page was drawn (`DetailsModel.EngineerValue`) | Never updated by script |

### The preview fills the box

Every change to the calculator, the chosen source or the chosen source's figures posts to `PreviewValuation`. The answer is the lines partial. When it carries a proposal, script writes that figure into the Engineer's Value box. When it does not, the box returns to the figure it had when the page was drawn and Use this value is withdrawn. No preview runs when the page opens, so the box is filled only after a change. Origin: operator, 23 September 2026 (no Apply; the result fills the box).

### When a calculation is recorded

`ChosenCalculation` returns the calculation to record, or nothing. It returns one only when a basis is posted and has a retail, and either Use this value was pressed or the calculator differs from the one the page opened on. `AdoptAsync` then records it only when the posted Engineer's Value box is empty or equals the calculated figure (`IsEngineerValueBox`). Otherwise the save writes the box as a field and no record. Origin: operator, 23 and 28 September 2026.

### Why "Applied Engineer's Value" reads "None yet" beside a filled box

1. The figure was fetched or typed and the calculator was not touched: by the rule above nothing is recorded.
2. The figure was typed over the calculation: it is the Engineer's own, and nothing is recorded, with no message.
3. A calculation was recorded, but the save answered without the Valuation section, so the block still shows what it showed when the page was drawn.

A source whose retail was fetched in this edit has no Basis radio until the page is drawn again, so it cannot be posted as the basis except through Use this value.

### The record is a second copy with its own life

`AdoptAsync` writes the snapshot, an action-history entry, and a `CaseValuations` row with source Engineer's Value that no screen displays. It writes no Case field. A later typed change to the box does not change the record or mark it out of date, so the block can show one figure under a box holding another.

### Where the calculation sits

`.calc` is a grid of two columns placed after the cards. In source order its children are On the report, the calculation, and a block holding the lines and the applied record. The calculation is told to span two rows, so it takes the right column while the other two stack on the left. At 1180px and below the grid is one column.

### The default basis

While editing, the selected card is `DefaultBasis`: the latest recorded basis, else the first guide source with a retail. The lines show `DefaultCalculation` for it. So a card carries the chosen outline and a proposal is shown before anything has been chosen or recorded. While reading, only a recorded basis is outlined.

### Sources

Glass's, Brego, Super CAP, CAP and Cazana are each one card in both modes. A card's boxes belong to the Case form. Get valuation is offered only where a provider is connected and fills the card by script using the card's own guide month. A source with no provider shows its sentence from the start. AI market research is a row above the cards (Valuation month, a "Get valuation" label, the button), and a card only after research has run or while it is running. The row is not offered in the Inspection view.

### Who reads the figure afterwards

The report, the salvage matrix, repair spec scaling and Send to AI read the Case field. Report freshness and the report's commentary fallback read the applied record.

## Things the FRD does not settle

- Whether the section head and the recorded calculation should follow a save in place.
- Whether a recorded calculation should still be shown once the box holds a different figure.
- Whether a figure equal to the chosen source's calculation should be recorded against it when Use this value was not pressed.
- Where the calculation sits relative to the three values.
- Whether a source is drawn as chosen before the Engineer chooses one.
