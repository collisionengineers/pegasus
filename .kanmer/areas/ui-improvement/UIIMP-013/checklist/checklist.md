# Checklist — UIIMP-013

- [ ] Step 1 — Fast-forward the clean recorded branch to `origin/dev` and confirm the recovered worktree remains clean.
- [ ] Step 2 — Partition the single existing capture filter into browser and non-browser invocations sharing one once-wiped capture directory.
- [ ] Step 2 — Keep the browser two-thread override, omit an override for non-browser capture, and add `--no-build` only after the first capture invocation.
- [ ] Step 2 — Print phase names and elapsed time and retain phase-specific non-zero failures.
- [ ] Step 3 — Preserve the build-affecting trigger, correct the Test UI job commentary, and set initial step/job budgets to 40/45 minutes.
- [ ] Step 3 — Add honest failure text that reserves “stale corpus” for an explicit snapshot assertion and never masks the job failure.
- [ ] Step 4 — Add one runbook sentence describing the Test UI browser/non-browser concurrency split without changing command blocks.
- [ ] Step 5 — Run locked restore, Release build, and the full non-Corpus solution test gate; record commands, cwd, output, and exit codes.
- [ ] Step 5 — Run a fresh Test UI verify and the catalogue check; confirm the combined capture count is 414.
- [ ] Step 5 — Prove stale-file and orphan negative cases with the retained capture, restoring/removing each temporary change.
- [ ] Step 5 — Confirm `docs/design/test-ui/**` and `AGENTS.md` are unchanged.
- [ ] Step 5 — Run the simplification pass and record dated findings and dispositions in the plan.
- [ ] Step 5 — Commit, push, open the PR to `dev`, and record the commit and PR in Kanmer.
- [ ] Step 6 — Obtain three Test UI executions at the same PR SHA and record every duration and conclusion.
- [ ] Step 6 — Confirm all three pass, median snapshot duration is at most 22 minutes, and no run exceeds 25 minutes.
- [ ] Step 6 — Apply the deterministic timeout formula if it lowers the initial 40/45 budgets, then obtain green CI on the amended SHA.
- [ ] Step 6 — Write the post-implementation report, move UIIMP-013 implementing → review, and stop without merging.

## Progress notes

Append execution evidence; never remove a failed attempt.
