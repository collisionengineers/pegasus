# Post-implementation report — DELIV-035

Written retrospectively during the EPIC-011 closeout board reconciliation on
2026-08-29. The ticket's work was implemented and merged by the lane that
carried it, but no board documents were written at the time and the record was
left in `implementing`. This report records what shipped, from the merged
result, so the stage record matches reality.

## What shipped

PR [#625](https://github.com/collisionengineers/pegasus/pull/625) —
"DELIV-035: fix the dev build break from the QueuedIntakeStatus arity change" —
merged as `55e23b02` at 2026-08-29 17:12:12 +0100 from branch
`task/deliv-035-queued-status-arity` (head `31ec8898`).

The whole change is one deleted line:

```diff
 tests/Pegasus.Core.Tests/ProviderApi/ProviderSubmissionTests.cs
@@ -281,7 +281,6 @@
             Now,
             QueuedIntakeStatusKind.Complete,
             ProcessedReceiptId: null,
-            CaseId: null,
             FailureCode: null);
```

`git show --stat 55e23b02` — **1 file changed, 1 deletion(-)**. No source file
under `src/` was touched.

## The approach the ticket specified was followed exactly

The ticket directed: delete the argument, do **not** re-add `CaseId` to
`QueuedIntakeStatus`, because INTK-001 removed it deliberately and its
case-id resolution is the single owner now.

That is what happened. On merged `dev` the record is:

```
src/Pegasus.Core/Intake/DurableIntake.cs:93
public sealed record QueuedIntakeStatus(
    Guid StagedReceiptId,
    string SourceFileName,
    DateTimeOffset ReceivedAtUtc,
    QueuedIntakeStatusKind Status,
    Guid? ProcessedReceiptId,
    string? FailureCode,
    DateTimeOffset? RetryDueAtUtc = null);
```

No `CaseId` member was restored. The duplication INTK-001 existed to remove
stays removed.

## No assertion was weakened, skipped or deleted

The deleted line is a constructor argument set to `null`, not an assertion.
The test's assertions are untouched and still check `paused.Status`. AGENTS.md
rule 19 is satisfied by construction: the diff removes no assertion.

## Scope discipline

The ticket is a build fix and shipped no feature (AGENTS.md rule 3). It stayed
inside the one test file that failed to compile. The simplification pass is
recorded as **n/a — a single-line deletion in one test file**; there is no
diff to simplify.

## Cause, recorded once so no lane re-derives it

This was a merge-order break, not a defect in either lane. INTK-001 (#620,
`6c648c59`) narrowed the record while TICK-058 (#594, `63009b02`) added a test
constructing it with the removed member. Both were green on their own CI
because neither run had seen the other, and git had nothing to report because
the files do not overlap. The lesson is already recorded in the EPIC-011
closeout decisions as the merge-loop rule: *when two PRs in a batch touch
related Core types, the second gets a `dev` merge and a fresh CI pass before it
goes in — or they go in one at a time.*
