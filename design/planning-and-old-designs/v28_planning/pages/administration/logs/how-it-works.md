Read from the live source on 18 September 2026.

## What this page does not show

- No lede under either tab; the tab name and the table caption are the only
  words above the data.
- The Action logs tab's "Recorded counts and processing times" panel is a
  `<details>` — collapsed by default, not a rendered grid on first load.
- The intake-log drawer renders only when a receipt id resolves
  (`detail is not null`); each of its three retry/re-evaluate actions is
  present only when `Model...Actions` says that action currently applies to
  this row's state.

## Source table

| File | Owns |
| --- | --- |
| `Pages/Administration/Logs.cshtml` | Both tabs, the intake figures, both filter forms, both tables, the drawer |
| `Pages/Administration/Logs.cshtml.cs` | `IsIntakeTab`, `IntakeLog`, `IntakeDetail`, `Result` (action log), `ReevaluateIntake`/`RetryIntakeAllocation`/`RetryIntakeOcr` handlers |
| `Presentation/OperatorLabels.cs` (`IntakeLog` class) | `Outcome()`, `OutcomeTone()`, `Source()`, `BecameHref()`, `Attempts()` |
| `Presentation/OperatorLabels.cs` (`ActionLogActorTypes`) | Staff / AI (automation) / Pegasus (system) |

## Behaviours

### Two tabs, one page

`?tab=intake` selects the Intake log tab; its absence selects Action logs.
Both tabs share one page header and one `_AdminNav`; nothing else is shared
between their content.

### Action logs tab

Filters: Keywords, Activity, Person (every staff account plus, when
present, the automation actor), Actor type (Staff / AI (automation) /
Pegasus (system)), Case/reference, Outcome, From, To. Table: Time (sort
link), Actor (an "AI" chip precedes the actor label when
`Model.IsAiActor(row)`), Area, Action, Reference (a link when the row names
an AI job's own record), Result. A collapsed details panel below lists
thirteen further recorded counts (mailbox failures, mailbox freshness,
oldest pending Box upload, poisoned inbox messages, unknown sends, oldest
pending AI job, and seven cache figures).

### Intake log tab

Two head figures: Failed intake (a link into Operations, since every
retryable intake failure is listed there with its own retry) and Oldest
pending intake. Filters: Search (reference/registration/claim
number/sender/file name), Outcome (all nine `IntakeLogOutcome` values),
Source (all four `IntakeSourceChannel` values), Principal, From, To. Table:
Received, Source, Item, Outcome (chip, plus a failure reason line for
Closed/Could not be read/Processing failed), Became (the reference this item
produced, or an em dash), Attempts, Open.

### Intake log drawer

Opens beside the table (`aria-modal="false"`, not a blocking dialog):
Received/Source/Outcome/Became/Attempts facts; a Retained original section
(Open file, and Open message when the item came by mailbox); a Processing
evidence list built from the receipt's decision/failure reason, any
registration reading, any suggested field value, and any recorded evidence
signal; and up to three reason-gated actions in the foot — Re-evaluate with
current policy, Retry allocation, Retry OCR — each a one-field form (a
required Reason) rather than a confirmation dialog.

## Things the FRD does not settle

- The word "intake" appears in shipped, operator-facing copy beyond the one
  permitted tab name (see this area's README Notes, sign-off item B); whether
  that is an accepted, tracked exception or an unswept defect is not settled
  by anything under `Pages/Administration/`.
