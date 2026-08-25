# Checklist — PR-044

- [x] Add bounded fresh-context Pending→Uncertain cancellation handoff.
- [x] Preserve the original caller cancellation after the durable handoff.
- [x] Prove cancellation during provider move recovers by same key and blocks new keys until resolved.
- [x] Prove cancellation during Success save recovers by same key and never duplicates the move.
- [x] Run focused/proportional verification and four simplification lenses.
- [x] Update PR-044/TICK-049 reports and traceability, push, and leave Review.

<!-- kanmer-groom:release-take:PR-044:2026-08-25 -->
### Board-hygiene claim release — 2026-08-25

Audit record written before releasing this completed ticket's stale take. Previous assignee: `codex-mcp-client`; branch: `task/tick-049-mail-07-confirmed-folder-move`; worktree: `../pegasus-worktrees/tick-049`; taken at: `2026-08-20T16:52:32.882Z`. The branch and worktree coordinates are preserved here; this groom does not delete either.
