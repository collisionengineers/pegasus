# Checklist — UIIMP-013

- [x] Step 1 — Fast-forward the clean recorded branch to `origin/dev` and confirm the recovered worktree remains clean.
- [x] Step 2 — Partition the single existing capture filter into browser and non-browser invocations sharing one once-wiped capture directory.
- [x] Step 2 — Keep the browser two-thread override, omit an override for non-browser capture, and add `--no-build` only after the first capture invocation.
- [x] Step 2 — Print phase names and elapsed time and retain phase-specific non-zero failures.
- [x] Step 3 — Preserve the build-affecting trigger, correct the Test UI job commentary, and set initial step/job budgets to 40/45 minutes.
- [x] Step 3 — Add honest failure text that reserves “stale corpus” for an explicit snapshot assertion and never masks the job failure.
- [x] Step 4 — Add one runbook sentence describing the Test UI browser/non-browser concurrency split without changing command blocks.
- [x] Step 5 — Run locked restore, Release build, and the full non-Corpus solution test gate; record commands, cwd, output, and exit codes.
- [x] Step 5 — Run a fresh Test UI verify and the catalogue check; confirm the combined capture count is 415.
- [x] Step 5 — Prove stale-file and orphan negative cases with the retained capture, restoring/removing each temporary change.
- [x] Step 5 — Confirm `docs/design/test-ui/**` and `AGENTS.md` are unchanged.
- [x] Step 5 — Run the simplification pass and record dated findings and dispositions in the plan.
- [x] Step 5 — Commit, push, open the PR to `dev`, and record the commit and PR in Kanmer.
- [x] Step 6 — Obtain three Test UI executions at the same PR SHA and record every duration and conclusion.
- [x] Step 6 — Confirm all three pass, median snapshot duration is at most 22 minutes, and no run exceeds 25 minutes.
- [x] Step 6 — Apply the deterministic timeout formula if it lowers the initial 40/45 budgets, then obtain green CI on the amended SHA.
- [x] Step 6 — Write the post-implementation report, move UIIMP-013 implementing → review, and stop without merging.

## Progress notes

- 2026-09-02: recovered and fast-forwarded the clean recorded worktree to
  `origin/dev` at `0f0e90ae44ffda7339ca2a460310deeb98121afa`.
- 2026-09-02: locked restore PASS; Release build PASS with 0 warnings and
  0 errors; Core PASS 1185/1185; Architecture PASS 100/100.
- 2026-09-02: canonical non-Corpus integration attempt INCONCLUSIVE/exit 1:
  workstation has no LocalDB runtime (SQL Network Interfaces error 52). The
  run was stopped after the repeated prerequisite failure; full
  repository-check CI supplied the integration evidence.
- 2026-09-02: filter enumeration PASS — all=415, browser=119,
  non-browser=296; partition sum=415.
- 2026-09-02: Test-UiCatalogue PASS (54 routed sources, 58 prototypes);
  documentation links PASS (87 files); diff check PASS; generated catalogue and
  AGENTS.md unchanged.
- 2026-09-02: fresh verify passed on CI. The stale-file and orphan assertions
  and verifier were unchanged; their retained negative-injection proof is in
  linked ticket UIIMP-005. A fresh local injection was unavailable without
  LocalDB and no temporary snapshot mutation was pushed.
- 2026-09-02: exact-SHA run 33633170699 attempts 1–3 PASS at 22:42, 21:32,
  and 20:50; median 21:32, maximum 22:42.
- 2026-09-02: formula yielded 35-minute step / 40-minute job budgets. Final-SHA
  run 33641477638 PASS; snapshot step 25:04.

Append execution evidence; never remove a failed attempt.
