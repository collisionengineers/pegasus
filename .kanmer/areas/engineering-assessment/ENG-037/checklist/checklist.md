# Checklist — ENG-037

- [x] Use invariant parsing and cover accepted/rejected dates across calendars.
- [x] Record focused normalizer/report projection checks and bounded diff.

Root session12906: locked restore and focused build PASS; 78/78 tests PASS.
Independent review and exact merged acceptance PASS. Closeout completed;
claim released after final record readback and exact owned Git cleanup.

## Closeout — ENG-037

- [x] PR merge verified (`gh pr view --json state,mergedAt`)
- [x] proof.md finalised (PR URL + merge date appended)
- [x] Moved to final stage
- [x] Outcome recorded in ticket body (PR link, follow-ups)
- [x] cd out of worktree; `git worktree remove .worktrees/<id>`
- [x] `git branch -d <branch>` (`-D` if squash/rebase-merged)
- [x] `git fetch --prune` + `git worktree prune`
- [x] `take_ticket action: "release"`
