# Post-implementation report

## What changed

`tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs`, in
`LocalDbTestDatabase`:

- `BuildConnectionString(string databaseName)` (~line 534): raised the
  single shared `SqlConnectionStringBuilder`'s `ConnectTimeout` from 15 to
  60, added `ConnectRetryCount = 3` and `ConnectRetryInterval = 10`, and
  added a short comment explaining the measured ~14s CI contention window
  and that the budget deliberately stays well below the existing
  `LifecycleCommandTimeoutSeconds` (300s) so a genuinely wedged instance
  still fails inside the job timeout. `MultipleActiveResultSets` and
  `InitialCatalog` unchanged.
- `DisposeAsync()`'s `DROP DATABASE` path (~line 773): added a 5-attempt
  bounded retry (linear `250ms * attempt` backoff) around
  `ExecuteNonQueryAsync()`, filtered to `SqlException.Number == 5061`
  ("lock could not be placed") — the second failure signature in the
  ticket's evidence. The final exception is not swallowed.

No other file changed. No `src/` file touched, no workflow/script touched,
per the ticket's explicit "Do NOT" list.

## Why / reuse

Both changes live on the one existing `BuildConnectionString` builder and
the one existing `DisposeAsync` disposal path — no second connection-string
construction or general-purpose retry abstraction was introduced. The
connect-retry uses ADO.NET's own built-in `ConnectRetryCount`/
`ConnectRetryInterval` mechanism. The disposal retry follows the test
project's existing inline pattern of a bounded loop with a filtered
`catch (SqlException e) when (...)`.

Searched `tests/` for `ConnectTimeout`, `Connect Timeout`, `Data Source=`,
`Server=` to confirm no second, competing connection-string builder exists
that would defeat this fix (see `files.md` for the full list). The only
other `Connect Timeout=` literals found are two intentional
`Connect Timeout=1` settings in `ReadinessEndpointTests.cs` against an
unreachable loopback port, used to exercise readiness-probe failure paths —
not the same concept, not touched.

## Build

`dotnet build ./Pegasus.slnx --configuration Release`

Exit code 0 — 0 Warning(s), 0 Error(s). Run twice: once by codex during
implementation, once independently by the orchestrating agent after the
fact. Both green.

## Tests

`dotnet test ./Pegasus.slnx --configuration Release --no-build --filter
"FullyQualifiedName~IntakePersistenceIntegrationTests"`

Passed! Failed: 0, Passed: 10, Skipped: 0, Total: 10. Run twice (once by
codex, once independently by the orchestrating agent against the same
worktree/LocalDB) with identical results.

The ticket's own acceptance criterion — ten consecutive `sql-integration`
CI runs without a connection-timeout failure — is CI-observed evidence that
can only be gathered after this PR merges and subsequent shard runs are
watched; it is not reproducible in a single local run and is carried to
`verifying`.

## Commits

- `cc543922` — `fix(tests): harden LocalDB lifecycle connections (DELIV-031)`
  — pushed to `origin/task/deliv-031-sql-connect-timeout`.

## Out-of-scope defects found

None reported by codex or found on independent review.

## Risks / open questions

- Concurrent 3-shard CI contention (the actual failure condition) was not
  reproduced locally — only a single-process LocalDB run was exercised.
  The fix's effectiveness is confirmed by watching subsequent
  `sql-integration` shard runs post-merge (the ticket's stated
  verification), not by this local run.
- The 60s connect budget is 5x, not an order of magnitude, below the 300s
  `LifecycleCommandTimeoutSeconds`; the code comment says "well below",
  which is accurate without overstating the margin.
