# Plan — DELIV-049: one documentation authority

## Objective

Remove the superseded root work index and current-looking stream instructions.

## Starting state

At dev 3da60bd0c270111d5168dc17246dc831882108ea, commit 817a961c1 added
NOW.md plus explicit allowances in AGENTS, docs/index and Markdown placement.
PR674 integrated the streams; PR675 remains open. Current user permits merge,
deployment and removal of competing documentation. Shared checkout is dirty;
use a fresh ticket worktree. No runtime code changes are required.

## Governing docs

Meets docs/index.md's one-file-per-question authority rule and current user
instruction. Work state belongs to Kanmer. Preserve operator business facts;
label the historical three-stream execution boundary as superseded only.

## Required changes

Delete NOW.md, remove its special routing/allowance, and make ordinary task
workflow current again. Replace the long expired three-stream exception in
AGENTS with a concise historical pointer to its tickets and current EPIC014
scope/authority. Mark the old open-unmerged operator delivery note historical.
Cross-OS mechanisms and their docs are owned by DELIV-048; this ticket records
that current request and does not falsely claim they are implemented.

## Expected files

- NOW.md (delete).
- AGENTS.md (remove duplicate index allowance; retire expired workflow).
- docs/index.md (remove duplicate navigation and allowance).
- docs/operator-notes.md (historical execution-status annotation only).
- scripts/Test-MarkdownPlacement.ps1 (remove root-index exception).

## Do not modify

Product code, tests, migrations, runtime configuration, existing operator
business statements, supplied reference data or other agents' worktrees.

## Constraints

No new Markdown source of truth or new test suite. Preserve original text in
Git history. This current user request supplies the business/workflow approval.

## Ordered steps

1. Remove NOW.md and its navigation/placement exceptions.
2. Retire obsolete three-stream instructions and annotate historical delivery.
3. Run whitespace/placement checks and inspect reference absence; request an
   independent docs review. No dotnet build for documentation-only edits.

## Acceptance checks

No active NOW.md reference; no current command to leave all v1 PRs open.
Kanmer/current-state/behavior documentation owners remain distinct. The
Markdown placement guard still rejects unapproved new root Markdown files.

## Commands

In the ticket worktree: git diff --check; git grep -n NOW.md -- AGENTS.md docs
scripts (exit 1 expected); pwsh -NoProfile -File scripts/Test-MarkdownPlacement.ps1
-Base 3da60bd0c270111d5168dc17246dc831882108ea -Head HEAD after commit.
Use the existing placement test script if available; otherwise inspect the
removed exception directly. No full solution or integration build.

## Failure and deviation rules

Retain failed checks. Stop the affected edit for a material business-meaning
conflict; do not silently delete protected statements or broaden scope.

## Stop condition

Commit the focused correction and return for independent review. Open the PR
with Kanmer footer after focused checks; author does not merge.

## Simplification pass — 7 September 2026

Documentation removal plus deletion of one special-case guard condition.
Reuses the existing path matcher; no new abstraction or second source. The
independent reviewer assesses the final five-file diff and dispositions.
