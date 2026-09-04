# Checklist — UIIMP-016

- [x] Step 1 — Governing evidence contract names Chromium automation and explicit exclusions
- [x] Step 2 — Runbook and operations remove the Windows-only release gate
- [x] Step 3 — Browser and documentation verification pass
- [x] Step 3 — Docs-only simplification disposition recorded
- [x] Step 3 — Branch pushed and PR opened to dev

## Closeout — UIIMP-016

- [x] PR merge verified (`gh pr view --json state,mergedAt`)
- [x] proof.md finalised (PR URL + merge date appended)
- [x] Moved to final stage
- [x] Outcome recorded in ticket body (PR link, follow-ups)
- [x] cd out of worktree; `git worktree remove .worktrees/uiimp-016`
- [x] `git branch -D UIIMP-016-chromium-accessibility` (squash-merged)
- [x] `git fetch --prune` + `git worktree prune`
- [ ] `take_ticket action: release`
