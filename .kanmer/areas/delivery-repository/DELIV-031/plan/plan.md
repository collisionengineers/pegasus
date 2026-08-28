# Plan

## Root cause

`LocalDbTestDatabase.BuildConnectionString` in
`IntakePersistenceIntegrationTests.cs` builds a single
`SqlConnectionStringBuilder` with `ConnectTimeout = 15`, shared by ordinary
connections, `MasterConnectionString()` (the `DROP`/`CREATE`/`RESTORE
DATABASE` path), and every EF Core `UseSqlServer` composition. Under CI shard
parallelism, `CREATE`/`RESTORE`/`DROP DATABASE` DDL serialises on the single
shared LocalDB instance and an ordinary connection queues behind it past the
15-second budget — measured failure durations (13999-14014 ms) land exactly
on that hardcoded timeout.

## Steps

1. Raise `BuildConnectionString`'s single shared `SqlConnectionStringBuilder`
   to `ConnectTimeout = 60`, add `ConnectRetryCount = 3` /
   `ConnectRetryInterval = 10` (ADO.NET's built-in idle-connection retry —
   reused, no new abstraction). Keep `MultipleActiveResultSets` and
   `InitialCatalog` unchanged. Document the measured ~14s contention window
   and that the budget stays well below `LifecycleCommandTimeoutSeconds`
   (300s) so a genuinely wedged instance still fails inside the job timeout.
2. Add a small bounded retry inline in `DisposeAsync()`'s `DROP DATABASE`
   path for `SqlException` 5061 (`ALTER DATABASE ... lock could not be
   placed`) — the second failure signature in the ticket's evidence. No
   existing retry helper exists in this file or project, so this stays a
   local `for` loop (5 attempts, linear backoff), never swallowing the final
   exception — matches the ticket's explicit "do not invent a general
   retry abstraction" instruction.
3. Search `tests/` for competing `ConnectTimeout`/`Connect Timeout`/
   `Data Source=`/`Server=` literals that could defeat the fix or represent a
   second copy of the same concept. Record findings in `files.md` instead of
   touching them (none compete or duplicate the harness — see `files.md`).
4. Build (`dotnet build ./Pegasus.slnx --configuration Release`) and run the
   focused filter `FullyQualifiedName~IntakePersistenceIntegrationTests`
   against LocalDB to prove the harness still works end to end.
5. Commit (`fix(tests): ...` form) and push to
   `task/deliv-031-sql-connect-timeout`.

## Reuse

- The one existing `BuildConnectionString` builder — no second connection
  string construction is introduced.
- ADO.NET's built-in `ConnectRetryCount`/`ConnectRetryInterval` — no custom
  retry package or abstraction for the connect path.
- The test project's existing inline pattern of a bounded `for` loop with a
  filtered `catch (SqlException e) when (...)` for the disposal-path retry —
  no new retry helper class.

## Out of scope

- `.github/workflows/repository-check.yml`, `scripts/Invoke-TestShard.ps1`,
  and any `src/` file — excluded per the ticket's explicit "Do NOT" list.
- The two unrelated red shards on PRs #605/#606 (real assertion failures
  owned by other tickets).
- The non-`ConnectTimeout` `Server=` literals found during the search
  (`files.md`) — not the flaking harness, not touched.

## Verification

- `dotnet build ./Pegasus.slnx --configuration Release` — exit 0.
- `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter
  "FullyQualifiedName~IntakePersistenceIntegrationTests"` — 10/10 passing.
- The ticket's own acceptance ("ten consecutive `sql-integration` runs
  without a connection-timeout failure") is CI-observed evidence gathered
  post-merge, not reproducible locally; recorded as an open item for
  `verifying`.

## Simplification pass

n/a at plan time — executed on the branch's own diff before PR open, per the
Repository task workflow; recorded in this document once complete.
