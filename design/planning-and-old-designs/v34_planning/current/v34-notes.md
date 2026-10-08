# v34 notes: the Valuation section as guide cards

Built on 8 October 2026 from `origin/dev` acb2ffbd1. These notes cover the offline proposal only and are not application evidence.

## 1. What changes, and where it shows

Shot numbers refer to `v34-shots/` (listed on the [page README](../pages/case-record/valuation/README.md)). Today's section is design D as implemented. Its screenshots are in [v33-conformance](../../v33_planning/current/v33-conformance/verification.json) (`edit-fetched`, `edit-applied`, `read-applied`, `edit-pending`).

| # | Today (design D) | Proposal | Shots |
| --- | --- | --- | --- |
| 1 | Sources are full-width rows under column heads | Sources are cards, three to a row, each with Retail, Trade and Guide month (items A, D) | 01–04 |
| 2 | The chosen row is outlined in navy; the calculation and three boxes open under it | The chosen card has the red border and tint and one word ("Selected" or "Basis", item B). The calculation stays in one place below the cards | 01, 03, 12 |
| 3 | A click chooses a row, but not on a label or inside a box, and a row with no retail ignores it silently; **Use this value** marks the decision for the save | A click anywhere on a card is the decision; a card with no retail says why; the button leaves (item E) | 01, 03 |
| 4 | Brego, Super CAP, CAP and Cazana each carry the standing unavailable sentence | Each carries the word "Manual" beside its name, or the sentence (item C) | 01, 13 |
| 5 | Commercial VAT is a cell of its own beside the deduction | Add 20 % VAT is a row of the Value increases list with its amount (item F) | 01, 07 |
| 6 | Previous total loss is a None / −10 % / −20 % select | A tick box and a −10 % / −20 % switch, live only while ticked (item G) | 01, 03 |
| 7 | Retail value, Trade value and Engineer's Value are three boxes | One Engineer's Value box in its own panel. Retail and Trade are the chosen card's (operator, 8 October 2026; item I) | 01–04 |
| 8 | On the report is a collapsible sub-panel | One line: three tick boxes while editing, the summary while reading | 01, 02 |

Section height at 1580, in pixels, from `verification.json`. Today's figures are from v33's conformance record.

| | Today | v34 |
| --- | --- | --- |
| Editing, nothing recorded (`fetched`) | 901 | 816 |
| Editing, calculation recorded | 961 | 966 |
| Reading, calculation recorded | 759 | 849 |

The proposal is taller while reading because the cards keep their three lines where today's rows keep one. The fixtures differ as well: v34 has four guides with figures and an earlier research card, where v33 had one guide.

## 2. Live rules the mockup mirrors

- **Arithmetic** (`ValuationCalculationPolicy.Calculate`):
  - VAT is 20 % of the retail, rounded.
  - Previous total loss is 10 % or 20 % of retail plus VAT, rounded.
  - The figure is retail plus VAT, less previous total loss, plus increases, less the condition deduction, rounded to whole pounds away from zero.
  - Below zero is refused with Core's sentence.
  - A VAT-registered claimant never has VAT.
- **The box follows the calculation** (`case-workspace.js`, `preview`): every change fills the Engineer's Value box. A refusal puts back the saved figure. There is no Apply (23 September 2026).
- **What a save records** (`DetailsModel.ChosenCalculation`, `IsEngineerValueBox`): a calculation is recorded when the Engineer chose the source or the calculator changed, and only when the box is empty or holds the calculated figure. In this mockup a card click is the choice (item E). Live, only Use this value is.
- **The recorded source's word** (`RecordedBasisWord`): shown on the Engineer's Value label while the box holds the recorded figure (v33 item D).
- **Default basis** (`DefaultBasis`): while editing, the recorded basis, else a source with a retail, is chosen.
- **Get valuation** only where `GuideSourceConnected`: Glass's. AI market research has its own, not in the Inspection view. It shows Researching with the filed note while a job runs, and the month · mileage · date of each recorded research.
- **Report summary**: `ReportContentSummary`, word for word.
- **Labels**: exact strings from `CaseWorkspaceLabels.Valuation`, `CaseWorkspaceLabels.Report`, `OperatorLabels` and Core's refusal. The self-check reads those sources and fails on any other word, except the two strip-switched words.

## 3. Frame rules

- **The frame** is the captured Case page: 48px utility bar, 56px ribbon, 40px section row, 285px aside at 1441px and above.
- **Body text and controls:** body text 13.5px. Controls inside a card are 28px, in the deductions row 36px, and the Engineer's Value box 40px. Radius 3px and 4px, as live.
- **Read and edit share one geometry:** every figure is text while reading and an input while editing, in the same cell. Get valuation and the tick boxes exist only while editing, as live.
- **Columns:** cards three to a row, two at 1180px and below, one at 760px and below. The increases list is two columns, one at 760px and below.
- **Origin of a value:** one `src-tag` word, as live: AI, the recorded source, the claimant VAT warning, and "Manual" if item C takes it.
- **No explanatory copy.** None of the screenshots' narration is drawn (discussion log).

## 4. Decisions taken and their authority

- No Apply; the box fills as anything changes: operator, 23 September 2026, confirmed 8 October 2026.
- Get valuation on each connected card only: operator, 8 October 2026. Design authority: one route per source, no Get valuation without a provider.
- Retail and Trade follow the chosen card; one Engineer's Value box: operator, 8 October 2026.
- AI market research is a card that keeps its lookup: operator, 8 October 2026.
- The redesign itself: operator, 8 October 2026, the authority the guardrails require to move the section's parts.

## 5. Deliberate departures from live

- Nothing is posted. The runtime works the arithmetic itself and applies the recording rule in the page.
- Get valuation on Glass's answers fixed demo figures after a short "Looking up…". AI market research goes to Researching and stays there; its previous result becomes an earlier card.
- Report a problem and every link that leaves the page do nothing.
- The head figure follows each save a moment after a change, as live.
- The value-increase presets (Tow bar, Decals, Camper conversion, PCO plated, Driving tuition) are fixture data from the operator's screenshot. Live, they come from Administration › Valuation presets.

## 6. Sign-off list

Each item is the operator's to settle. Nothing here is taken as decided.

- **A.** The sources become cards in a grid (shots 01–04), which reverses v33 item I (rows). The calculation no longer opens under the chosen source; it has one fixed place below the cards. Confirm, or keep rows.
- **B.** The chosen card's word: "Selected", from your screenshot (a new word), or "Basis", the live label. The strip switches between them (shot 12). Confirm one.
- **C.** A source with no connected provider: the word "Manual" beside its name (your screenshot), or the standing sentence approved on 23 September 2026 (shot 13). "Manual" drops the card's "report a problem" link. Confirm one.
- **D.** Each card keeps its Guide month as a third line. Your screenshot omits it, but it is recorded with each card and the report can use it. Confirm, or drop it.
- **E.** A click on a card is the Engineer's decision to use it, so the save records the calculation against it. The **Use this value** button leaves. This changes the rule kept on 6 October 2026 (v33 item L), where a click alone chose a source without marking the save. A card typed in this edit can then be chosen by a click too; today it cannot.
  - The whole card is the target: its name, labels, figures, boxes and padding (operator, 8 October 2026: "parts of the box are randomly not clickable"). Only Get valuation and the report-a-problem link do something else.
  - A guide card with no retail cannot be chosen. A click on it shows the existing approved sentence "Enter the retail value on this card to use it." on the card, and typing a retail removes it.
  - Confirm, or keep Use this value on each card.
- **F.** Add 20 % VAT moves into the Value increases list, with its amount where an increase's figure stands (shots 01, 07). Confirm, or keep it beside the deduction.
- **G.** Previous total loss becomes a tick box with a −10 % / −20 % switch; ticking starts at −10 %. Same data as today's select. Confirm.
- **H.** No composed "what the report carries" line is drawn: its wording would be new, and only you can approve it. On the report reads as today. Confirm, or supply the wording for a report preview line.
- **I.** Retail and Trade follow the chosen card (your answer, 8 October 2026). Today each is a typed Case field and a report blocker until entered. Open:
  - With no card chosen, should the report's Retail and Trade be blank (a blocker), or should they be typed somewhere?
  - A figure typed directly into the report's Retail or Trade (today's overtype) is no longer possible. Confirm.
- **J.** The panel's label stays the live "Engineer's Value", not "Engineer's value (PAV)" (CONTEXT.md already says the PAV is the Engineer's Value). Confirm, or ask for "(PAV)".
- **K.** This folder: kept as the record of the round, or removed in the Stage 2 pull request.

## 7. Self-check

8 October 2026, after the first feedback round: `python check-valuation-v34.py` printed `RESULT {"fail": [], "okCount": 671}`, with no script or console error. (The first build printed 656 before the whole-card checks were added.) It was run with `PEGASUS_CHROME` pointing at Playwright's installed Chromium 1234, because the Python driver expected build 1223. What it checks:
- All eleven states render under both strip choices.
- None of the screenshots' narration appears, and there is no Apply and no Use this value.
- There is one Engineer's Value cell, and no Retail or Trade boxes.
- Get valuation appears only on Glass's and the research card, and not on the research card in the Inspection view.
- Exactly one chosen card carries its word.
- There are four "Manual" words or four sentences.
- "None yet" appears only with no card chosen.
- Every control kind today's section has is counted, except the two deliberate drops.
- Every visible word is from the application's label sources or the fixtures.
- A click on a card's name, label, guide-month line, box, padding or research meta chooses it. The box keeps its focus and caret. A card with no retail says why, and Get valuation does not choose its card.
- The figures move as the live arithmetic moves them: a card click, VAT, −10 % and −20 %, an increase, a deduction, the head after a save, a typed figure losing the source word, Get valuation, research, a refusal, a typed card, Done.
- Nothing spills sideways at 1580, 1440 and 760.

22 screenshots were written. The record is `v34-shots/verification.json`.

## 8. Known limits

- The frame was captured for v33 at `origin/dev` f5bc6e5f1, two days before this round. Sections other than Valuation are collapsed heads. The Repair Spec head shows an unstyled file input because the capture's scripts are removed.
- Inter is inlined; a browser without it falls back to the system font.
- At 760px the deductions panel keeps an empty label line above the Previous total loss tick box.
- A colleague's lease, a locked section and the lazy-loaded section placeholder are not drawn.
- More than five presets, and a custom increase with no label, are not drawn.
- Neither the screenshots nor the self-check are application evidence.
