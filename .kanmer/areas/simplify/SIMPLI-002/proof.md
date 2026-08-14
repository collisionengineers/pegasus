# Proof — Rewrite AGENTS.md (PR #374)

`AGENTS.md` rewritten and shipped in PR #374:
- Added the **Documentation model** section: PRD/FRD/ADR definitions, the routing
  table (where to write / where to send an agent), ADR authoring conventions
  (stable IDs, YAML frontmatter, one-decision, supersede-don't-renumber), and
  new-Markdown placement.
- Rewrote **Planning process** + **Repository task workflow**: the claimable unit
  is now a Kanmer ticket taken via `take_ticket`; dropped the NOW.md claim-line /
  date-bump mechanics; kept worktree/plan/review/merge-auth + Git safety rules.
- Added the **read-only-Azure-permitted** safety rail (writes/deploys/credential/
  destructive/external still need exact-target approval) and the **commit-freely**
  rule beside the merge rule.
- Preserved: the `kanmer:instructions` managed block, the `CLAUDE.md` symlink, the
  filename (7 integration tests use it as the repo-root marker), and the
  `#repository-task-workflow` anchor.

Verified: `pwsh ./scripts/Test-DocumentationLinks.ps1` green (118 files);
independent review confirmed the invariants intact.
