Implementation executed via `codex exec` (gpt-5.6-sol, xhigh reasoning) in
worktree `../pegasus-worktrees/deliv-031-sql-connect-timeout` on branch
`task/deliv-031-sql-connect-timeout`, cut from `origin/dev`.

Codex's own report (verbatim summary):
- Raised `ConnectTimeout` 15 -> 60 on the single shared
  `SqlConnectionStringBuilder` in `BuildConnectionString`; added
  `ConnectRetryCount = 3` / `ConnectRetryInterval = 10`; added a short code
  comment citing the measured ~14s contention and the 300s
  `LifecycleCommandTimeoutSeconds` ceiling.
- Added a 5-attempt bounded retry (linear 250ms*attempt backoff) around the
  `DROP DATABASE` `ExecuteNonQueryAsync()` in `DisposeAsync()`, filtered to
  `SqlException.Number == 5061` (lock could not be placed), final exception
  left unsuppressed.
- Searched `tests/` for competing `ConnectTimeout`/`Connect Timeout`/
  `Data Source=`/`Server=`; found only the two intentional
  `Connect Timeout=1` unreachable-endpoint readiness tests in
  `ReadinessEndpointTests.cs` — not a competing/duplicate concept.
- Build: `dotnet build ./Pegasus.slnx --configuration Release` — exit 0, 0
  warnings, 0 errors (codex-reported).
- Tests: `dotnet test ... --filter FullyQualifiedName~IntakePersistenceIntegrationTests`
  — 10 passed, 0 failed (codex-reported).
- Committed `cc543922` and pushed to
  `origin/task/deliv-031-sql-connect-timeout`. No PR opened (per brief).
- Codex-reported risks: concurrent 3-shard CI contention not reproduced
  locally (only single-process LocalDB run); Kanmer MCP was unreachable from
  inside the codex sandbox (SSE probe 404) so it touched no `.kanmer` files.

Independent verification performed by the orchestrating agent (this
session), NOT by codex:
- `git status --porcelain=v1` clean; `git log --oneline origin/dev..HEAD`
  shows exactly one commit (cc543922); nothing unpushed
  (`origin/<branch>..HEAD` empty).
- `git diff --stat origin/dev..HEAD` touches exactly one file:
  `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs`
  (+22/-2) — matches the ticket's allowed file set exactly, no scope
  overrun.
- Full diff reviewed line-by-line; matches the plan (ConnectTimeout=60,
  ConnectRetryCount=3, ConnectRetryInterval=10, comment, 5061-filtered
  retry loop in DisposeAsync).
- Re-ran `dotnet build ./Pegasus.slnx --configuration Release` myself:
  Build succeeded, 0 Warning(s), 0 Error(s).
- Re-ran the focused filter myself:
  `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter
  "FullyQualifiedName~IntakePersistenceIntegrationTests"` — Passed! Failed:
  0, Passed: 10, Skipped: 0, Total: 10 (1m33s). Matches codex's claim
  exactly.
- Confirmed `LifecycleCommandTimeoutSeconds = 300` (line 503), so the code
  comment's "well below" claim about the 60s connect budget is accurate.
- Grepped `tests/` myself for `ConnectTimeout|Connect Timeout|Data Source=|Server=`
  and independently confirmed codex's search claim.

No changes needed to codex's diff; nothing reverted.

## 2026-08-29 — proof written; HELD in Verifying (not moved to Done)

`proof` is written against merged `dev` at `b92cb9a7` (D15). `get_doc_gates`
now reports `enter-done` **passable: true** — the gate is satisfied.

**The ticket was deliberately not moved.** The gate checks that a proof
document exists; it does not check that the proof proved anything. This
ticket's sole acceptance item — ten consecutive `sql-integration` runs without
a connection-timeout failure — is unmet at the time of writing:

- 12 completed `sql-integration` shard jobs on commits carrying `2d67cefa`,
  all green, zero connection-timeout and zero 5061 failures.
- But only **3 complete workflow runs** (runs 33243741194, 33245424905,
  33246463997). The ticket body distinguishes runs from shards ("PRs #588
  shard 3, #589 shard 2 ..."), so the bar is workflow runs: **3 of 10**.

Moving to Done now would be moving on partial evidence. Per AGENTS.md rule 20,
Done requires PASS; this is INCONCLUSIVE, not PASS.

**How to clear the hold.** Re-count runs whose head has `2d67cefa` as an
ancestor:

```
gh run list --workflow=ci.yml --limit 40 \
  --json databaseId,headSha,conclusion --jq '.[]|[.databaseId,(.headSha[0:8]),.conclusion]|@tsv'
git merge-base --is-ancestor 2d67cefa <headSha>     # exit 0 = carries the fix
gh api repos/collisionengineers/pegasus/actions/runs/<id>/jobs?per_page=100 \
  --jq '.jobs[]|select(.name|startswith("sql-integration ("))|[.name,.conclusion]|@tsv'
```

When ten such runs are clean, append the tally to `proof`, tick the
Verification item, move to Done, and close [[DELIV-033]] as not needed. If a
`Connection Timeout Expired` recurs first, DELIV-033's trigger has fired.

Note: six of the seven PRs merged on 2026-08-29 merged *before* DELIV-031
(`b92cb9a7` is the newest merge on `dev`), so their CI ran without the fix and
must not be counted toward the ten.
