# v34 discussion log

How this mockup came to be. It is not design authority.

## 8 October 2026: the brief

The operator supplied two screenshots of a valuation layout and wrote:

> Examine the setup for this page … Rework our valuation section to use this type of UI (keeping with the general theme we have). We still need a lookup button for the values. dont include the "UI Narration" from the screenshots. /razor-html-mockup-creation run this first so theres a mockup for my approval of the new case page design

What the screenshots show:
- a three-column grid of guide cards (CAP, Glass's, Cazana, Brego, Super CAP, Market research), each with Retail and Trade, with CAP marked "Selected" in a red border;
- Super CAP and Market research tagged "Manual" with typed boxes;
- a dashed "Value increases" list in two columns, with tick boxes, suggested figures, "VAT (on commercial) +20%" and two "Other…" rows;
- a dashed "Condition deduction" row with a £ box, a "Previous total loss" tick box and a −10 % / −20 % switch;
- an "Apply to engineer's value" button;
- an "Engineer's value (PAV)" panel;
- "Disclose guide source on report" and "Include valuation commentary on report" tick boxes;
- a "Composed — what the report carries" line.

Narration not drawn, as asked:
- "click a guide to select · Super CAP & Market research entered manually";
- "tick to add · suggested figures — overwrite freely";
- "Retail £3,100 — no adjustments; apply sets the engineer's value to the guide retail.";
- "one field, two places — mirrors §7 decisions";
- "Composed — what the report carries".

## 8 October 2026: four questions before drawing

The screenshots conflict with earlier rulings or with how the system works, so four questions were put to the operator. The answers:

1. **Apply.** The 23 September ruling has no Apply: the calculation fills the box as anything changes. *Answer: live fill, no Apply.*
2. **Where the lookup goes.** Only Glass's has a connected provider. *Answer: on each connected card, as today.*
3. **Retail and Trade boxes.** Today these are typed Case fields; the screenshot has only the Engineer's Value. *Answer: they follow the selected card.*
4. **Market research.** In Pegasus it is AI market research, with a Valuation month and a Get valuation that starts a job. *Answer: an AI card that keeps its lookup.*

The answers are recorded as D1 to D5 in [how-it-should-work.md](../pages/case-record/valuation/how-it-should-work.md). The remaining differences from today are lettered items A to K in [v34-notes.md](v34-notes.md).

## 8 October 2026: the mockup

Built on `task/valuation-cards-v34` from `origin/dev` acb2ffbd1, by forking the v33 build (captured frame, live CSS, arithmetic and recording rule). The self-check passed. Stopped for sign-off.

## 8 October 2026: round 1 feedback

> Parts of the box are randomly not clickable for valuations

**Cause.** The mockup had copied the live rule (`selectCard` in `case-workspace.js`): a click on a label ("Retail", "Trade", "Guide month") or inside a box is skipped, so only the gaps chose a card. A card with no retail, such as Super CAP, ignored every click without a word.

**Changed.**
- The whole card is the click target, except its Get valuation button and links.
- Choosing a card updates it in place, so a box clicked to choose its card keeps its caret.
- A guide card with no retail shows the existing approved sentence "Enter the retail value on this card to use it.", and typing a retail removes it.
- The self-check gained the whole-card checks and now passes 671 (was 656).

Recorded under item E, D9 and D9a. Stage 2 must change `selectCard` the same way.

## 8 October 2026: the list settled

The operator answered the lettered list:

> 1. this is the task. its not a decision thats open. the point of the task is to do this. b. selected c. approved sentence D. keep E. not a decision thats open this is literally what i asked for F. Confirm G. Confirm H. Stays out I. Confirmed J. Confirm K. keep

**Lesson.** A (cards in place of rows) and E (a click is the decision to use) were the request itself and should never have been listed as open. They keep their letters, marked as the request.

**Changed.**
- The strip's two switches were removed. The mockup draws "Selected" on the chosen card and the approved sentence on the four unconnected cards.
- The self-check runs each state once and passes 366.
- 20 screenshots were taken.

**Reading of I.** "Confirmed" is taken to mean that, with no card chosen, the report's Retail and Trade are blank and the report stays blocked, and that a figure no card holds cannot be typed.

The notes (sections 6 and 9) and how-it-should-work (D1–D14, "Decided 8 October 2026") record the settlement. Stage 2 waits for the operator's go-ahead.
