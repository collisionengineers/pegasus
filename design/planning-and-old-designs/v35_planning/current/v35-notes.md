# v35 notes: the Case aside's Next action

Temporary review artifact; see [README](README.md).

## 1. What is wrong today and what each design changes

Today, in the operator's screenshot of 8 October 2026 (Review, eleven blockers), the step reads "Assign Engineer" beside a button that also says Assign Engineer. Under it, an amber-filled panel holds eleven white cards. Each card has its own amber bar, the captions **Source:** and **Why:**, the same "Assessment record" and "No value is recorded." lines repeated, and a button named after its section. The same section button appears twice in a row (Vehicle, Vehicle). The aside's content is 1,809px tall at 1580, so the sticky aside scrolls through about two screens.

| Problem today | Change | Shots |
| --- | --- | --- |
| The step's words and its button say the same thing | A, B, C: where the control says what the step says (Assign Engineer, Create audit), the step is that one control at full width. Steps whose control names a section (Generate report → Report) keep today's words and link | 02–04, 19–21 |
| The amber fill and per-card amber bars read as a warning on top of a warning | A, B, C: no fill and no bars. A small amber warning glyph beside "Report not ready" and an amber dot per blocker keep the incomplete colour (design README) | 02–04 |
| Card per blocker with captions and a section button: 132px per blocker | **A** groups the blockers under the section that clears them; the requirement is the link; source and reason share one line. 80px per blocker, aside content 1,348px | 02, 06, 10 |
| | **B** is one 35px line per blocker: requirement, and at the right the section link. The line opens to show source, reason and what clears it, with today's captions. Aside content 665px | 03, 07, 11, 13 |
| The list pushes the step and the Figures out of view | **C** gives the blockers their own card, "Report not ready", below Next action. Each row is one whole link with the section at its right. At 1580 only that card scrolls, so Figures and the step stay in view. 83px per blocker | 04, 08, 12 |
| At 1440 and below, the folded strip stretches Figures to Next action's height | C's blocker card spans the strip's full width, so Figures and Next action sit side by side at their own heights | 22–25 |

Heights are the aside's content measured at 1580 in the Review state (eleven blockers) and the near state (four).

## 2. Live rules the mockup mirrors

| Rule | Source |
| --- | --- |
| Next action order: AI drafts, Cancellation received, the stale notice, the step, then the blockers | `_CaseAside.cshtml` |
| The step: Assign Engineer in Review (no control where the Actions menu does not offer it); none With Engineer while blockers exist; Generate report, Waiting for the report to be stored, Send report; Create audit or Mark completed after the send | `CaseNextAction.Of`, `DetailsModel.NextAction` |
| Every blocker in page order: section by section, then by field order | `CaseWorkspaceLabels.Report.InPageOrder` |
| A blocker's control: the Repair Spec claim with focus for the two repairer VAT blockers; else the section (and its Images or Fee tab); else Accounts for an Administrator; else none | `BlockerEditFocus`, `BlockerSectionKey`, `BlockerTab`, `BlockerOpensAccounts` |
| No count of blockers anywhere | FRD-13 "Blockers are specific" |
| The aside is sticky and capped at 1441px and above; the list is capped at 400px in the folded strip below | `case-workspace.css` |
| Blocker words | `AssessmentPolicy.Evaluate`, `CaseReportReadiness` |

## 3. Frame rules

- Aside 285px at 1441px and above, sticky under the 48px utility bar and the sticky block; folded into a two-column strip above the sections at 1440 and below; one column at 760.
- 12.5px aside rows, 11px blocker detail, 36px full-width step control (today's step button is the 32px small button).
- No new copy: every word is a live label or Core's blocker text (asserted by the self-check).

## 4. Decisions taken and their authority

- Amber stays the incomplete colour: design README, product states.
- Every blocker keeps its requirement, source, reason and what clears it, each linking where today's does: FRD-13, FRD-16.
- Page order is kept, so A's groups are the runs of one section that the order already produces.

## 5. Deliberate departures from live

- The captured frame is a Not ready Case; the ribbon's chip and Engineer are repainted per state, but the sections still show that Case's values.

## 6. Sign-off list

Which design (A, B, C or a mix) is the operator's choice and is not lettered. Each item below applies to the designs named.

- **A** (A, B, C). Where the step's control says what the step says, the step is that control alone at full width; Generate report and Send report keep their words with the Report link. Confirm, or keep words and button side by side.
- **B** (A, B, C). The step control is a secondary button. Confirm, or make it primary red (strip: Step control; shots 30–32).
- **C** (A, B, C). The amber fill and the amber bar on each blocker go; an amber glyph beside "Report not ready" and an amber dot per blocker remain. Confirm, or keep the fill.
- **D** (A, C). The **Source:** and **Why:** captions go; source and reason read as one line, "Assessment record · No value is recorded.". Confirm, or keep the captions.
- **E** (A). Blockers sit under their section's name, and the requirement itself is the link; there is no button per blocker. The repairer VAT blocker's requirement opens the Repair Spec for editing, as its button does today. Confirm, or keep a section button per blocker.
- **F** (B). Source, reason and what clears it show only when a row is opened. FRD-13 says the screen shows each, so FRD-13 and FRD-16 would say they show on opening the row. Confirm, or show them always (which is A or C).
- **G** (C). The blockers leave the Next action card for their own card, "Report not ready", below it. With Engineer with no other step, there is no Next action card. FRD-16 and FRD-13 would say the aside carries the readiness list in its own card. Confirm, or keep the list inside Next action.
- **H** (C). At 1441px and above only the blocker card scrolls, so Figures and Next action stay in view. Confirm, or let the whole aside scroll as today.
- **I**. This folder is removed in the Stage 2 pull request. Confirm, or keep it as the record.

## 7. Self-check

`python check-rail-v35.py` on 8 October 2026: `RESULT {"fail": [], "okCount": 768}`, no console error. It covers live, A, B and C in all six states at 1580, 1440 and 760. This is evidence about the mockup, not the application.

## 8. Known limits

- The frame is a captured page with its scripts removed: section links scroll to the section but do not open a tab or edit mode, and step buttons do nothing.
- The Inspection + Audit Case's Views card and the Linked cases card are not drawn; they sit above Figures and no design changes them.
- The fixtures are synthetic; the eleven-blocker list follows the operator's screenshot and continues in page order.

## 9. 8 October 2026, second round: a Case missing nearly everything

The operator asked what the aside looks like when images, claimant or Case details and the original report are missing. Added state **Not ready · original report, images, Case facts missing**: a standalone Audit just in, with its original report and images outstanding (the Case requirements) and 22 report blockers, Case facts included (`AssessmentPolicy.EvaluateReadiness`). Shots 33–42.

What today does with it (shot 33):

- The step names only the first Case requirement, "Original report missing", with a Case details button (`CaseNextAction.BeforeTheReport`). "Images incomplete" is not in the aside at all; only Case details' Outstanding requirements panel lists it.
- "Original report missing" links to Case details, but **Mark as original report** is on Files (`_CaseDocuments.cshtml`).
- The aside's content is 3,346px tall at 1580. A: 2,331px, B: 1,046px, C: 2,112px (the step stays in view, and the blocker card scrolls).
- Outside this round: Case details' Outstanding requirements head shows "2 outstanding", the kind of count FRD-13 rules out.

New items:

- **J** (A, B, C). Next action lists every outstanding Case requirement, headed "Outstanding requirements", in the design's own row style above "Report not ready", in place of the one step line (strip: Case requirements; shots 37–42). Each keeps its source and reason and today's Case details link. FRD-13 and FRD-16 would say "names every outstanding requirement". Confirm, or keep the first only.
- **K**. "Original report missing" links to Files, where Mark as original report is, rather than Case details. Confirm, or keep Case details.

Self-check on 8 October 2026 after this round: `RESULT {"fail": [], "okCount": 1194}`, no console error, seven states. The captured frame still shows that Case's own claimant, claim reference and so on, so the sections disagree with this state's blockers.
