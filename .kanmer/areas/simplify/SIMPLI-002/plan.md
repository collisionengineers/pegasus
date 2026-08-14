# Plan — Rewrite AGENTS.md (Stage B, with [[SIMPLI-004]])

1. Rewrite "Planning process": Kanmer board is the queue;
   `docs/capabilities.md` is the roadmap; drop the `NOW.md` queue pointer.
2. Rewrite "Repository task workflow": Take = `take_ticket` (records branch,
   worktree, date, agent) + create worktree from `origin/dev`; keep the
   worktree/plan/PR/review/merge-auth and Git allowed/banned-operation rules
   (those are still correct and load-bearing). Remove NOW.md-specific claim,
   conflict-reapply, and date-bump mechanics.
3. Keep the managed block, "Safety rails", "Product invariants", the filename,
   the symlink, and the `#repository-task-workflow` anchor.
4. Do NOT add a new "simplicity contract" (per simplify.md).

## Acceptance
AGENTS.md coherent + current; claim mechanism is Kanmer; invariants intact.

## Verify
Read-back of AGENTS.md; `git ls-files -s CLAUDE.md` still mode 120000;
`pwsh ./scripts/Test-DocumentationLinks.ps1` green (anchor links resolve);
targeted run of one repo-root-marker test if questioned.

Make sure NOW.md and its documented tasks are all still within Kanmer.

**Held for user review.**

## Update (2026-08-13): read-only Azure policy
Add a "Safety rails" line to AGENTS.md: **read-only Azure checks are fully
permitted** (no per-target approval). Writes, deployments, credential/account,
destructive, and external writes still require explicit approval for exact
targets. The rail defers the approval matrix to
`docs/runbook.md#live-operation-approval-matrix` — coordinate the same carve-out
there so the two agree. (User grant, 2026-08-13.)
