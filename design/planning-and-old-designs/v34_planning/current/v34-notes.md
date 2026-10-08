# v34 notes: the Valuation section as guide cards

Built on 8 October 2026 from `origin/dev` acb2ffbd1. These notes cover the offline proposal only and are not application evidence.

## 1. What changes, and where it shows

Shot numbers refer to `v34-shots/` (listed on the [page README](../pages/case-record/valuation/README.md)). Today's section is design D as implemented. Its screenshots are in [v33-conformance](../../v33_planning/current/v33-conformance/verification.json) (`edit-fetched`, `edit-applied`, `read-applied`, `edit-pending`).

| # | Today (design D) | Proposal | Shots |
| --- | --- | --- | --- |
| 1 | Sources are full-width rows under column heads | Sources are cards, three to a row, each with Retail, Trade and Guide month (items A, D) | 01–04 |
| 2 | The chosen row is outlined in navy; the calculation and three boxes open under it | The chosen card has the red border and tint and the word "Selected" (item B). The calculation stays in one place below the cards | 01, 03 |
| 3 | A click chooses a row, but not on a label or inside a box, and a row with no retail ignores it silently; **Use this value** marks the decision for the save | A click anywhere on a card is the decision; a card with no retail says why; the button leaves (item E) | 01, 03 |
| 4 | Brego, Super CAP, CAP and Cazana each carry the standing unavailable sentence | Unchanged: each card carries the sentence (item C) | 01 |
| 5 | Commercial VAT is a cell of its own beside the deduction | Add 20 % VAT is a row of the Value increases list with its amount (item F) | 01, 07 |
| 6 | Previous total loss is a None / −10 % / −20 % select | A tick box and a −10 % / −20 % switch, live only while ticked (item G) | 01, 03 |
| 7 | Retail value, Trade value and Engineer's Value are three boxes | One Engineer's Value box in its own panel. Retail and Trade are the chosen card's (operator, 8 October 2026; item I) | 01–04 |
| 8 | On the report is a collapsible sub-panel | One line: three tick boxes while editing, the summary while reading | 01, 02 |

Section height at 1580, in pixels, from `verification.json`. Today's figures are from v33's conformance record.

| | Today | v34 |
| --- | --- | --- |
| Editing, nothing recorded (`fetched`) | 901 | 896 |
| Editing, calculation recorded | 961 | 1045 |
| Reading, calculation recorded | 759 | 849 |

The proposal is taller because the cards keep their three lines where today's rows keep one, and each unconnected card holds its sentence under its figures. The fixtures differ as well: v34 has four guides with figures and an earlier research card, where v33 had one guide.

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
- **Labels**: exact strings from `CaseWorkspaceLabels.Valuation`, `CaseWorkspaceLabels.Report`, `OperatorLabels` and Core's refusal. The self-check reads those sources and fails on any other word, except "Selected" (item B).

## 3. Frame rules

- **The frame** is the captured Case page: 48px utility bar, 56px ribbon, 40px section row, 285px aside at 1441px and above.
- **Body text and controls:** body text 13.5px. Controls inside a card are 28px, in the deductions row 36px, and the Engineer's Value box 40px. Radius 3px and 4px, as live.
- **Read and edit share one geometry:** every figure is text while reading and an input while editing, in the same cell. Get valuation and the tick boxes exist only while editing, as live.
- **Columns:** cards three to a row, two at 1180px and below, one at 760px and below. The increases list is two columns, one at 760px and below.
- **Origin of a value:** one `src-tag` word, as live: AI, the recorded source and the claimant VAT warning.
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

Settled by the operator on 8 October 2026. A and E were never open: they are what the operator asked for, and listing them was this round's mistake. They keep their letters so the record reads straight.

- **A.** The sources become cards in a grid, and the calculation has one fixed place below them. *Not a decision: the operator's request ("this is the task").*
- **B.** The chosen card's word. *"Selected".*
- **C.** A source with no connected provider. *The standing sentence approved on 23 September 2026, with its "report a problem" link; no "Manual" tag.*
- **D.** Each card keeps its Guide month as a third line. *Keep.*
- **E.** A click anywhere on a card is the Engineer's decision to use it; the Use this value button leaves. A guide card with no retail says "Enter the retail value on this card to use it." *Not a decision: the operator's request ("this is literally what i asked for").*
- **F.** Add 20 % VAT is a row of the Value increases list, with its amount. *Confirmed.*
- **G.** Previous total loss is a tick box with a −10 % / −20 % switch; ticking starts at −10 %. *Confirmed.*
- **H.** No composed "what the report carries" line. *Stays out.*
- **I.** Retail and Trade follow the chosen card. *Confirmed. With no card chosen they are blank, and the report stays blocked until a card is chosen; a Retail or Trade figure no card holds cannot be typed.*
- **J.** The label stays "Engineer's Value", not "Engineer's value (PAV)". *Confirmed.*
- **K.** This folder. *Kept as the record of the round.*

## 7. Self-check

8 October 2026, after the operator settled the list: `python check-valuation-v34.py` printed `RESULT {"fail": [], "okCount": 366}`, with no script or console error. (Earlier runs printed 656 and 671 while every state was drawn twice, once per strip choice; the strip choices were removed when B and C were settled.) It was run with `PEGASUS_CHROME` pointing at Playwright's installed Chromium 1234, because the Python driver expected build 1223. What it checks:
- All eleven states render.
- None of the screenshots' narration appears, and there is no Apply and no Use this value.
- There is one Engineer's Value cell, and no Retail or Trade boxes.
- Get valuation appears only on Glass's and the research card, and not on the research card in the Inspection view.
- Exactly one chosen card says "Selected".
- The four unconnected cards carry the approved sentence, and no "Manual" tag appears.
- "None yet" appears only with no card chosen.
- Every control kind today's section has is counted, except the two deliberate drops.
- Every visible word is from the application's label sources or the fixtures.
- A click on a card's name, label, guide-month line, box, padding or research meta chooses it. The box keeps its focus and caret. A card with no retail says why, and Get valuation does not choose its card.
- The figures move as the live arithmetic moves them: a card click, VAT, −10 % and −20 %, an increase, a deduction, the head after a save, a typed figure losing the source word, Get valuation, research, a refusal, a typed card, Done.
- Nothing spills sideways at 1580, 1440 and 760.

20 screenshots were written. The record is `v34-shots/verification.json`.

## 8. Known limits

- The frame was captured for v33 at `origin/dev` f5bc6e5f1, two days before this round. Sections other than Valuation are collapsed heads. The Repair Spec head shows an unstyled file input because the capture's scripts are removed.
- Inter is inlined; a browser without it falls back to the system font.
- At 760px the deductions panel keeps an empty label line above the Previous total loss tick box.
- A colleague's lease, a locked section and the lazy-loaded section placeholder are not drawn.
- More than five presets, and a custom increase with no label, are not drawn.
- Neither the screenshots nor the self-check are application evidence.

## 9. Decided, 8 October 2026

The operator settled the list the same day (section 6). The strip's two switches were removed and the mockup now draws "Selected" and the approved sentence. The operator gave the go-ahead for Stage 2 the same day; section 10 records it.

## 10. Stage 2, 8 October 2026

Implemented on `task/valuation-cards-v34` against D1–D14.

- **Core.** The report's Retail and Trade join a new set, `AssessmentVocabulary.GuideCardDerivedPaths`. The one field gate, `AssessmentPolicy.NormalizeWritableField`, refuses them on every route: the Case save's free fields, the assessment save and `pegasus_assessment_update`. `ValuationPolicy.ReportValues` turns a card's figures into the two field values.
- **Infrastructure.** A Case save that carries a chosen calculation writes Retail and Trade from the basis card, as that save leaves it (`EfValuationStore.ReportValuesAsync`). This happens before the fields are written, so it holds whether or not the calculation itself is recorded. `pegasus_valuation_save` goes through the same save.
- **Web.**
  - `_CaseValuation.cshtml` draws the cards.
  - `_CaseValuationCalculation.cshtml` draws the increases (with Add 20 % VAT), the deductions (Previous total loss as a tick box plus two radios of the field, on the Scroll / Tabs switch's look) and the Engineer's Value panel.
  - The valuation block of `case-workspace.js` makes the whole card the target. A click switches on `selection.Use`, and choosing updates the cards in place.
  - `case-workspace.css` replaces the row rules with the card rules from `lib/valuation-v34.css`, using existing tokens. The chosen card's tint is the selected-metric tint already in `site.css`.
  - The Use this value labels are gone and `Selected` is added.
- **Docs.** FRD-16, FRD-24, FRD-10, ADR-0064, the design authority, and the case-workspace guardrails and their skill.

Where the implementation differs from the mockup:

- **The chosen card on opening.** As today, while editing the recorded basis, or else the first recorded card with a retail, is drawn Selected before anyone clicks (`DefaultBasis`). Retail and Trade are written only once a card is clicked or the calculation changes, so a Case nobody has chosen on keeps them blank. A click on that already-Selected card is the decision too.
- **No card chosen.** Retail and Trade keep whatever they last held. On a new Case that is blank, as item I settled.
- **Reading.** "On the report" keeps the live summary sentence, as drawn.

**Conformance.**
- `python check-valuation-implementation.py <captured pages>` printed `RESULT {"fail": [], "okCount": 66}`. It opens the Case pages the application rendered (seven states, made by the temporary test kept as `captured/capture-implementation-test.cs.txt`, never committed to the test project) with the application's own stylesheet and scripts.
- What it checks:
  - nothing spills sideways at 1580, 1440 and 760;
  - there is no Use this value and no Retail or Trade box;
  - a click on a label, a box or the padding chooses the card and marks the decision, and a clicked box keeps its focus;
  - a card with no retail says why, and typing a retail answers it;
  - a card typed in this edit is chosen by its source, and Enter works;
  - VAT, −10 % and −20 % fill the box with their amounts, and unticking clears the percentage;
  - a refusal restores the saved figure;
  - a landed save moves the head and shows the source word, and typing over withdraws the decision;
  - a recorded calculation opens with its tick box, percentage and amounts.
- The screenshots are in `v34-conformance/`, beside the mockup's `v34-shots/` of the same state (`edit-fetched` and 01, `read-fetched` and 02, `edit-applied` and 03, `read-applied` and 04, `edit-pending` and 08, `edit-empty` and 10, `read-empty` and 11). The capture fixture runs a 2031 test clock, so guide months read May 2031.
- **Tests.** Core, integration and Web tests were updated; CI holds the verdict. Locally only the temporary capture test ran, which built every project.
- **What this is not.** The pages were opened from disk with requests stubbed. No real save ran, and no signed-in walk of a running Pegasus has been done.
