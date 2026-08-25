# Checklist — PR-032

- [x] Add the null-dossier unavailable renderer with no new behavior owner or control.
- [x] Add exact authenticated null-classification unavailable-state Web coverage.
- [x] Run Release build, focused Web tests, and the four-lens diff pass.
- [x] Update PIRs, commit/push PR #474, record traceability, and move PR-032 to Review.

## Progress notes

Shared fix branch/worktree: task/tick-047-mail-05-folder-recommendation / ../pegasus-worktrees/tick-047. No external writes.

Release build passed with 0 warnings/errors; MailWorkspaceWebTests passed 17/17. Four lenses: existing Core result reused; no partial or mapping copy; Razor-only constant work; no lower-layer or write-path changes.

Committed and pushed 4bc3f158 to PR #474; PIRs and traceability updated.

<!-- kanmer-groom:release-take:PR-032:2026-08-25 -->
### Board-hygiene claim release — 2026-08-25

Audit record written before releasing this completed ticket's stale take. Previous assignee: `codex-mcp-client`; branch: `task/tick-047-mail-05-folder-recommendation`; worktree: `../pegasus-worktrees/tick-047`; taken at: `2026-08-20T12:27:50.353Z`. The branch and worktree coordinates are preserved here; this groom does not delete either.
