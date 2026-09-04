# Plan — DELIV-046: Restore main as an ancestor of dev

## Objective

Restore the append-only release invariant by making the two authorised main-only commits reachable from dev through one reviewed merge commit.

## Starting state

Local dev and origin/dev are at 8f3d09602540346caaca5b7f3e26245b72eb3575. Local main and origin/main are at 32f8679d3695e0dcab8f310a1c20f8b129d20190. origin/main...origin/dev reports 2 left and 54 right. The main-only commits are f1d6234af (four authorised test artifacts) and merge commit 32f8679d3. The merge base is 07ac7f1be. Evidence: read-only Git inspection on 2026-09-04 and EPIC-013/context.md.

## Governing docs

No product governing document changes. docs/engineering.md owns branch delivery procedure and must record this exact one-use remediation. AGENTS.md must carry the matching repository workflow convention.

## Required changes

Create a task branch from origin/dev, document a one-use DELIV-046 main-to-dev reconciliation exception, merge origin/main without rewriting either history, and deliver through a merge-commit PR to dev. Preserve the authorised test artifacts byte-for-byte.

## Expected files

| Action | Repo-root-relative path | Responsibility |
| --- | --- | --- |
| Modify | `docs/engineering.md` | Record the bounded one-use reconciliation. |
| Modify | `AGENTS.md` | Keep repository workflow instructions aligned. |
| Merge | `tests/Pegasus-Test-Logs/basic-intake-match-testing/test-cases/test1/**` | Preserve the authorised main-only evidence exactly. |

## Do not modify

`.github/**`, application code, migrations, test assertions, corpus, shared branches directly, and the four main-only artifact bytes.

## Constraints

No rebase, reset, force push, squash merge, or direct dev update. The task PR must use a merge commit so origin/main remains an ancestor. An independent reviewer owns merge authority.

## Ordered steps

### Step 1 — Record the bounded exception
- Preconditions: Ticket is taken on its recorded branch/worktree from origin/dev.
- Files: `docs/engineering.md`, `AGENTS.md`.
- Change: Add a one-use DELIV-046 exception allowing origin/main to merge into this task branch only.
- Preserved behaviour: Normal tasks still branch from and merge to dev; shared refs remain append-only.
- Forbidden: General or recurring main-to-dev synchronization.
- Tests: Documentation link and Markdown checks.
- Commands: `pwsh ./scripts/Test-DocumentationLinks.ps1`; `pwsh ./scripts/Test-MarkdownPlacement.ps1`.
- Expected output: Both exit zero.
- Done when: Both authorities describe the same exact exception.
- Deviation stop: Any conflict with protected managed instructions.

### Step 2 — Merge the authorised main history
- Preconditions: Step 1 is complete and remote SHAs still match Starting state.
- Files: `tests/Pegasus-Test-Logs/basic-intake-match-testing/test-cases/test1/**`.
- Change: Merge origin/main into the task branch with a merge commit, resolving no artifact content.
- Preserved behaviour: All 54 dev-only commits and both main-only commits remain reachable.
- Forbidden: Squash, rebase, cherry-pick reconstruction, force update, or content deletion.
- Negative cases: Stop if either remote SHA moved or Git reports a merge conflict.
- Tests: Git ancestry and exact main-tree object comparisons.
- Commands: `git merge-base --is-ancestor origin/main HEAD`; compare the four blob ids with origin/main.
- Expected output: Ancestor check exits zero and blob ids match.
- Done when: Task HEAD contains both histories without conflict.
- Deviation stop: Any changed remote or merge conflict.

### Step 3 — Verify and open the PR
- Preconditions: Task branch is clean with both steps committed.
- Files: `AGENTS.md`, `docs/engineering.md`, `tests/Pegasus-Test-Logs/basic-intake-match-testing/test-cases/test1/**`.
- Change: Run repository documentation and branch guards, push only the task branch, and open a PR targeting dev that requires merge-commit integration.
- Preserved behaviour: Main and dev remain unchanged until independent review.
- Forbidden: Self-review, self-merge, or starting PLAT-073.
- Tests: Focused checks plus PR CI.
- Commands: Git ancestry/blob checks and canonical documentation tests.
- Expected output: Local checks pass and PR is open against dev.
- Done when: Post-implementation report names commits, checks and PR.
- Deviation stop: Failed test, non-merge PR policy, or concurrent remote movement.

## Acceptance checks

- Task HEAD contains origin/main and the recorded origin/dev base as ancestors.
- The authorised artifacts are byte-identical to origin/main.
- The PR targets dev and is configured for a merge commit.
- After independent merge, origin/main is an ancestor of origin/dev and left/right is 0/N.

## Commands

Run Git ancestry/blob checks, `pwsh ./scripts/Test-DocumentationLinks.ps1`, `pwsh ./scripts/Test-MarkdownPlacement.ps1`, and the PR's required CI.

## Failure and deviation rules

Stop on remote movement, merge conflict, changed artifact bytes, failed checks, scope expansion, or inability to obtain independent merge review.

## Stop condition

Stop with the PR open in Review. Do not merge it or start PLAT-073.

## Simplification pass — 2026-09-04

n/a — documentation-only ancestry repair. The diff contains only the exact bounded exception in the two owning guidance files; no code abstraction or duplicate implementation was introduced.
