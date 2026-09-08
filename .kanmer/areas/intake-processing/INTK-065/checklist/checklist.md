# Checklist — INTK-065

- [x] Step 1 — Correct the five current inventory declarations and Core expectations; relocate README.md and qdos.md from their starting bytes to principal-profiles; add only the planned historical-v1/current-source clarification to README, keep qdos.md byte-identical, and repair only the matching docs index link. Do not execute commands or alter JSON.
- [x] Step 2 — Extend only the existing Markdown-placement matcher and its existing regression for `docs/principal-profiles`, add the required AGENTS documentation-placement convention, and preserve historical old-path audit references. Do not run commands.
- [x] Step 3 — Under root's verifier slot, rematerialize final generator output and run the authorized focused checks. Final generator output, structural/deterministic comparison, focused tests, documentation links and Markdown placement all passed; materialization 0 remains retained as superseded rather than final accepted evidence.
- [x] Step 4 — Ran `git diff --check`, recorded the report, committed and pushed the declared paths as `5e0aeb47b9cb87258e12f66967e1efee4743b3f3`, and opened draft PR #706 targeting dev for independent review.

---

## Closeout — INTK-065

- [x] PR merge verified (`gh pr view --json state,mergedAt`)
- [x] proof.md finalised (PR URL + merge date confirmed)
- [x] Moved to final stage
- [x] Outcome recorded in ticket body (PR link, follow-ups)
- [x] Pre-merge refresh harness and focused TRX retained with matching SHA-256 values
- [x] cd out of worktree; `git worktree remove .worktrees/intk-065`
- [x] `git branch -D INTK-065-principal-evidence-inventory` (squash-merged)
- [x] `git fetch --prune` + `git worktree prune`
- [x] `git push origin --delete INTK-065-principal-evidence-inventory`
- [ ] `take_ticket action: "release"`

PR #706 merge and full schema-2 proof were re-read before cleanup. The
pre-merge artifacts were copied outside the disposable worktree and hash-verified;
the recorded clean implementation worktree and feature branches were then
removed. Historical failures, erratum and original-input regeneration limitation
remain in the ticket records; unrelated shared-checkout changes were untouched.
