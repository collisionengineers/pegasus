# Checklist — ENG-037

- [x] Use invariant parsing and cover accepted/rejected dates across calendars.
- [x] Record focused normalizer/report projection checks and bounded diff.

Root session12906: locked restore and focused build PASS; 78/78 tests PASS.
Independent review, exact merged acceptance and closeout remain outstanding.

## Closeout — ENG-037

- [ ] PR merge verified (`gh pr view --json state,mergedAt`)
- [ ] proof.md finalised (PR URL + merge date appended)
- [ ] Moved to final stage
- [ ] Outcome recorded in ticket body (PR link, follow-ups)
- [ ] cd out of worktree; `git worktree remove .worktrees/<id>`
- [ ] `git branch -d <branch>` (`-D` if squash/rebase-merged)
- [ ] `git fetch --prune` + `git worktree prune`
- [ ] `take_ticket action: "release"`
