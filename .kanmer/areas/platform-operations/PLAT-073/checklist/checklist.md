# Checklist — PLAT-073

- [x] Node 24 default and Linux-only PATH corrected
- [x] Exact .NET and Offline/Cloud prerequisites installed
- [x] Pinned npm, SQL image, certificate and Chromium payload initialized
- [x] Offline Doctor passes
- [x] Cloud Doctor passes without authentication
- [x] Locked restore/build/non-Corpus test passes in documented split lanes
- [x] Browser lane passes
- [x] Kanmer v0.4.1 managed files reconciled
- [x] Any repair guidance correction is execution-backed
- [x] Simplification pass recorded
- [x] Task branch pushed and PR opened to dev

---

## Closeout — PLAT-073

- [x] PR merge verified (`gh pr view --json state,mergedAt`)
- [x] proof.md finalised (PR URL + merge date appended)
- [x] Moved to final stage
- [x] Outcome recorded in ticket body (PR link, follow-ups)
- [ ] cd out of worktree; `git worktree remove .worktrees/plat-073`
- [ ] `git branch -d PLAT-073-wsl-toolchain` (`-D` if squash/rebase-merged)
- [ ] `git fetch --prune` + `git worktree prune`
- [ ] `take_ticket action: "release"`
