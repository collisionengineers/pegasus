# UI Inbox — task plan

Branch `task/ui-inbox`. Pages 2 (Inbox half) and 6 (Operations Email, merged
away).

## What this PR owns

1. **The Inbox**: H1 only, Received/Sent direction tabs, one filter scale, and
   a row an operator can recognise a message by.
2. **Operations Email retired**: the route redirects into the Inbox's Failed
   filter. Its content is the Sent tab and the Failed filter.
3. **Sender, subject and the case it produced** on the list projection.

## Decisions

- **The Inbox lists everything received, including messages that became a
  case.** The counts still exclude them — a count measures what is waiting.
  The defect was never the presence of an accepted receipt in the list; it was
  that the row read "Instruction draft" with no indication it had produced
  anything. The row now says which case, and links to it.
- **Subject is read from evidence**, not a new column. The subject is a fact
  the reader recorded; adding a column would create a second writer for it and
  need a migration for something only the Inbox displays.
- **"Instruction drafts" is not relabelled, it is gone.** A definitive
  instruction is already a case, so there is no pending-draft queue to filter.

## Two real bugs found while building this

- **The intake gate blocked every POST to the Inbox index**, not just the
  manual upload handler its own comment describes. Any other action landing
  there — retrying a mailbox that failed to deliver, for one — was refused in
  Production for no stated reason. Narrowed to the handler it names.
- **The upload form's binding errors failed the retry post.** Two forms share
  the page; the upload form's required fields are not posted by the retry, so
  its model errors were failing an action that carried everything it needed.

## Verification

- Core 441/441, architecture 73/73, integration 399 passed / 0 failed
- `OperationsWebTests` asserts the redirect, the merged content, that the
  recorded failure code is never shown as text, and that the retry reaches
  Core and reports its outcome — the last of which is what caught the binding
  bug above
- The accessibility sweep covers `/Intake`; `aria-selected` on plain links was
  a genuine violation and is now `aria-current`

## Deliberately deferred

The manual upload form stays on this page, demoted below the list, until the
Upload page exists in the next change. A capability with no way in is worse
than one panel in the wrong place for one merge.
