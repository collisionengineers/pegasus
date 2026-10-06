# v33 notes: the Valuation section

Built on 6 October 2026 from `origin/dev` f5bc6e5f1. These notes cover the offline proposals only and are not application evidence.

## 1. What changes, and where it shows

Shot numbers: `00` today and `01`–`05` designs A–E, editing as in the operator's screenshots (Glass's fetched, nothing recorded); `06` today and `07`–`11` A–E editing with a calculation recorded; `12` today and `13`–`17` A–E reading the same record. Those are captured at 1580, 1440 and 760. `18`–`22` the Engineer's own figure, `23`–`27` a calculation that cannot be worked out, `28`–`32` AI market research in progress and `33`–`37` nothing recorded are captured at 1580. Each shot is the Valuation section, whole, not the viewport.

| # | Live today | Proposal (all five designs) | Shots |
| --- | --- | --- | --- |
| 1 | The calculation is the right-hand column of a grid placed after the five source cards, so it sits at the far end of the section from the Engineer's Value box it fills | The calculation touches the box: directly under it (A, C), ending in it (B, E), or in one opened block with it (D) | 00, 01–05 |
| 2 | "Guide retail" and "Proposed Engineer's Value" in a box of their own. Script copies the proposal into the Engineer's Value box, so the figure is on the page twice | No second total. A, C and D show each adjustment's amount in its own label line; B and E keep the lines of the sum and make the Engineer's Value box its last line | 01–05, 07–11 |
| 3 | "Applied Engineer's Value" reads "None yet" beside a filled box, and when a calculation is recorded it shows the figure a third time | No block. A recorded calculation is shown only while the box holds its figure, as the source's word on the Engineer's Value label or as one line of Basis, Applied by and Adjustments (item D). It never reads "None yet" | 00, 06, 07–11, 13–17 |
| 4 | The section head's "Engineer's Value" and the Applied block are drawn once and not redrawn by a save, so they go stale until the page is reloaded | The head follows the box as it changes, and the record appears when the save lands (item C) | 00, 07–11 |
| 5 | "Valuation month", a loose "Get valuation" label and the AI market research button in a row above the cards; the AI card exists only once research has run | A keeps that row. B to E give AI market research its own standing place among the sources, with its figures, its month and its own Get valuation (item F). A strip switch shows either in every design | 01–05, 28–32 |
| 6 | On the report sits between the sources and the calculation | On the report closes the section (item G) | 01–05 |
| 7 | Five cards, each about 240px tall while editing; four are a notice and empty boxes | A and B keep the cards. C, D and E draw the same boxes and buttons as rows (item I) | 03–05 |
| 8 | The three values open the section (operator, 26 September 2026) | A and C keep them there. B makes them the worksheet's total line, D puts them inside the chosen source, E puts them in the right-hand pane (item H) | 01–05 |

Section height at 1580, in pixels, from `verification.json`:

| | Today | A | B | C | D | E |
| --- | --- | --- | --- | --- | --- | --- |
| Editing, as the screenshots | 975 | 994 | 1080 | 792 | 901 | 832 |
| Editing, calculation recorded | 1197 | 1033 | 1196 | 792 | 940 | 902 |
| Reading, calculation recorded | 925 | 722 | 900 | 668 | 737 | 815 |

## 2. Live rules the mockup mirrors

- **Arithmetic** (`ValuationCalculationPolicy.Calculate`): VAT is 20% of the guide retail, rounded; previous total loss is 10% or 20% of retail plus VAT, rounded; the proposal is retail plus VAT, less the previous total loss, plus the increases, less the condition deduction, rounded to whole pounds away from zero. A proposal below zero is refused with Core's own sentence. A VAT-registered claimant never has the commercial addition.
- **The box follows the calculation** (`case-workspace.js`, `preview`): every change to the calculator, the chosen source or the chosen source's figures fills the Engineer's Value box. Where the calculation cannot be worked out the box returns to the saved figure and Use this value is withdrawn (`invalidate`).
- **Typing makes it the Engineer's own** (`case-workspace.js`, the `isTrusted` input handler): a figure typed in the Engineer's Value box withdraws Use this value and is not touched again until the calculator changes.
- **Use this value** chooses the source, fills Retail value and Trade value from it, and marks the save. On a source with no retail it shows "Enter the retail value on this card to use it." A click on a source that has a retail chooses it without that mark.
- **What a save records** (`DetailsModel.ChosenCalculation`, `IsEngineerValueBox`): a calculation is recorded when Use this value was pressed or the calculator differs from the one the page opened on, and only when the box is empty or holds the calculated figure. Any other save records the box alone.
- **The calculator opens on the recorded selection** (`DetailsModel.ValuationIncreaseRows` and the applied calculation): reading shows the recorded adjustments; editing starts from them.
- **Default basis** (`DetailsModel.DefaultBasis`): while editing, the latest recorded basis, else the first source with a retail, is the calculator's source.
- **Sources**: Glass's, Brego, Super CAP, CAP and Cazana always present, same in read and edit; Get valuation only on a connected source (Glass's here); the standing sentence on an unconnected one (operator-approved wording, 23 September 2026); no mileage box; no Save on a source.
- **AI market research**: asked for a month, shown as Researching while the job runs with the filed-without-ending-your-edit note, not offered in the Inspection view.
- **Save as you go**: a change lands a moment after it is made; the ribbon reads Saving… then Saved; Done ends the edit.
- **Labels**: exact strings from `CaseWorkspaceLabels.Valuation`, `CaseWorkspaceLabels.Editors`, `CaseWorkspaceLabels.Report` and `OperatorLabels`. The self-check reads the label sources and fails on any other word.

## 3. Frame rules

- The Case frame is the captured page: 48px utility bar, 56px ribbon, 40px section row, 285px aside at 1441px and above, folded above the sections below that. The Valuation section is 1003px wide at 1580 and 1158px at 1440.
- Body text 13.5px; controls 36px, 30px inside a source row and 28px inside a source card as live; radius 3px.
- Read and edit share one geometry: every cell is a greyed box while reading and a white control while editing, in the same place. No cell exists in one mode only, except the buttons and the standing notice, which are edit-only as live.
- A value's origin is one `src-tag` word in its label line; nothing else marks provenance.
- No explanatory copy. Every visible word is a live string or a fixture value.
- C's columns and E's panes fold to one column at 1180px and below, where the live calculator already folds. Source rows fold their buttons to a second line at 1180px and to two columns at 760px.

## 4. Decisions taken and their authority

- No Apply button, no Save on a source, no second source-button row, one route per source: [case-workspace guardrails](../../../../.agents/skills/pegasus-ui-guardrails/references/case-workspace.md), operator 23 September 2026.
- The calculation's result fills the Engineer's Value box: same ruling. Every design keeps it and removes the second copy.
- "None yet" means only that no source is chosen: [FRD-24](../../../../docs/frd/frd-24-engineer-findings-damage-valuation-and-settlement.md). It is drawn in that one state.
- The unavailable sentence is drawn word for word: operator-approved wording, 23 September 2026.
- A redesign of this section was asked for by the operator on 6 October 2026, which is the authority the guardrails require for moving its parts.

## 5. Deliberate departures from live

- Nothing is posted. The runtime works the arithmetic itself and applies the recording rule in the page.
- Get valuation on Glass's answers fixed demo figures. AI market research goes to Researching and stays there.
- Report a problem and every link that leaves the page do nothing.
- The head figure follows the box as it is typed, a moment before the save. Live it would follow the save (item C).
- The record's time and name are fixed fixture values ("alex", 6 October 2026).
- Today's page is a still. The capture's fixture holds no saved values and runs a 2031 test clock, so the build writes the three boxes' figures, the head figure a freshly drawn page would show, this round's dates and a staff name into it. The operator's screenshot state keeps its "—" head, as on the screenshot. Nothing else in the capture is altered.
- On the report shows the summary for all three switches off; the summary wording for other combinations is not drawn.

## 6. Sign-off list

Each item is the operator's to settle. Nothing here is taken as decided.

- **A.** Which design: A, B, C, D or E. C is recommended. Confirm one, or name the alterations. *D chosen (operator, 6 October 2026: "Option D").*
- **B.** "Proposed Engineer's Value" and the "Applied Engineer's Value" block leave the page, and the Engineer's Value box is the one place the figure stands. Confirm, or name which one stays. *Taken as drawn with the choice of D; the operator was told so on 6 October 2026 and asked for no change.*
- **C.** The section head's figure and the recorded calculation follow each save without a reload. Confirm. (This is a defect today whatever the design: the save does not redraw either.) *Taken as drawn with the choice of D; the operator was told so on 6 October 2026 and asked for no change.*
- **D.** How a recorded calculation is shown: the source's word on the Engineer's Value label ("Glass's"), or one line of Basis, Applied by and Adjustments. The strip switches between them. Confirm one, or neither. The word is an existing source name used in a new place. *The source word (operator, 6 October 2026).*
- **E.** The record is shown only while the box holds its figure. Once the Engineer types a different figure, the earlier recorded calculation is no longer shown beside it; today it stays on screen under a different number. It remains in the Case's history. Confirm, or keep showing the last recorded calculation. *Hide the record (operator, 6 October 2026).*
- **F.** AI market research: its own standing place among the sources, holding its figures, the Valuation month and its own Get valuation, in place of the row above the sources; or today's row. The strip switches between them. Confirm one. *Its own row (operator, 6 October 2026).*
- **G.** On the report moves to the foot of the section. Confirm, or name its place. *Taken as drawn with the choice of D; the operator was told so on 6 October 2026 and asked for no change.*
- **H.** Designs B, D and E move the three values off the first row, where they were placed on 26 September 2026. Choosing one of them reverses that placement. Confirm with item A. *Follows from D: the three values stand under the chosen source.*
- **I.** Designs C, D and E draw the sources as rows, not cards: the same three boxes and the same buttons, with column heads Retail, Trade and Guide month and no head over the source names. Confirm with item A. *Follows from D.*
- **J.** A source with no connected provider keeps its standing sentence, as approved on 23 September 2026. Four such sentences are now the tallest thing in the section. Confirm they stay as drawn, or say how an unconnected source should read. No shorter form is drawn, because it would need wording only the operator can approve. *The sentence stays as drawn; the operator was told so on 6 October 2026 and asked for no change.*
- **K.** While editing, the first source with a retail is drawn as chosen and the calculation starts from it, before anyone has pressed Use this value. This is today's behaviour, kept in every design. Confirm, or nothing is chosen until Use this value or a click on a source. *Kept as today; the operator was told so on 6 October 2026 and asked for no change.*
- **L.** A figure that was fetched or typed, with Use this value not pressed and the calculator not touched, is saved as the Engineer's own and records no calculation. This is today's rule and the reason "Applied" read "None yet" on the screenshots. Confirm the rule stays, or say that a figure equal to the chosen source's calculation is recorded against it on any save. *Keep the rule (operator, 6 October 2026).*
- **M.** A typed figure that differs from the calculation is kept as the Engineer's own with no message. The designs show it by the absence of the source word or line. Confirm, or ask for something said. *Kept as today; the operator was told so on 6 October 2026 and asked for no change.*
- **N.** Design E heads its left pane "Basis", an existing label used as a heading. Confirm with item A if E is chosen. *Not applicable: E was not chosen.*
- **O.** This folder: kept as the record of the round, or removed in the Stage 2 pull request. *Open: kept in the Stage 2 pull request as historical reference until the operator says otherwise.*

## 7. Self-check

6 October 2026: `python check-valuation-designs.py` printed `RESULT {"fail": [], "okCount": 714}` with no script error on any page or state. It checks, for each design: all eleven states render; no "Proposed Engineer's Value" or "Applied Engineer's Value" anywhere; "None yet" only with no source chosen; one Engineer's Value cell; every control today's section has (fifteen kinds, counted); every visible word is in the application's label sources or the fixture; the figures move as the live script moves them (VAT, a typed figure, Use this value, a refused calculation, a source typed in this edit, Done and Edit); both strip choices each way; nothing spills sideways at 1580, 1440 and 760. Today's page is checked to still show the three things the round is about. 74 screenshots written. The record is `v33-valuation-shots/verification.json`.

## 8. Known limits

- Inter is inlined; a browser without it falls back to the system font.
- Today's page is a still and has six states, the ones captured.
- At 760px the row designs (C, D, E) hide their column heads; each box keeps its accessible name but has no visible label. Stage 2 would give each box its label at that width.
- The Inspection view is drawn as the same frame without the research controls; the Views card is not drawn.
- A colleague's lease, a locked section and the lazy-loaded section placeholder are not drawn.
- Value increases beyond two presets and two custom rows, and a custom increase with no label, are not drawn.
- Neither the screenshots nor the self-check are application evidence.

## 9. Decided, 6 October 2026

The operator chose design D ("Option D") and answered four questions the same day: the recorded calculation shows as the source's word on the Engineer's Value label (item D), AI market research has its own row (item F), a figure typed over a recorded calculation hides the record (item E), and the recording rule stays (item L). Items B, C, G, J, K and M were stated to the operator as taken as drawn or as today before the build, and no change was asked for. Items H and I follow from D. Item O stays open.

Stage 2 implemented D in the same pull request. Where the implementation differs from the mockup:

- **Research in progress.** The mockup's Researching row had no month box and no Get valuation. The live page lets research be asked for again while a job runs, and the implementation keeps that: the row keeps its Valuation month and Get valuation, and says "Researching · {month}" on its second line.
- **What a research card was asked with.** The live card states the month, mileage and date of a recorded research. The mockup dropped that line; the implementation keeps it as the row's second line, so the fact is not lost while the month box holds the next month to ask for.
- **Earlier research.** The live page shows every recorded research card. The implementation keeps each as a row beneath the standing one.
- **The section head** follows the save, as section 5 said it would, not each keystroke.
- **At 760px** each box in a source row shows its own label, which closes the known limit in section 8.
- **Without script** the three boxes are typed and a recorded calculation's result is not displayed anywhere, because the applied block was its only server-drawn copy. The amounts beside the controls are drawn.
- **Two approved messages still say "card"**: "Enter the retail value on this card to use it." and "The chosen card is no longer on the Case. Refresh the page." They were left word for word; changing them is the operator's call.

## 10. Stage 2 conformance, 6 October 2026

- **Tests.** `CaseValuationV26WebTests`, the class that owns the section's markup: 31 passed in a local Release build. It now pins the rows, the calculation and the three boxes under the chosen row, the absence of the second total and the applied block, the recorded source's word, a typed-over figure showing no record, and the saved state a commit answered in place carries. CI holds the verdict for the whole suite.
- **The implemented section in a browser.** `python check-valuation-implementation.py <captured pages>` printed `RESULT {"fail": [], "okCount": 50}`. It opens the Case pages the application rendered after Stage 2 with the application's own stylesheet and scripts, and checks: nothing spills sideways in seven states at 1580, 1440 and 760; the preview fills the Engineer's Value box and puts the commercial VAT and previous total loss amounts in their cells; a refused calculation clears them and restores the box; Use this value on a source typed in the same edit moves the calculation under that row and keeps its controls; a click on a row moves it back; a landed save's state changes the section head and shows the recorded source's word; typing over the figure takes the word away and typing it back returns it.
- **Side by side.** The 21 screenshots are in `v33-conformance/`. Against design D's mockup shots at 1580: `edit-fetched` beside `04-d-fetched`, 901px both; `edit-applied` beside `10-d-recorded`, 961px against 940px; `read-applied` beside `16-d-recorded-read`, 759px against 737px; `edit-pending` beside `31-d-pending`. The extra height is the research row's second line, the first departure listed in section 9.
- **What this is not.** The pages are opened from disk with requests stubbed, so no real save ran and the commit's redraw was driven by handing the script the saved state. The fixture holds an Engineer's Value and no Retail or Trade value, so those two boxes are empty in the screenshots, and its dates are the test clock's (2031). A signed-in walk of a running Pegasus, in both views and in Scroll and Tabs, has not been done.
