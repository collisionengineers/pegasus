# Checklist — PLAT-018

- [x] Remove only `queue` from the banned operator-copy enumeration; retain the “queue mechanics” rule and every other banned term.
- [x] Reword the consequence-sentence exception to point exclusively to the closed approved necessary-copy list, without changing that list.
- [x] Inspect the documentation-only diff and verify the retained shell label, mechanics restriction, closed-list wording, and absence of unrelated file changes.
- [x] Record `git diff --check`, the focused authority search, and diff inspection in the post-implementation report/proof after implementation and merge.

## Progress notes

Planning complete; implementation remains a one-file docs-only change.

2026-08-21 — Completed the two authority corrections. `git diff --check` passed; the focused search confirmed the retained “queue mechanics” restriction, the approved `Queues` shell label, and the new closed-list exception.

<!-- kanmer-groom:release-take:PLAT-018:2026-08-25 -->
### Board-hygiene claim release — 2026-08-25

Audit record written before releasing this completed ticket's stale take. Previous assignee: `codex-mcp-client`; branch: `task/plat-018`; worktree: `../pegasus-worktrees/plat-018`; taken at: `2026-08-21T14:01:07.563Z`. The branch and worktree coordinates are preserved here; this groom does not delete either.
