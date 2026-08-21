# Inbox message page — approved design

The target for the `/Inbox/{id}` redesign, drawn against **`origin/dev`** as of
2026-08-21, covering the nine MAIL tickets then in Verifying. Implemented by
[[MAIL-006]]. Live canvas:
<https://claude.ai/code/artifact/60fbd406-0644-4754-9107-2669e5faa97e>

## Open these

`preview/*.html` are plain, self-contained pages — **double-click one and it
renders**. No tooling, no build step. Start with `preview/Main.html`.

The matching `*.dc.html` files are the canvas sources (page markup inside
`<x-dc>`, stylesheet inside `<helmet>`); `canvas.json` holds the layout and the
design notes. Every class name and value in the stylesheet is copied from
`src/Pegasus.Web/wwwroot/css/site.css` on `origin/dev`, so the markup lifts into
Razor without translation.

| Artboard | State it shows |
| --- | --- |
| `Main` | Message tab, unclassified and unfiled — the deployed state |
| `Filed` | Message tab, classified and filed to a case |
| `Correcting` | *Correct classification* pressed — the dialog over the page |
| `Moving` | *Move to folder* pressed — a confirmation, no typed reason |
| `Case` | Case tab, not yet linked: search, candidates, confirm target |
| `CaseLinked` | Case tab, linked, with unlink |
| `FolderStates` | The Decision card's folder row across MAIL-08's four outcomes |
| `Dialogs` | The three reasoned actions sharing one shape |

## Structure

One `.record` container — head, state accent, tabs — as on
`Pages/Cases/Details.cshtml`. There is **no action bar**: both decision actions
live in the Decision card beside the rows they change. That is a deliberate
departure from `docs/design/README.md:176` and is recorded as one.

Four tabs: **Message · Attachments *n* · Thread · Case**.

- **Message** — `.split-main`. The letter fills the main column; the Decision
  card sits in the sticky right column, with a Corrections card beneath it once
  the classification has been corrected.
- **Case** — MAIL-09/MAIL-10 association, in one 680px column: search, then
  candidates, then the target you picked.
- **Attachments** and **Thread** keep their current content.

The Decision card carries Classification, Destination, Filed to, Folder and
Decided, plus *Correct classification* and *Move to folder*. Rows and actions
render only when populated and available, so an unclassified message shows
neither Folder nor the move action. `Filed to` is the case link once filed —
**there is no Open case button**.

The subject wraps in the dark band. It previously truncated with an ellipsis,
which read as clipping.

## What is removed

All of it already banned by the design authority:

| Removed | Rule |
| --- | --- |
| `Policy: … version 3`, `Destination policy: … version 1`, `Folder recommendation policy: … version 1` | `README:177` policy keys; `README:171` version integers |
| `Decision version: 1` | `README:171` |
| `Reason: No accepted classification predicate matched…` | `README:431` how-it-works copy |
| `Material evidence` — the `subject.reply-prefix`-style rows | `README:171` raw codes; `README:177` source labels |
| `Read from the message and its attachments` | `README:431` |
| `Recommended Outlook folder: Unavailable — …` and its standalone panel | `README:436` only populated sections render |
| `Latest folder move: Uncertain — …` as a fact row | state belongs on the control, not in prose |
| `Suggested next action` heading and its reason paragraph | `README:431` |
| `This message is not associated with a case. Search for the exact Case/PO before linking it.` | `README:431` |
| `(only when Other is selected)` on two field labels | `README:426` |
| Every `DialogConsequence` sentence, the partial's default, and the `Required.` hint | `README:400` — necessary copy is an approved list, and it is closed |
| The second sentence of the correction notice | `README:431` |

## Two labels that do not exist yet

Both are raw machine text reaching the operator, and both need an
`OperatorLabels` map — tracked as [[MAIL-008]].

1. **The classification value.** `DecisionLabel` renders `MailCategory.Name`,
   which for received families is `new-instruction-received`,
   `in-progress-cases`, `pre-instruction-emails`. `Filed.dc.html` shows
   `New instruction · Inspection` as a **proposal**, not a settled term.
2. **The folder-move reason.** `Moving.dc.html` shows
   `Classified as a new inspection instruction` as a **proposal**; the stored
   `RetainedMailSuggestedMove.Reason` is machine-worded.

## The folder move is a confirmation

`Moving.dc.html` asks for no typed reason. The folder is the one the policy
designated, so the reason is already known and is shown as the value that will
be recorded.

There is **no override to a different folder**, by decision, not omission —
`docs/frd/frd-08-email-mailbox-and-background-processing.md:243`: *"Staff may
confirm only the designated folder from the applicable classification policy. A
different destination requires correction of that classification, not an
arbitrary folder choice."*

## The body rendering

A view change, not a text change. `Pegasus.Core.Intake.StaffForwardBodyCleaner`
already strips leaked `cid:` tokens, drops the CE forwarder's preamble and
signature above the quoted original, and collapses runs of three or more
newlines to two. What reaches the page still has one blank line between every
line of the provider's letter, and `<pre class="mail-body">` with
`white-space: pre-wrap` renders each at full body height.

The design puts the sender on one line with the forwarding route muted beneath,
splits the retained `From: / Sent: / To: / Subject:` block out as a quoted
header, renders the letter as paragraphs (blank line is a paragraph break at
14px, consecutive lines stay tight at 4px) and caps the measure at 68ch.

Suppressing the trailing signature, disclaimer and link footer of the
*original* sender is [[MAIL-007]] — the cleaner does not do it today.

## A note on `.facts`

`.facts` is defined in `site.css` but has no caller in any `.cshtml`. This
design is its first. Its `repeat(auto-fit, minmax(230px, 1fr))` grid splits the
heading away from its list once the container is wide enough, so
`.decision .facts` and `.reason-dialog .facts` pin it to one column.
