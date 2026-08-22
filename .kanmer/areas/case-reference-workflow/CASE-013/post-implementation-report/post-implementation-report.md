# Post-implementation report

**Branch:** `task/qdos26009-operator-fixes` · **PR:** #506 · **Commit:** `ca564ac5`

## What was built

Both questions the ticket posed turned out to have the same answer: nothing was evaluating
readiness for an automatically created case, and the rule that should have was never called.

1. `AllocateIntake.AttemptAutomaticAsync` records `AutomaticCompleteness` — instruction and
   images complete, staff flags false — instead of a hardcoded all-false.
2. `CaseCompletenessPolicy` waives the staff-review requirements for an automatically
   definitive intake, matching `CaseCompleteness.IsReadyForReview`.
3. `AcceptIntake` decides "automatic" from the actor kind.
4. The custody promotion calls `IsReadyForReview` instead of restating it.

## The finding that matters more than the diff

`CaseCompleteness.IsReadyForReview(bool automaticallyDefinitive)` existed, expressed the
right rule, and **had no callers anywhere in the solution.** Two Infrastructure copies had
been written instead, both stricter than the owner. That is how a case could hold every
field the operator could see and still be told it was incomplete.

The fix is mostly deletion of duplicated logic. Step 4 is behaviour-preserving on its own —
`IsReadyForReview(automaticallyDefinitive: false)` is exactly the condition it replaced —
and its value is that Core now owns the rule with two callers rather than zero.

## Departure from the plan

The plan proposed evaluating images against the workflow configuration. Not needed: the
live configuration requires all four, and the waiver is about *who can satisfy* the staff
half, not about lowering the bar. Adding a configuration branch would have been machinery
that changed no outcome.

## Evidence

- `Pegasus.Core.Tests` — 916 passed, 4 new in `AutomaticCaseReadinessTests`, including one
  that asserts the readiness rule and the acceptance policy agree on the waiver
- `Pegasus.ArchitectureTests` — 99 passed
- Full solution builds clean

## Not claimed

QDOS26009 itself is not fixed by this. It was allocated under the old rule and its flags
are already stored false; this changes what *new* cases record. Whether the live shape
reaches Review is Phase 6, and a corrective path for the existing case is not in scope here.

## Deferred, and named

Naming *which* evidence is missing when a case genuinely is incomplete — the operator asked
for it, it is a separate UI change, and it is only worth doing now that the flags mean
something.
