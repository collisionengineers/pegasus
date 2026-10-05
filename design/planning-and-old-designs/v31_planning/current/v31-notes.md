# v31 notes: the Triage Case page

Built on 5 October 2026 from `origin/dev` 81b571c36. These notes cover the offline proposals only and are not application evidence.

## 1. What changes, and where it shows

The operator's numbered requests, applied to all three designs:

| # | Live today | Proposal | Shots |
| --- | --- | --- | --- |
| 1 | Files lists Documents only | Files has two tabs, Documents · n and Correspondence · n. Correspondence uses the regular Case's table: Received, Sender, Subject, Classification, Open message. Each row opens the message dialog with Reply, Reply all, Forward and Open full message. | 37–39 |
| 2 | An inline "Chaser correspondence" form (To/Cc/Subject/Message/Attachments) in the page body | The form is removed. Send chaser (or Reply with outcome once Completed) opens the Inbox composer overlay, prefilled with the reply. It is offered from Actions and from the Correspondence tab. "Latest send" and Reconcile status move into the Correspondence tab. | 31–33 |
| 3 | Cancel Triage, Complete, Await information, Record correction and Reopen sit in the Determinations panel | One ribbon Actions menu, using the regular Case's `details.menu` markup. Cancel Triage is red and last; Reopen replaces it once the Triage is settled. | 28–30 |
| 4 | Determinations is an inline form | The Determinations dialog (Roadworthiness, Repair outcome, Reason · Save determinations) opens from Actions. The page shows the two values as read-only boxes. | 34–36 |
| 5 | An Assign/Reassign button in the ribbon's Assignee fact | Assign/Reassign is a menu item. The Assignee fact stays in the ribbon as a value only. | 28–30 |
| 6 | Link case / Unlink case in the record bar | These are menu items. The record bar is gone. | 28–30 |
| 7 | Open message in the record bar | Removed. The request e-mail is the first row of the Correspondence tab. | 01–03 |
| — | Vehicle images sit mid-page in a 135px gallery | Images lead the page in all three designs (A stage, B split viewer, C contact sheet). | 01–03, 40–42 |

Actions menu order: Assign/Reassign · Determinations · Record correction · Complete Triage · Await information · Send chaser/Reply with outcome · Link/Unlink case · separator · Cancel Triage (red) or Reopen.

## 2. Live rules the mockup mirrors

Each rule is listed with the source it comes from:
- **Mutable:** the state is not Completed or Cancelled (`_TriageCase.cshtml` `mutable`). Assign, Determinations, Link/Unlink and Cancel Triage are offered only while mutable. Reopen is offered otherwise.
- **Complete Triage** is offered only in Finding recorded. **Await information** is offered in Open or Finding recorded. **Record correction** is offered on Completed with a current finding (`correction`).
- **Reply purpose** (`TriageLifecycleRules.ReplyPurpose`) is the chaser until Completed, Reply with outcome on Completed, and none on Cancelled. A reply is offered only when the Triage came by e-mail (`CanSendReply`) and no send is in flight (`ReplyOperationBlocked`).
- **Blocked:** "The existing correspondence operation must finish or be resolved before another action." Reconcile status shows when the latest send is Unknown.
- **Images:** the section renders only when there are evidence images (`EvidenceImages.Count > 0`). The viewer keeps Crop and Tag from the pre-Case viewer (`OperatorLabels.PreCaseImages`).
- **Labels:** labels are exact strings from `OperatorLabels.Triage`, `CaseWorkspaceLabels.Files`, `_ReasonDialog` ("Reason for action", "Confirm Action") and the Cancel consequence "Cancelled Triage can be reopened with a reason."
- **Notices:** "Finding recorded.", "Triage completed.", "Chaser sent.", "Reply sent.", "Assigned to {name}.", "Case linked.", "Triage cancelled." and the others are the live action notices.

## 3. Frame rules

- There is no working-set strip. The ribbon is 56px.
- Body text is 13.5px and controls are 36px (design authority).
- The ribbon carries one primary button (item B) plus the Actions menu.
- No explanatory copy is added. Every visible sentence is either a live string or a mockup-only control.
- B's aside is 340px at 1181px and above and folds into two columns below that. All three designs are single-column at 760px.
- C's ribbon and tab row are sticky under the 48px utility bar.

## 4. Deliberate departures from live

- **The composer is a Triage composer.** It reuses `_ComposeForm`'s layout and fields: From, To, Cc, Subject, Message, Attachments. It drops Find a Case and Case/PO, because the Case is this Triage Case. Its eyebrow is "Triage · t.QDOS…" and its submit keeps "Send chaser" / "Send reply" (item E).
- **The message dialog's Reply, Reply all and Forward** lead to the Inbox record's composer, as they do on the regular Case. In the mockup they open a "Mockup destination" note.
- **"Next step beside Actions"** (item B) shows the state's lead button outside the menu: Determinations, Complete Triage, Reply with outcome or Reopen. It mirrors the live Determinations panel's primary button.
- **C only:** Roadworthiness and Repair outcome are shown in the tab row, and Exact response evidence sits in the Correspondence tab (item D).

## 5. Lettered sign-off items

- **A.** Choose a design: A · Image stage, B · Inspection split (recommended) or C · Contact sheet. *Confirm, or choose another.*
- **B.** Should the state's next step (Determinations, Complete Triage, Reply with outcome, Reopen) show as a red button beside Actions, with everything else in the menu? *Confirm, or put everything in Actions.* (Mockup switch: "Next step beside Actions".)
- **C.** Open file (the retained original for a Triage that came by Upload) is removed with the record bar. The file is already a row in the Documents tab. *Confirm, or keep Open file as an Actions item.*
- **D.** Exact response evidence is kept as its own section in A and B and sits inside the Correspondence tab in C. *Confirm, or always move it into the Correspondence tab.*
- **E.** The composer keeps the Triage chaser's purpose: it sends as a reply to the request e-mail, offers the request's own files as attachments and records "Chaser sent." *Confirm, or use the general Inbox composer (Find a Case, the Case's files).*
- **F.** Reply with outcome opens the same composer, prefilled from the outcome template. The completion notice keeps its "Reply with outcome" link. *Confirm, or change.*

## 6. Self-check

- 5 October 2026: `python check-triage-designs.py` gave `RESULT {"fail":[],"okCount":1614}` with no console errors and no external requests.
- Coverage: 3 designs × 13 states × both item B options, then the main flows. The flows are Determinations → Complete → Reply with outcome, Send chaser, Assign, Link case, Cancel, the message dialog, the viewer, keyboard focus return and composer focus containment.
- 81 state screenshots (27 per width at 1580×1000, 1440×900 and 760×1000), 15 dialog shots and 3 full-page shots are in `v31-triage-shots/`. See `verification.json`.

## 7. Known limits

- **Images:** the photographs are generated SVG scenes, not real photographs. The registration MA59BDY and all names are synthetic.
- **Actions are simulated:** each action changes only the page and lands a fixed result. There is no concurrency conflict, lease or failed send.
- **Viewer:** Crop draws a fixed frame rather than a dragged one.
- **Fonts:** the Inter font is inlined from the checkout. A missing glyph falls back to the system font.
