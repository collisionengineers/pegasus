# Post-implementation report — the timeline fix

Branch `task/case-017-notes-timeline`, PR #513, from `origin/dev` at `4257b841`.

## What was wrong

The original implementation wrote an operator note to `CaseHistory`. The Notes
tab reads `CaseWorkflowEvents` (`EfCaseQueryStore.cs:181`). Two different tables
with different purposes, so every note was persisted, the page returned *"The
note was added."*, and the timeline stayed empty with the count at `0`.

Nothing threw. CI was green. The Core command's tests drive a `RecordingStore`
fake, and no test asserted the note came back through the query the page uses —
a fake at the port boundary proves the command and never the wiring.

## The change

| File | |
| --- | --- |
| `EfCaseNoteStore.cs` | Writes `CaseWorkflowEventEntity`, matching every other writer of that timeline. Replay protection moves to the same table so the operation key still guards a resubmitted form. Before and after versions are equal and the workflow row is untouched — a note records itself and changes nothing. |
| `tests/…/CaseNotePersistenceTests.cs` | **New.** Two facts against real SQL. |

No schema change: both runtime roles already hold `SELECT, INSERT` on
`CaseWorkflowEvents` in the least-privilege baseline — checked before writing
the fix, not after, given [[DOCS-008]].

## Tests

- `AnOperatorNoteLandsOnTheTimelineTheNotesTabReads` — asserts the row is in
  `CaseWorkflowEvents` with its actor, kind and time, versions equal, workflow
  untouched, **and that `CaseHistory` is empty**. Fails on both halves against
  the previous implementation.
- `ResubmittingTheSameNoteFormLeavesOneEntry` — replay.

Both pass locally (2/2, 35s). CI green on PR #513 after one unrelated
infrastructure flake (`UnidentifiedPersistenceTests`, SQL post-login connection
timeout) was rerun.

## Simplification pass — 2026-08-22

The fix **removes** a storage route rather than adding one: the timeline now has
a single writer shape shared with `EfCaseTaskStore`, `EfCaseWorkflowStore`,
`EfCaseDataStore`, `EfCaseAssessmentStore` and `EfExternalWorkStore`. That is the
"one list per concept" rail applied to a table rather than a label. No other
findings on a two-file diff; nothing left unapplied.
