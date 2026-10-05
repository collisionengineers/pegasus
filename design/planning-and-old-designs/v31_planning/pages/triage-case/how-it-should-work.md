# Triage Case: how it should work

Proposed on 5 October 2026 and pending the operator's sign-off: items A–F in [v31-notes.md](../../current/v31-notes.md#5-lettered-sign-off-items).

## Shared by every design

- **Ribbon.** The same facts as today; Assignee is a value only. Then the state chip, the state's next step (item B) and one **Actions** menu.
- **Actions menu**, in order, each item only when the state allows it:
  1. Assign / Reassign
  2. Determinations (dialog)
  3. Record correction
  4. Complete Triage
  5. Await information
  6. Send chaser / Reply with outcome (composer)
  7. Link case / Unlink case
  8. a separator
  9. Cancel Triage (red) or Reopen
- **No record bar.** Open message is removed. Open file is item C.
- **Determinations** read as two greyed boxes and are edited only in the dialog. Its handlers are unchanged (`record_finding`, `supersede_finding`).
- **Files** has a Documents tab and a Correspondence tab. The Correspondence tab is the regular Case's table and message dialog, plus the latest send status, Reconcile status and the reply button.
- **The composer** overlays the page in the Inbox composer's frame. It submits to the existing `TriageSendReply` handler, so the purpose, attachments and refusal rules stay where they are (item E).
- **Vehicle images** lead the page. The viewer keeps Crop and Tag.

## Per design

- **A · Image stage:** a lead image with a filmstrip, then Determinations, Exact response evidence, Files and Notes.
- **B · Inspection split:** a persistent viewer with inline Tag and Crop and a thumbnail grid. Beside it is an aside with Determinations, a Correspondence card, Exact response evidence and Notes. Files runs below.
- **C · Contact sheet:** a sticky ribbon and tab row (Images, Files, Notes) with the determinations at the row's end. Images is a large-thumbnail grid.

## Decided, 5 October 2026

C · Contact sheet, implemented: no Crop or Tag, no Await information, Record finding with Complete Triage and Reply with finding tickboxes, and Reply with finding as the reply's name. See [v31-notes.md](../../current/v31-notes.md#8-decided-5-october-2026).
