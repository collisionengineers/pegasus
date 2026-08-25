# Checklist — ENG-018

- [x] Remove the Core EVA activation type and gate.
- [x] Remove Infrastructure/Web dependency injection and configuration.
- [x] Remove obsolete Bicep settings.
- [x] Update focused Core and integration tests.
- [x] Add regression coverage for configuration-free Export and absence of legacy GUI wording.
- [x] Update FRD and current-state documentation.
- [x] Run focused verification and canonical Release build/tests.
- [x] Complete the four-lens simplification pass.
- [x] Commit, push, open the PR, and write the implementation report.

## Closeout — ENG-018

- [x] PR merge verified (`gh pr view --json state,mergedAt`)
- [x] proof.md finalised (PR URL + merge date appended)
- [x] Moved to final stage
- [x] Outcome recorded in ticket body (PR link, follow-ups)
- [ ] cd out of worktree; `git worktree remove`
- [x] Task branch removed by GitHub merge
- [ ] `git fetch --prune` + `git worktree prune`
- [ ] `take_ticket action: "release"`
