# Triage Case: how it works today

Read from `origin/dev` 81b571c36 on 5 October 2026. Sources:
- `src/Pegasus.Web/Pages/Cases/Shared/_TriageCase.cshtml`
- `Pages/Cases/Details.Triage.cs` (`TriageCaseView`, the `TriageAction`, `TriageSendReply` and `TriageReconcileReply` handlers)
- `Pages/Cases/Shared/_TriageDeterminationFields.cshtml`
- `wwwroot/css/triage.css`

## Order on the page

1. **Header.** Eyebrow "Triage", the `t.` reference, Back to Cases and Refresh.
2. **Notices.** The action notice (with a "Reply with outcome" link after completion), the case-association warning and the one-time `CaseStatus`.
3. **Ribbon.** Triage reference, Registration, Principal, Source, Opened, then Assignee. The Assignee fact carries an **Assign / Reassign** button while the record is mutable and the roster is not empty. Then the Case link (or None) and the state chip.
4. **Record bar.**
   - **Open message** links to `/Inbox/{id}` for an e-mailed Triage.
   - **Open file** (`/Received/{receipt}/Source`) appears for a non-e-mail origin.
   - **Link case / Unlink case** appears while mutable and association is available.
5. **Determinations panel.**
   - While mutable, an inline form: Roadworthiness, Repair outcome and Reason, with **Save determinations**. Save is primary in Open and Awaiting information. The form records a new finding, or supersedes the single active finding.
   - With several active findings, a reconciliation warning shows instead.
   - When settled, greyed read-only boxes.
   - The transition row:
     - Complete Triage (Finding recorded)
     - Reply with outcome (an anchor to the reply panel, Completed)
     - Record correction (Completed with a finding)
     - Await information (Open or Finding recorded)
     - Cancel Triage (red, while mutable) or Reopen
6. **Vehicle images.** Rendered only with evidence images. A `_ImageGallery` grid of 135px tiles opens `_EvidenceViewer`, which carries pre-Case Crop and Tag.
7. **Chaser correspondence / Reply with outcome.**
   - Rendered only for an e-mailed Triage whose approved mailbox may send, and only when there is a reply purpose or an existing send.
   - It shows the latest send status, with Reconcile status when that status is Unknown.
   - An inline form holds To, Cc, Subject, Message (4 rows, or 8 for an outcome reply) and the intake attachments as checkboxes. The submit is **Send chaser** or **Send reply**.
   - While a send is in flight, the blocked warning replaces the form.
8. **Exact response evidence.** Rendered when evidence is linked or there are candidates. It shows the timeline, Unlink response (with reason), and the candidate select with "Record and link exact response".
9. **Files.**
   - The head holds the custody chip, Add evidence (to Upload) and More › Open in Box.
   - The body is a **Documents list only**: there is no tab strip and no Correspondence.
10. **Notes.** Add note (while mutable) and the newest-first history.

## Dialogs

- Assign: Assignee select, with Unassign / Cancel / Assign.
- Record correction: the determination fields, posting `supersede_finding`.
- Cancel Triage: a reason dialog with the consequence "Cancelled Triage can be reopened with a reason.".
- Link case: Case ID and Reason for action.
- Unlink case: a reason dialog.
- Reopen: a reason dialog.

## Handlers

Every write posts `TriageAction` with an `actionName`:
- `assign`, `unassign`
- `note`
- `await_information`
- `record_finding`, `supersede_finding`
- `link_response`, `unlink_response`
- `complete`, `cancel`, `reopen`
- `link_case`, `unlink_case`

The handler claims and releases the Triage edit scope inside its own save, through `TriageWriteAuthority`. The reply posts `TriageSendReply`:
- it requires PerformCasework, the retained mail, an approved staff-send mailbox and a fresh operation key
- it resolves attachments through `AttachmentResolver.ResolveIntakeAsync`
- it sends `StaffMailPurpose.TriageChaser` or `TriageOutcomeReply` as a Reply

## What is awkward

- The page is a long single column. The images sit below the Determinations form, so assessing means scrolling between the photographs and the decision.
- The reply form is always open in the page body, even when no reply is needed.
- Actions are scattered across three places: the ribbon (Assign), the record bar (Open message, Link case) and the Determinations panel (Complete, Await, Cancel, Reopen).
- A Triage Case has no Correspondence tab. A chaser that was sent can only be found through the Inbox.
