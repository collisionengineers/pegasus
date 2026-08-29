## 2026-08-29 — round-2 remediation of the adversarial verification

Commit `99483f55` pushed to `task/plat-053-external-work-vocabulary`; PR #613
body rewritten (via `gh api -X PATCH` — `gh pr edit` fails on this machine
with `authentication token is missing required scopes [read:project]`).

All seven findings disposed in the `plan` doc under "Review findings —
dispositions (round 2)": five fixed, two deferred to created tickets
([[PLAT-056]] ten remaining files, [[PLAT-057]] EfEvaSubmissionWorkStore
coverage). Nothing rejected, nothing risk-accepted, nothing silenced.

The verifier was right on every finding I could check. Its two honesty
callouts were fixed at source rather than reworded: the "mechanical
substitution" claim is now true of all three files because the restructure
was withdrawn, and the `EvaSubmissionPersistenceTests` citation is struck
from `plan`, `files` and `post-implementation-report`.

Real numbers this round: build exit 0 / 0 warnings / 0 errors; focused
tests 138 passed, 0 failed, 1 skipped across 14 classes (101/0/1 + 37/0/0),
both runs exit 0. Widened from the 3 classes cited in round 1 to every
non-Browser class touching `ExternalWorkItems` or `IEvaSubmissionQueries`.

Ticket left in `review` and `proof` left unwritten, per lane instructions.

## 2026-08-29 — proof written against merged `dev` `b92cb9a7`, moved to Done

Independent verification, no source-tree edits. Merge `940062c2` (PR #613);
`8a358ad4` and `99483f55` both ancestors of `b92cb9a7`.

Decisive check for behaviour preservation: inlining each
`ExternalWorkStatePersistence.X` reference back to its declared literal makes
all three changed files identical to `940062c2^1` (whitespace-normalised) —
so the persisted strings and the control flow are provably unchanged, rather
than asserted.

One record correction found: `files`, `plan`, `post-implementation-report`
and PLAT-056's body all say "ten further Infrastructure classes" but each
enumerates nine. The enumeration is correct and complete; only the count word
is wrong. Recorded in the proof's Outstanding section; correcting it belongs
to PLAT-056, not to this closed ticket.

## 2026-08-29 — Audited under the strict rule 14 (D20/D21) and KEPT in Done

An independent GPT-5.6 audit flagged this ticket, and the adjudication rejected the
flag: `CLEAR_KEEP`. This ticket became the worked example the other adjudications
cite for the scope rule.

Reason: PLAT-053 is a behaviour-preserving constants extraction that ships no
capability and names none. Its What gives the whole boundary — the persisted
`ExternalWorkItems.State` words spelled as literals in three Infrastructure classes,
given one internal owner, with the three callers reading it; "Behaviour-preserving;
no schema change." Owns names exactly three files, and there is no Verification
section.

The only new code is `src/Pegasus.Infrastructure/Persistence/ExternalWorkStatePersistence.cs`
— an internal static class of six `const string` fields and nothing else (the
parse/format methods the first pass added were withdrawn in `99483f55`). It has 43
references across three files, and at least two of the three readers are
production-reachable:

1. `EfExternalWorkStore.cs` (24 references) — registered unconditionally at
   `src/Pegasus.Infrastructure/DependencyInjection.cs:217-225` behind no feature
   flag, reached in production by `src/Pegasus.Worker/IntakeFunctions.cs:20`
   (`PendingWorkRecoveryFunction`, TimerTrigger) and `:48` (`UnifiedWorkFunction`,
   QueueTrigger on `intake-work`). No gate anywhere on that path.
2. `EfEvaSubmissionQueries.GetActivityAsync` (2 references) —
   `src/Pegasus.Core/Operations/ServiceHealth.cs:376` →
   `src/Pegasus.Web/Pages/Operations/Index.cshtml.cs:78`, behind the OPEN
   `Features:AutomationMcp` gate (`docs/operations.md:122,134-139`).

The audit's reversal rested on `EfEvaSubmissionWorkStore`, whose only producer is
closed by the per-Principal `EvaAutomaticSubmission` toggle
(`docs/operations.md:360-362`, "no Principal has either EVA toggle on"). That store
is not PLAT-053's code and not its capability: `git log --diff-filter=A` on the file
returns exactly one commit, `09beefef` "EXT-04: direct EVA API submission of a case
and its images (TICK-077)". PLAT-053's entire net diff to it is six
literal-for-constant substitutions with control flow, the unknown-state guard and the
`_ => pending` catch-all restored to match `dev` exactly.
