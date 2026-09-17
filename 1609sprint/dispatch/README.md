# Sprint 1609 dispatch

One `codex-workflow` run that implements the verified sprint tasks with Codex, verifies each
branch on this host behind one serialized slot, has a separate Codex thread review it, and
pushes it as a PR. The plan that produced it: `C:/Users/Alex/.claude/plans/run-a-full-check-greedy-twilight.md`.

## Files

- `sprint-1609.mjs`: the workflow script (default export for `codex-workflow.mjs run`).
- `tasks.mjs`: the task table; `tasks/<id>.md`: one brief per Codex task.
- `args.json`: paths, limits and the `only` / `skip` / `dryRun` switches.
- State: `artifacts/sprint-1609/state.json` (resume authority) and `host-slot.json` (slot ledger), both under the main checkout and gitignored.

## Run

```
node "C:/Users/Alex/Documents/GitHub/claude-using-codex-skill/plugins/codex-bridge/scripts/codex-workflow.mjs" run "C:/Users/Alex/Documents/GitHub/pegasus/1609sprint/dispatch/sprint-1609.mjs" --args @1609sprint/dispatch/args.json --concurrency 3
```

Dry run (no Codex, no dotnet, no git writes, no GitHub): set `"dryRun": true` in `args.json`.
Subset: `"only": ["U1", "C731"]`. The script skips any task whose recorded state is already
`delivered`, `blocked` or `needs-human`, so re-running the same command resumes.

Watch: `node .../codex-workflow.mjs status --last`, or read `artifacts/sprint-1609/state.json`.
Verify logs and TRX files land in the run directory printed at start (`<run>/verify/`, `<run>/trx/`).

## Phases

- Phase A (immediately): U1 docs-gate PR, close #731, PR repairs P766 → P767 and P765.
- Gate: tasks whose tree will contain `v27-notes.md` (U2 and I1–I8) wait, at their first verify,
  for the U1 PR to be merged (poll every 60 s, up to `waitForMergeMinutes`). Their Codex implement
  turns start immediately; only the verifier waits.
- Phase B: U2 six-shard on PR 764 and I1–I8 on new branches.

## After the run

Merge U1 first and re-run failed documentation lanes on 764–767. Merge order for the rest is in
each PR body. `result.json` lists every task's state, PR URL, open questions and quarantines.
