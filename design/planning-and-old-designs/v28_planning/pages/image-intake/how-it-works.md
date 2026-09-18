Read from the live source on 18 September 2026.

## What this page does not show

- No lede or explanatory sentence — the panel headings ("Record",
  "Principal", "Retained files", "Registration reading", "Images",
  "Eligible case candidates", "History") name themselves.
- The list has no Principal filter (`IndexModel.ListsCases("awaiting")` is
  false); the Awaiting-instruction tab has no filter bar at all.
- The record's "Registration reading" table has only three columns (Read,
  Result, Decision) — unlike Unidentified's own reading table, it carries
  no per-row Dismiss action; an Image-initiated Case's readings are
  informational only here.

## Source table

| File | Owns |
| --- | --- |
| `Pages/Cases/Index.cshtml` (tab `awaiting`) | The list: rail count, columns, quick-detail pane, the Attach-to-Case form |
| `Pages/Cases/Index.cshtml.cs` | `ImageRow` (columns/cells/facts), `Columns` for `"awaiting"`, `StageCounts.AwaitingInstruction`, `LoadAwaitingAsync` |
| `Pages/ImageIntake/Details.cshtml` | The record: ribbon, action bar, Record/Principal panels, Retained files, Registration reading, Images gallery, Eligible case candidates, History, the Close dialog |
| `Pages/ImageIntake/Details.cshtml.cs` | `RetainedFiles`, `Suggestions`, `AssociationCandidates`, `PrincipalOptions`, `Triage`, `SourceMessageId`, `AddToCaseHref`, `CustodyLabel` |
| `Pegasus.Web.Presentation.OperatorLabels` | `ImageIntakeLifecycleState`/`ImageIntakeLifecycleStateContinuation`, `ImageChaseState`, `Principal`/`PrincipalNotKnown`, `SourceChannel`, `HistoryEvent` |
| `Pages/Shared/_StatusChip.cshtml` | The ribbon's lifecycle chip tone (see the Notes on the neutral-chip finding) |
| `Pages/Shared/_ImageGallery.cshtml` | The Images gallery tiles, crop badge and tag chips |
| `Pages/Shared/_ReasonDialog.cshtml` | The Close dialog |

## Behaviours

### The list is a tab, not a page

There is no `/ImageIntake` or `/PreCaseImages` list route at all — the
Awaiting-instruction queue is `IndexModel.ImageRow` on the shared Cases
page, columns `Image reference, Registration, Received, Images, Source`,
newest first. Every row's Chip is a fixed "Awaiting instruction" (amber) —
the tab only ever lists records in that one lifecycle state, so no other
chip value can appear there. The quick-detail pane adds `Box` (custody
state) only when the record carries one, and always ends with `Chase`
("Chase due" / "Not yet due" from `ImageIntakeChaseSchedule`).

### Attaching from the list is receipt-bound, not a blank form

The quick-detail pane's "Add to an existing case" posts the selected row's
`receiptId`/`operationId`/version state to `IndexModel.OnPostAttachAsync`;
"Create Case" instead routes to `/Cases/Create` carrying the same receipt.
Neither offers a case-less path — this mirrors the Search page's own
receipt-bound Create Case finding (see the `search` lane's how-it-works).

### One record, three terminal shapes for "what's the Case association"

`Case association` on the Record panel reads three ways from one fact:
a link to the associated Case when `MergedIntoInstructionCase`, or "None —
awaiting definitive instruction" / "None — staff-closed" via
`OperatorLabels.ImageIntakeLifecycleStateContinuation`, which lower-cases
only the label's first character so "Instruction-initiated Case" survives
intact mid-sentence. `Closure reason` only appears as a fact at all when
the state is `StaffClosed`.

### The ribbon chip's tone is a genuine live inconsistency, kept as-is

`OperatorLabels.ImageIntakeLifecycleState` returns "Awaiting definitive
instruction", "Merged into Instruction-initiated Case" or "Staff-closed".
`_StatusChip.cshtml` normalises that text to a lookup key and none of the
three match anything in its tone table, so its `_ => "neutral"` fallback
renders all three as the same grey chip on the record page — genuinely
different from the list tab's own row, which passes an explicit
`ChipTone: "amber"` override with the different wording "Awaiting
instruction". Per the ground rule to capture awkward live behaviour as-is,
both are transcribed faithfully rather than reconciled to one colour or
wording.

### Editing gates the Principal form and the Close action together

`Edit record` (or `Take over`, when another window of the same operator
already holds the edit lease) opens the one edit session that both the
Principal form and, only while the record is `AwaitingInstruction`, the
"Close with reason" control depend on — a heartbeat and an unload beacon
keep the lease alive the same way Triage's record does.

## Things the FRD does not settle

None found in the read source for this page.
