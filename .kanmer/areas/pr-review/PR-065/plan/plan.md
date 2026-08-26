# Plan

## Approach

Delete the three-line paragraph that points to the nonexistent greenfield manual. The following numbered workflow already owns the setup guidance, so replacement prose or a new documentation tree would duplicate instructions.

## Steps

1. Create the PR-065 task worktree from current `origin/dev` and record the claim.
2. Remove only the broken reference paragraph from `.grok/skills/kanmer-setup/SKILL.md`.
3. Run `pwsh -File scripts/Test-DocumentationLinks.ps1` and `git diff --check`.
4. Confirm the diff is limited to the intended deletion, record the implementation report, commit, push, and open a PR to `dev`.

## Governing docs

No PRD, FRD, or ADR governs this repository-documentation defect. The ticket brief and repository workflow are authoritative.

## Alternatives rejected

- Adding `docs/manual/greenfield.md`: unrelated documentation scope solely to satisfy a link.
- Redirecting to a loosely related asset: would misstate what the target document provides.

## Proof

The documentation-link script exits 0 and the PR's documentation check passes. PR #560 can then incorporate the landed correction and rerun its inherited failing lane.

## Simplification pass — 2026-08-26

n/a — docs-only. The final diff deletes only the invalid reference paragraph; no replacement abstraction, compatibility path, or documentation tree was added.
