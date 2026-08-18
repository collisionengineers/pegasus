# Plan — DELIV-005: Remove Markdown-placement CI gate

## Approach

Delete only the `Markdown placement` step from the existing
`documentation` job in `.github/workflows/ci.yml`. This preserves the
independent placement regression test and documentation-link validation, while
removing the diff-path policy that blocks release PR #400.

## Governing docs

This is a workflow-maintenance change, not product behaviour or a durable
architecture decision. No PRD, FRD, or ADR applies. It follows `AGENTS.md`'s
repository task workflow and changes the existing CI convention in place.

## Steps

1. From a fresh `origin/dev` worktree, remove the Markdown-placement step and
   its SHA environment/run script from `.github/workflows/ci.yml`.
2. Inspect the resulting diff to confirm the retained regression and link
   validation steps are unchanged.
3. Run YAML parse/structural checks and the two retained documentation scripts.
4. Run `git diff --check`, commit the one-file change, and record the
   docs-only simplification disposition.
5. Open a PR to `dev`; independent review must confirm no unintended CI
   scope was removed.

## Verification

- `rg` output shows the documentation job retains
  `Markdown placement regression tests` and `Documentation links`, but not
  the removed `Markdown placement` step.
- The two retained scripts pass locally.
- The PR CI no longer executes the removed gate; once merged into `dev`,
  release PR #400 is re-evaluated without this failure.

## Risks

Removing the gate deliberately stops CI from enforcing Markdown placement. The
scope is kept narrow by retaining the regression test and link checker, and by
leaving the policy script itself unchanged for potential future use.

## Simplification pass — 2026-08-18

n/a — docs-only workflow configuration. The change removes one isolated CI step and reuses the existing documentation job; it adds no mechanism, abstraction, or duplicate policy.
