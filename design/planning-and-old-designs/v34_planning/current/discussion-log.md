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
