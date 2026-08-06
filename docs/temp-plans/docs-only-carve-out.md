# docs-only-carve-out

Amend the multi-agent task workflow with the operator-decided docs-only
carve-out (2026-08-06): a docs-only task skips the transient plan file, and
its two-question independent review runs against the PR diff instead.

## Boundary (operator-decided)

A task is docs-only when every path in its final PR diff is a Markdown
file outside `src/`, `tests/`, `infra/`, and `scripts/`. A task that stops
qualifying mid-work writes the plan file before review, as today.

## What changes

- `docs/adr/0017-multi-agent-task-workflow.md` gains a dated addendum
  (2026-08-06) recording the carve-out; the original decision clauses are
  untouched.
- `docs/adr/README.md`: the ADR-0017 index row records the addendum.
- `docs/engineering.md` task workflow: step 4 gains the carve-out and its
  boundary; step 6 gains the diff-based form of the two review questions;
  step 7 notes a docs-only task has no plan file to delete, so its
  release step is worktree and branch cleanup only. Step 1's
  slug-collision check is deliberately unchanged — it stays correct for
  any plan file that does exist.
- `docs/temp-plans/README.md`: the contract gains the exemption.
- `NOW.md`: this PR removes its own claim line.

## How verified

Docs-only change set: the `repository-check` documentation job (relative
link resolution) is the CI gate; build/test lanes are path-skipped by
design. Manual check before the PR: the four amended texts state the same
boundary and the same two questions, and the ADR body above the addendum
is byte-identical to its state at `origin/dev` (immutability). Review is
the standard two-question review against this plan.
