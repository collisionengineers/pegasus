# Plan

Revised 2026-08-29 after adversarial verification. The first version of this
plan mis-sourced its evidence; every premise below now names the command that
checked it.

## Root cause

Two distinct failures share one harness,
`LocalDbTestDatabase` in `IntakePersistenceIntegrationTests.cs`.

**1. Connect-timeout expiry (the ticket's own evidence).**
`BuildConnectionString` builds a single `SqlConnectionStringBuilder` with
`ConnectTimeout = 15`, shared by ordinary connections,
`MasterConnectionString()` (the `CREATE`/`RESTORE`/`DROP DATABASE` path), and
every EF Core `UseSqlServer` composition. `tests/Pegasus.IntegrationTests/
xunit.runner.json` sets `maxParallelThreads: 4`, so four collections run
concurrently inside one shard, each driving database DDL against that
runner's single LocalDB instance. An ordinary open queues behind that DDL and
expires: measured `[Post-Login] complete=13999`/`14000`/`14014` ms, landing
exactly on the 15s budget.

Correction to the first plan: the contention is *within* one shard, not
across shards. `.github/workflows/ci.yml:163-168` gives each of the three
shards its own `windows-latest` runner, so the shards never share a LocalDB
instance. The concurrency that matters is the four xUnit collections on one
four-vCPU runner.

**2. `ALTER DATABASE` lock unavailable (error 5061).** `DisposeAsync()` runs
`ALTER DATABASE ... SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE`
and cannot take the exclusive database lock while a neighbouring collection
holds the instance.

## Premises, and how each was checked

| Premise | Check | Result |
| --- | --- | --- |
| The 5061 signature really occurs in CI | `gh run view --log-failed --job 98903659122` | Confirmed |
| It originates in this exact code | same log, stack frames | `LocalDbTestDatabase.DisposeAsync() ... IntakePersistenceIntegrationTests.cs:line 767` and `:line 773` |
| 5061 is the number for that message | `SELECT message_id FROM sys.messages WHERE text LIKE 'ALTER DATABASE failed because a lock%'` on LocalDB 16.0.1000.6 | `5061` |
| `SqlException.Number` will be 5061, not the trailing 5069 | CI log renders 5061's text first in `SqlException.Message`, which concatenates `Errors` in order | Confirmed by observation |
| 5061 leaves the connection usable | `SELECT severity FROM sys.messages WHERE message_id = 5061` | `16` — statement-level, non-fatal |
| The transient server-error family that `ConnectRetryCount` targets occurs here | grep of all five failure logs for 4060 / 40197 / 40501 / 40613 / 233 / 10928 / 10929 / "cannot open database" / "transport-level" | **Zero hits** |
| Failures happen at instance cold start | first-failure timestamps vs `Test run for` in each of the five logs | **No** — 3m27s, 4m50s, 5m01s, 5m27s, 6m29s in |
| The patched builder covers every failing class | `grep` for `IntakeWebApplicationFactory` / `LocalDbTestDatabase` in each named class | Confirmed (see `files.md`) |

Verified job ids: 98903659122 (PR #594, shard 3), 98823484553, 98804254607,
98801103945, 98797031780.

## Steps

1. `BuildConnectionString`: raise `ConnectTimeout` from 15 to 60 on the one
   existing shared builder. Keep `MultipleActiveResultSets` and
   `InitialCatalog` unchanged. Comment the measured window and that 60s still
   fails a wedged instance inside the 20-minute job timeout.
2. `DisposeAsync()`'s `DROP DATABASE` path: bounded retry for
   `SqlException.Number == 5061`, 5 attempts, `TimeSpan.FromSeconds(attempt)`
   backoff (10s total), reusing the already-open connection and command. The
   final attempt rethrows. No retry helper exists in this project, so this
   stays a local loop rather than a new abstraction.
3. Search `tests/` for competing `ConnectTimeout` / `Connect Timeout` /
   `Data Source=` / `Server=` literals, and separately confirm the patched
   builder reaches all three classes named in the ticket's evidence. Record
   in `files.md`; touch nothing.
4. Build, then run the focused filters.
5. Commit and push.

## Reuse

- The one existing `BuildConnectionString` builder — no second
  connection-string construction.
- The existing open `SqlConnection` and `SqlCommand` in `DisposeAsync`, reused
  across retry attempts rather than rebuilt.
- `LifecycleCommandTimeoutSeconds` (300s), unchanged, still bounds each
  attempt.

## Rejected

**`ConnectRetryCount` / `ConnectRetryInterval`.** The first implementation
added `ConnectRetryCount = 3`, `ConnectRetryInterval = 10` and the first plan
described them as "ADO.NET's built-in idle-connection retry". That
description was wrong in both directions. They do reach the initial
`SqlConnection.Open()` path, not only idle reconnects — but connection
resiliency retries only the built-in transient server-error list, and the
observed failure is a client-side budget expiry (surfaced as -2), which is
not on that list and leaves no budget to retry from once exhausted. So the
settings could not have fixed the signature this ticket targets. Grepping all
five failure logs for the transient family they *would* cover returned zero
hits, making them a gate that gates nothing (AGENTS.md rule 21). **Removed in
this revision.** The effective lever is the `ConnectTimeout` raise alone, and
the record now says so.

**A shared drop-with-retry helper across `LocalDbTestDatabase.DisposeAsync`
and `LocalDbTemplateDatabase.DropQuietlyAsync`.** Same SQL, different
contract: `DropQuietlyAsync` is a best-effort sweep of a *previous* run's
leftovers that deliberately swallows every exception and uses a shorter 60s
budget, because giving up is the correct outcome there;`DisposeAsync` must
succeed and asserts `DB_ID` is null afterwards. No second caller wants the
same behaviour, so no abstraction (see `docs/engineering.md`
§abstractions-and-deferred-capabilities).

## Out of scope

- `.github/workflows/ci.yml` and `scripts/Invoke-TestShard.ps1` — read for
  evidence, not modified. (The first plan attributed this exclusion to "the
  ticket's explicit Do NOT list"; the DELIV-031 record has no such list. The
  constraint came from the orchestrator's lane brief.)
- Any `src/` file.
- The non-`ConnectTimeout` `Server=` literals found during the search
  (`files.md`).

## Verification

- `dotnet build ./Pegasus.slnx --configuration Release` — exit 0.
- `dotnet test ... --filter "FullyQualifiedName~IntakePersistenceIntegrationTests"`
  — 10/10.
- `dotnet test ./tests/Pegasus.IntegrationTests/... --filter
  "FullyQualifiedName~CaseTaskArchivePersistenceTests|...OrganizationAdministrationWebTests|...AutomationConnectorAuthorizationTests|...LocalDbTemplateDatabaseTests"`
  — 48/48. These are the classes that actually failed in CI, including the one
  that raised the 5061.
- The ticket's own acceptance — ten consecutive `sql-integration` runs with no
  connection-timeout failure — is post-merge CI evidence, carried to
  `verifying`. It cannot be gathered locally: a single-process local run does
  not reproduce four-way collection contention on a four-vCPU runner.

## Simplification pass — 2026-08-29

Run over this branch's own diff (`git diff origin/dev...HEAD`, one file).

| Lens | Finding | Disposition |
| --- | --- | --- |
| Reuse | `LocalDbTemplateDatabase.DropQuietlyAsync` runs the same `ALTER`+`DROP` SQL; a shared retry helper was considered | **Rejected** — different contract (best-effort sweep vs. asserted teardown); no second caller wants the retry semantics |
| Reuse | No retry helper exists anywhere in `Pegasus.IntegrationTests` | Confirmed by grep; local loop is the right shape |
| Simplification | `ConnectRetryCount` / `ConnectRetryInterval` gate nothing observed | **Fixed** — removed, 2 lines deleted |
| Simplification | Bare magic number `5061` in the exception filter | **Fixed** — named `lockNotPlacedErrorNumber` |
| Simplification | Comment conflated the connect-stall window with the lock-contention window | **Fixed** — the two comments now each describe their own failure |
| Efficiency | Retry reuses the open connection and command; backoff costs nothing on the happy path | No change needed |
| Efficiency | `ConnectTimeout = 60` raises a ceiling, not a floor — no cost when connections succeed | No change needed |
| Altitude | Change confined to the one builder and the one drop path; no new type, file, or abstraction | Correct altitude |
