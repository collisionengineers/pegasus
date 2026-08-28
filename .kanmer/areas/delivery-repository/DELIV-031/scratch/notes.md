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
