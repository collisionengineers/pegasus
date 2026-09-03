# Plan — KANMER-011: Fix broken greenfield.md link in kanmer-setup SKILL.md

## Objective
The `documentation` CI job is green on `dev` again: no copied Kanmer skill links outside the repository.

## Starting state
`.opencode/skills/kanmer-setup/SKILL.md:169` and `.agents/skills/kanmer-setup/SKILL.md:169` contain `[`docs/manual/greenfield.md`](../../../../docs/manual/greenfield.md)`; run 33743975493 (`push` main, 2026-09-03T10:22Z) reports `BROKEN .opencode/skills/kanmer-setup/SKILL.md: ../../../../docs/manual/greenfield.md`.

## Governing docs
None modified. Upstream: Kanmer CORE-139 (PR collisionengineers/kanmer#314) makes the same edit in the shipped skill.

## Required changes
Replace the linked reference with plain text in both copies (same wording as upstream): "Use the Kanmer manual's greenfield chapter (`docs/manual/greenfield.md` in the Kanmer repository, or the in-app manual) to choose the appropriate initial depth and keep the first horizon bounded. This skill is copied into other repositories, so it never links into the Kanmer tree."

## Expected files
| Action | Path | Responsibility |
|---|---|---|
| Modify | `.opencode/skills/kanmer-setup/SKILL.md` | remove the escaping link |
| Modify | `.agents/skills/kanmer-setup/SKILL.md` | same |

## Do not modify
`AGENTS.md`, `.mcp.json`, any other skill file, application code.

## Ordered steps
1. Edit both files identically.
2. `pwsh ./scripts/Test-DocumentationLinks.ps1` from the worktree: exit 0.
3. Commit, push `task/kanmer-011-skill-link`, PR to `dev`, independent review, merge when CI green.

## Acceptance checks
- `documentation` job green on the PR head.
- `git grep -n "../../../../docs/manual"` returns nothing.

## Simplification pass
n/a — docs-only.

## Stop condition
PR open and reviewed; merge into `dev` by the independent reviewer after green CI.
