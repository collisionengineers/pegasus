# Valuation: how it works

Read from the live source on 8 October 2026 (`origin/dev` acb2ffbd1). This is v33 design D as implemented by PR 1045.

## What the section does not show

- A click on a row chooses it as the basis but does not tell the save that the Engineer decided to use it. Only **Use this value** does that. A click alone records a calculation only if the calculator differs from the one the page opened on.
- A click on a row's labels or inside its boxes is ignored (`case-workspace.js`, `selectCard`: clicks on `input,button,select,label,a` are skipped), so only the gaps between them choose the row. A row with no retail ignores every click without saying why.
- A row typed during this edit has no basis radio, so a click on it does nothing. Only Use this value can choose it (`selection.GuideSource`).
- The Retail value and Trade value boxes are Case fields of their own. Choosing a row copies its figures into them, and either can then be overtyped, so the report can carry figures no source holds.
- While editing, the first source with a retail is drawn as chosen before anyone chooses it (`DefaultBasis`).
- Without script, a recorded calculation's result is not shown anywhere: the source word on the label is the only sign.

## Governing documentation

| Document | What it settles for this section |
| --- | --- |
| [FRD-24](../../../../../../docs/frd/frd-24-engineer-findings-damage-valuation-and-settlement.md) | Sources, the three values and that each is a report blocker, the order of the calculation, Use this value, what a save records, the preview, "None yet" |
| [FRD-16](../../../../../../docs/frd/frd-16-case-record-workspace.md) | One save for the Case, the guide cards, the calculator with no Apply of its own |
| [Design authority](../../../../../../docs/design/README.md) | One route per guide source, Get valuation only where a provider is connected, the unavailable sentence (23 September 2026) |
| [Case-workspace guardrails](../../../../../../.agents/skills/pegasus-ui-guardrails/references/case-workspace.md) | Design D: rows, the chosen row opens, the three boxes under it, AI market research's own row, On the report last |

## Source by layer

| Layer | File | What it owns |
| --- | --- | --- |
| Web | `Pages/Cases/Shared/_CaseValuation.cshtml` | The rows (five guide sources, AI market research, earlier research), Get valuation, Use this value, the notices, On the report |
| Web | `Pages/Cases/Shared/_CaseValuationCalculation.cshtml` | The calculation (previous total loss select, condition deduction, commercial VAT, value increases) and the Retail value, Trade value and Engineer's Value boxes |
| Web | `Pages/Cases/Shared/_CaseValuationOpening.cshtml` | The calculation the page opened on, carried forward after each save |
| Web | `Pages/Cases/Details.Valuation.cs` | `RecordedValuation`, `RecordedBasisWord`, `DefaultBasis`, `ValuationIncreaseRows`, `ChosenCalculation`, `GuideSourceConnected`, the `PreviewValuation`, `GetValuation` and `StartMarketResearch` handlers |
| Web | `wwwroot/js/case-workspace.js` (valuation block) | Row click and keyboard choice, Use this value, the preview request, filling the boxes, moving the calculation under the chosen row |
| Web | `wwwroot/css/case-workspace.css` (`#section-valuation`) | The row grid, the opened block, the increases grid |
| Core | `Assessment/ValuationCalculations.cs` | `ValuationCalculationPolicy.Calculate`, `IsEngineerValueBox`, `FormatMoney` |
| Core | `Assessment/GuideValuationProviders.cs` | `IsConnected`: Glass's is the only provider (`GlassGuideValuationProvider`) |
| Infrastructure | `Persistence/EfValuationStore.cs` | `AdoptAsync`, the writer of the recorded calculation |

## The behaviours

### The rows

Glass's, Brego, Super CAP, CAP and Cazana are one row each, in that order. Each row has Retail, Trade and Guide month: greyed boxes while reading, inputs of the Case form while editing. Glass's has Get valuation. The other four show the standing sentence "{Source} valuation is unavailable. Contact an administrator or report a problem." from the start (operator, 23 September 2026). Every row has Use this value while editing. AI market research has its own standing row with its figures, a Valuation month, its own Get valuation (not in the Inspection view), Researching while a job runs, and the month · mileage · date it was asked with. Each earlier research is a further row.

### The chosen row opens

The calculation, then the three boxes, stand under the chosen row. With nothing chosen they close the list. The script moves the block when another row is chosen (v33 design D, 6 October 2026).

### The calculation fills the box

Every change to the calculator, the chosen row or its figures asks `PreviewValuation`. The answer fills the Engineer's Value box, and the commercial VAT and previous total loss amounts stand in those cells' label lines. A refusal shows Core's sentence and puts the box back. There is no Apply (operator, 23 September 2026).

### What a save records

`ChosenCalculation` returns a calculation only when the basis has a retail, and either Use this value was pressed or the calculator differs from the one the page opened on. It is recorded only when the box is empty or holds the calculated figure (`IsEngineerValueBox`). A figure typed over it is the Engineer's own (operator, 6 October 2026, items E and L).

### The recorded source's word

While the box holds a recorded calculation's figure, its label carries that calculation's source as one `src-tag` word (`RecordedBasisWord`; v33 item D).

### On the report

Disclose guide source, Valuation commentary and Unrelated damage are tick boxes while editing. While reading they are summarised by `ReportContentSummary`: "Guide source not disclosed", then "valuation commentary" and "unrelated damage" where on.

## Things the FRD does not settle

- What the report's Retail value and Trade value should be when no source is chosen and the boxes are gone. Settled on 8 October 2026: blank, so the report stays blocked (item I).
- Whether a source with no provider must say so in a sentence or may say so in one word. Settled for this section on 8 October 2026: the sentence stays (item C).
