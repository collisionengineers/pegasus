# v34 notes: the report dispatch screens

Stage 1 of the round the operator asked for on 6 October 2026 ("Mockup round for all three"): the Report sending panel on the Principal contact, the Tasks section, and the one-step delivery form, plus the two smaller surfaces that ruling day added (the Next action open-task blocker and the Work Centre row). Everything here is captured from the as-built pages on branch `task/report-dispatch-integration` (PR 1015, head `4803fa7c4`, CI green). Nothing has been accepted; the lettered list in section 6 decides what Stage 2 changes.

## 1. What is shown, and where

There is no previous version of these screens: the round presents what the PR built so the operator can accept or change it. The table says what each state shows and which shots carry it (numbers from `v34-shots/`, five per state: whole page, surface crop, 1580, 1440, 760).

| Surface | State | What it shows | Shots |
| --- | --- | --- | --- |
| Delivery form | `plain` | Default rules: To seeded from the original sender, no question, no hold; Report ticked and fixed; File name; Message pre-filled with the SOP body; one Send report | 01–05 |
| Delivery form | `rules` | "Already sent on … to …"; From line (reply in the instruction's thread); Override reason box because an unanswered question could stop the send; two Yes/No questions ("Does the instruction mention "Luton"?", "Are the images from Garage?"); Cc seeded with the Cc hint and a never-cc removal; the two warnings; After sending list; a hold with its Done tick; Send report | 06–10 |
| Delivery form | `stop` | "Stopped." notice with the rule's text; Override reason required; the separate fee note offered under Attach | 11–15 |
| Delivery form | `missing` | "The figure breakdown this Principal requires has not been generated…"; Repair Spec ticked and fixed, "(required for this Principal)"; no Send report | 16–20 |
| Tasks | `edit` | New task field with Add task; the table (task, assignee, state chip); an open row with the assignee select, Assign, Complete and Cancel task; a completed row with no actions | 21–25 |
| Tasks | `read` | The same list with no form and no actions (the lazy fragment, as a colleague or a closed Case sees it) | 26–30 |
| Next action | `next` | After the send: Mark completed greyed with the tooltip, the open tasks listed under Tasks, each with a Tasks button | 31–35 |
| Next action | `archive` | A closed Case with an open task: Archive greyed in the ribbon | 36–40 |
| Work Centre | `row` | The Open tasks row: first task "(+2 more)", Tasks chip, Unassigned, No due date, Open tasks action, Dismiss | 41–45 |
| Contact panel | `kerr-edit` | A Principal with the Default rules, in the Contact's edit | 46–50 |
| Contact panel | `dfd`, `ax`, `mp`, `pch`, `rjs`, `qdos` | The seeded rules as the panel shows them: Claim Source rules with a Stop (DFD), fixed To and the Bodyshop mentions rule (AX), required companions and no report images (MP), send to only, Audatex and the attachment name (PCH), Instruction mentions (RJS), Cc and garage figures (QDOS) | 51–80 |

## 2. Live rules the mockup mirrors

Each state is the server's own render, so the rules are the source's.

| Rule | Source symbol |
| --- | --- |
| The plan the form follows, made once on load with no answers | `ReportDispatchPolicy.Plan`, `DetailsModel.DeliveryDispatchPlan` |
| Mode and From line: reply from the instruction's mailbox, else new message from Send from, else the default staff-send mailbox | `ReportDispatchPlan.Mode`, `CaseWorkspaceLabels.ReportDelivery.FromReply` / `FromNewMessage` |
| Questions only for undecided conditions; "Instruction mentions" searched when the Case holds instruction text | `ReportDispatchPolicy.Condition`, `ReportDispatchPolicy.Mentions` |
| Override reason shown when a Stop applies (required) or an unanswered question could stop the send | `ReportDispatchPlan.Stop`, `ReportDispatchPlan.StopPossible` |
| Removals win, and the Cc hint names what set each copy and what was removed | `ReportDispatchPlan.Excluded`, `ReportDispatchPolicy.Reconcile`, `ReportDelivery.CcHint` |
| Required companions ticked and fixed; Send report withheld while one is not generated | `ReportDispatchPlan.RequiredCompanions` / `MissingCompanions`, `CaseReportDeliveryPolicy.AttachChoice` |
| A hold has a Done tick, required when it applies; a hold only an answer can decide has an optional tick | `ReportDispatchPlan.Holds` / `PossibleHolds` |
| The built-in message and the greeting | `EmailTemplates.CaseReportDeliveryDefault`, `ReportDispatchPolicy.Greeting` |
| Tasks: add, assign, complete, cancel in the edit session only; one fixed reason each | `DetailsModel.OnPostCreateCaseTaskAsync` … `OnPostAssignCaseTaskAsync`, `CaseWorkspaceLabels.Tasks` |
| Open tasks list in Next action after the send; Mark completed and Archive greyed with one sentence | `CaseNextAction.AfterTheSend`, `CaseTaskRules.OpenTasksBlockTerminal` |
| One Work Centre row per Case with an open task: oldest task named, count of the rest, no due instant, owner = assignee else creator | `NeedsAttentionKind.OpenTasks`, `EfDashboardQueries.ListOpenCaseTasksAsync`, `NeedsAttentionPresentation` |
| The Report sending panel's fields and the seeded rules | `PrincipalReportSendingRules`, `OperatorLabels.PrincipalAdministration`, migration `20261007090000_PrincipalReportSendingRules` |

## 3. Frame rules

The captures are the live shell, so the numbers are the live ones: 48px utility bar, 56px ribbon, 40px section row, the 12-column grid with 36px cells, the 285px aside at 1441px and above, 13.5px body text and 36px controls. The round proposes no frame change.

## 4. Decisions taken and their authority

Every behaviour the screens show was ruled by the operator on 6 October 2026 (recorded in PR 1015's description and in FRD-04, FRD-11, FRD-13, FRD-15, FRD-16 and FRD-21 on the branch). The round takes no design decision of its own: the layouts are what the as-built Razor renders, and every point where a choice was needed to build is a lettered item below.

## 5. Deliberate departures from live

None: the mockups are captures. Two things differ from a live page and are not design: the fixtures are the integration tests' own (synthetic Cases, addresses and names), and deferred sections keep their loading state because no server answers (section 8).

## 6. Sign-off list

Each item is "Confirm, or …". An item changes an FRD, a sentence on the page or a control's place, so none is taken without the operator.

Delivery form (`pegasus_case_report_delivery_v34.html`):

- **A.** The Yes/No radios: in the capture the "Yes" label is not visible beside its radio (shot 07). Confirm Stage 2 fixes the control's style so both words show, or say how the question should read.
- **B.** The hold ticks: the checkbox and "Done" sit far from the hold's text (shot 07, After sending). Confirm Stage 2 puts the tick on the hold's own line, or say where it goes.
- **C.** When a Stop is only possible (an unanswered question carries one) the Override reason box appears with no sentence (shot 07). Confirm it stays bare, or give the sentence.
- **D.** Two approved sentences were reworded from "prepare" to "send" when delivery became one step: "The {document} this Principal requires has not been generated. Generate it, then send the report." and the panel hint "Send is refused; staff may override with a reason." Confirm the new wording.
- **E.** The refusal when the rules or the Case's facts changed under an open form reads "The report was not sent. This Principal's report sending rules or the Case's facts changed; check the form and send again." Confirm, or give the sentence.
- **F.** The "Already sent on … to …" line sits at the top of the form (shot 07). Confirm its place.
- **G.** The "No recognised estimate is filed on this Case." warning also shows when an estimate is filed but not in the format the Principal's tick names (PCH with a Glass's estimate). Confirm, or give a second sentence for that case.
- **H.** The From line reads "From {mailbox} — reply in the instruction's thread" / "New message from {mailbox} (no instruction e-mail on this Case)". Confirm the wording.
- **I.** The After sending list and the holds have no heading of their own beyond "After sending" (shot 07). Confirm, or name the heading for holds.

Tasks (`pegasus_case_tasks_v34.html`):

- **J.** Assign is a select of enabled staff with a blank first option and an Assign button; on a row it wraps under the select (shot 22). Confirm Stage 2 keeps it on one line, or say how assigning should look.
- **K.** Unassign is not offered. Confirm, or ask for it.
- **L.** Rows are open tasks first, then by task. Confirm the order.

Next action (`pegasus_case_next_action_blocker_v34.html`):

- **M.** Each listed task has its own Tasks button (shot 32). Confirm, or one link under the list.
- **N.** The Cases rail quick detail still says "Mark completed" with no gate and no task list. Confirm, or say what it should show.
- **O.** The gated Mark completed shows as a greyed button in Next action rather than the usual link (shot 32). Confirm.
- **P.** The list shows in both views of an Inspection + Audit Case, because tasks belong to the Case; while an Audit is created after the Inspection was sent the Inspection's tasks are not listed until the Audit is sent. Confirm.

Work Centre (`pegasus_work_centre_tasks_v34.html`):

- **Q.** Labels chosen to build: kind "Open tasks", chip "Tasks", action "Open tasks", row title "{first task} (+N more)". Confirm the words.
- **R.** "First" is the oldest open task; Received shows that task's creation; the chip sits before AI draft. Confirm each.
- **S.** The row has no due instant and sits in Later. Confirm (ruled 6 October: always Normal).

Contact panel (`pegasus_contact_report_sending_v34.html`):

- **T.** The panel is a full edit form on the Contact page as the other Principal panels are, with one spare blank rule row and the five hints. Confirm.
- **U.** The condition kinds in the order Claim Source, Repairer, Images from, Instruction mentions, Sender is not, Outcome, Bodyshop mentions. Confirm the order and the words.

Found while writing the page folders:

- **W.** An address staff type that the rules exclude is taken out at send without a word: the form's removed line shows only what the plan itself removed. Confirm, or say how the send should tell staff (a status sentence after the send, or a refusal before it).
- **X.** The store refuses Close case and Correct principal too while a task is open (the old guard covers every terminal change), but only Mark completed and Archive are greyed; Close case and Correct principal refuse after the click. Confirm the gate greys those as well, or that the guard should cover only completion and archiving.
- **Y.** Work Centre Find does not match the task's words on an Open tasks row (the title is the vehicle). Confirm, or ask for Find to search the task text.
- **Z.** The Work Centre lists a Case's open tasks whenever one exists, but the Case page lists them in Next action only once Report sent is recorded. Confirm both, or align them.

Report generation (`v34-samples/`, three MP sample PDFs with their first pages as PNG):

- **V.** Page 1 of an image-free report. As built, the lead photo slot is left out and the plan drawing keeps its place (`mp-report-image-free-slot-left-out.pdf`); the alternative frames an empty slot where the photo would be (`mp-report-image-free-slot-blank.pdf`); `mp-report-with-images.pdf` is today's report for comparison. Confirm the slot left out, or choose the framed empty slot.

## 7. Self-check

`check-v34.py` on 6 October 2026 (21:28Z): `RESULT {"fail": [], "okCount": 53}`, 80 shots, no console error on any state (`v34-shots/verification.json`).

## 8. Known limits

- The fixtures are the tests' synthetic ones: Case QDOS-2026-00042 / AB12CDE, example addresses, and a Work Centre row dated by a fixed clock ("1674 d ago").
- Deferred sections (Vehicle in a read of the Case) keep their loading state: the mockup has no server to answer the fragment request.
- Posting a form or following an application link does nothing.
- The sample PDFs were rendered from the renderer tests' fixture (`ReadySnapshot` with three photos), not from an MP Case; the blank-slot variant was a temporary layout change that is not on the branch.
- The mockup is not application evidence; the branch's CI is.
