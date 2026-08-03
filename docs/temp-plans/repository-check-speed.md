# Cut `repository-check` wall clock

Task line: shard `validate` into parallel unit / SQL-integration / browser
jobs, replace the migrate-per-test LocalDB setup with a per-run migrated
template database, and cache NuGet packages and the pinned Playwright
Chromium.

## Measured baseline

GitHub Actions run `30779796084` (`windows-latest`, `validate`, 28 minutes):

| Step | Elapsed |
| --- | --- |
| checkout | 5 s |
| Documentation links | 1 s |
| setup-dotnet | 32 s |
| Restore (`--locked-mode`) | 61 s |
| Build (Release) | 116 s |
| Install pinned Playwright Chromium | 20 s |
| Test | 23 m 50 s |

Test step composition: `Pegasus.Core.Tests` 179 tests in 0.6 s;
`Pegasus.ArchitectureTests` 73 tests in 0.4 s; `Pegasus.IntegrationTests`
306 passed / 1 skipped in 23 m 44 s. `qdos-pressure` is a separate ~3-minute
job and is not touched.

The cost is the per-database lifecycle, not the tests. Every class carrying
`[Collection(LocalDbFixtureDefinition.Name)]` runs serially
(`DisableParallelization = true`), `LocalDbTestDatabase.CreateAsync` is called
directly from 19 files, and `IntakeWebApplicationFactory` — instantiated from
26 files — calls it once per factory
(`tests/Pegasus.IntegrationTests/IntakeWebTestSupport.cs:72`) and then migrates
inside `CreateHost` via `DevelopmentOfflineInitialization`. That is roughly
170 `CREATE DATABASE` + migrate + `DROP DATABASE` cycles in series.

Sharding alone therefore moves almost nothing: the unit lane is one second and
the browser lane is 14 tests. The template database is what makes the SQL lane
shrink; sharding is what converts that saving into wall clock. Each step was
judged against a local measurement rather than an estimate.

Measured on this workstation, `Pegasus.IntegrationTests` with
`--filter "Category!=Corpus"`, both runs on the same tree before
`origin/dev` was merged in:

| Revision | Tests | Elapsed |
| --- | --- | --- |
| `origin/dev` baseline | 295 | 22 m 41 s |
| Template restore, web factory included | 298 | 17 m 28 s |

That is a 23% cut for about 1.9 seconds saved on each of roughly 170 database
lifecycles. What remains per lifecycle is host construction and identity
seeding, not migration. A 17-minute lane is well above the eight-minute
threshold this plan set for declining the shard machinery, so the SQL lane is
matrix-sharded three ways.

## What this changes

### 1. Per-run migrated template database

New `tests/Pegasus.IntegrationTests/LocalDbTemplateDatabase.cs`: a
`Lazy<Task<…>>` (`LazyThreadSafetyMode.ExecutionAndPublication`) that, once per
test-run process, creates `Pegasus_Template_<guid:N>`, migrates it through the
existing `LocalDbTestDatabase` code path, `BACKUP DATABASE … TO DISK` into the
server's default data directory, records the logical file names from
`RESTORE FILELISTONLY`, then drops the template database itself. Only the
`.bak` survives.

`BACKUP`/`RESTORE` is chosen over `DBCC CLONEDATABASE` (copies no data, so
`__EFMigrationsHistory` rows would be missing, and the clone is read-only and
diagnostics-only) and over detach + file copy + `CREATE DATABASE … FOR ATTACH`
(requires the client to touch server data files, which is impossible on the
`PEGASUS_TEST_SQL_DATASOURCE` container path and would fork the fixture into
two mechanisms). `BACKUP`/`RESTORE` is entirely server-side T-SQL, so LocalDB
and a SQL Server container behave identically; the `.bak` is a read-only
source, so concurrent restores are safe.

`LocalDbTestDatabase.CreateAsync` gains a `SchemaOrigin` property
(`Empty` / `Migrated` / `Template`). With `migrate: true` it restores from the
template; if the template could not be built it falls back to today's
`CREATE DATABASE` + `MigrateAsync` and records `Migrated`. The `migrate: false`
path is untouched — 13 call sites depend on an empty database, and
`ReadinessEndpointTests` asserts an empty applied-migrations list and a missing
`__EFMigrationsHistory` table.

`ValidateExactDisposableName` runs first in the restore path exactly as it does
in `CreateEmptyDatabaseAsync`, and `DisposeAsync` is unchanged. The `.bak` is
deleted on process exit, best effort, with an age-based sweep of stray files
from killed runs.

The same sweep also drops disposable *databases* an earlier run abandoned. A
run killed before its tests dispose — Ctrl-C, a CI timeout, a crash — leaves
its databases attached, and nothing else removed them: 32 of them, 512 MB, had
accumulated on this workstation from before this task. Dropping is server-side,
so unlike the backup sweep it works on the container path too. Two guards make
it safe, and both are tested: only names matching the exact disposable shape
are ever considered, and only databases older than a day, which keeps a suite
running right now — including one in another worktree against the same LocalDB
instance — far out of range. The name-shape rule now has one definition,
`IsDisposableName`, used by both the sweep and the create/restore/drop guard.

Then `tests/Pegasus.IntegrationTests/IntakeWebTestSupport.cs:72` moves from
`CreateAsync(migrate: false)` to the template path. That is where most of the
lifecycles are. `DevelopmentOfflineInitialization.InitializeAsync` still runs
inside `CreateHost`, so identity seeding and the QDOS principal are unchanged;
its `MigrateAsync` call degrades to a history read. No product claim moves:
the fixture, not the application, was doing the migrating, and
"startup does not migrate" is separately proved by
`ReadinessEndpointTests`, `CaseWorkflowMigrationTests`,
`TypedCaseDataMigrationTests`, and `AzureSqlRuntimeRoleMigrationTests`, all of
which keep `migrate: false`.

Templating the *seeded* database (Identity roles, the DevelopmentOffline user,
the QDOS principal) is a further saving and is explicitly out of scope: it
changes what `InitializeAsync` proves and needs its own equivalence test.

### 2. Sharded lanes in `.github/workflows/ci.yml`

`validate` is replaced by four jobs. `changes` and `qdos-pressure` keep their
current shape.

| Job | Runner | Runs when | Command |
| --- | --- | --- | --- |
| `documentation` | windows-latest | always | `./scripts/Test-DocumentationLinks.ps1` |
| `unit` | windows-latest | `build == 'true'` | `Pegasus.Core.Tests` and `Pegasus.ArchitectureTests`, whole projects |
| `sql-integration` | windows-latest, 3-shard matrix | `build == 'true'` | `Invoke-TestShard.ps1` over `--filter "Category!=Corpus&Category!=Browser"` |
| `sql-integration-coverage` | ubuntu-latest | `build == 'true'` | `Invoke-TestShard.ps1 -VerifyPartition` |
| `browser` | windows-latest | `build == 'true'` | `--filter "Category=Browser&Category!=Corpus"` |

The three lanes share `.github/actions/dotnet-build`, a composite action
holding the SDK setup, the NuGet cache key, the locked restore, and the
Release build, so the cache key cannot drift between lanes.

`documentation` is split out because the doc-link check is the only thing that
runs today when no build-relevant path changed, and it must keep running for
every change set. It stays on `windows-latest`: `Test-DocumentationLinks.ps1`
uses `Test-Path`, which is case-insensitive on Windows and case-sensitive on
Linux, so moving it would silently change the rule.

No-loss argument: the two unit projects contain no `[Trait]` attributes at all,
so they run unfiltered. `Category=Browser` and `Category!=Browser` are a
complement pair over the integration project, so intersected with
`Category!=Corpus` their union is exactly today's selection. `Category=Browser`
is a class-level trait on `Browser/AccessibilityTests.cs`,
`Browser/OperatorJourneyTests.cs`, and `ReadinessEndpointTests`'
`OfflineBrowserReadinessTests`, and a method-level trait in
`MultiFormatIntakeWebTests` — nine tests, not the `Browser/` folder. The
arithmetic gate is that the lanes' executed counts sum to today's 559 with one
skip.

Each lane checks out, restores, and builds for itself; that fixed cost is paid
in parallel and, once NuGet is cached, is well under the saving. Passing `bin`
and `obj` between jobs as artifacts is hundreds of megabytes per hop and is not
expected to beat a 116-second rebuild.

`scripts/Invoke-TestShard.ps1` enumerates the lane's tests with
`--list-tests`, fails closed when nothing parses, assigns whole classes (the
LocalDB collection pins a class's tests together), and asserts that the TRX
counter equals the count it assigned. Each shard uploads what it enumerated and
what it was assigned, and `sql-integration-coverage` fails unless every shard
enumerated the same set, the assignments are pairwise disjoint, and their union
is that set. Locally the three shards take 69, 111, and 104 of 284 tests and
verify clean. The `buildPattern` in `.github/workflows/ci.yml` gains the new
script, `ci.yml` itself, and `.github/actions/`, so a change to how the lanes
run is exercised by the lanes rather than path-skipped into a false green.

### 3. Caches

NuGet through `actions/setup-dotnet@v6`'s `cache: true` with
`cache-dependency-path` covering `global.json` and every
`packages.lock.json` under `src/` and `tests/`. `workspaces/**` lock files are
excluded: they belong to `.github/workflows/workspaces.yml` and would churn
the key on unrelated changes. `tests/Pegasus.Core.Tests` and
`tests/Pegasus.ArchitectureTests` have no lock file, so their `.csproj` files
join the key; adding `RestorePackagesWithLockFile` to those two projects is
carried in the same change so `--locked-mode` verifies content hashes for
every project in the solution, which is what makes a restored package cache
safe to trust.

Playwright Chromium through `actions/cache@v4` over
`~\AppData\Local\ms-playwright`, keyed on the runner OS and the hash of
`tests/Pegasus.IntegrationTests/packages.lock.json` (the pin is
`Microsoft.Playwright` 1.61.0 in
`tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj`), in the
browser lane only. The install step stays unconditional — it is idempotent and
repairs a partial cache — so nothing is ever gated on `cache-hit`. This saves
about 20 seconds and is the smallest item in the task line.

### 4. Documentation

`docs/engineering.md` "Branches and delivery" names the current three jobs, the
single `validate` step list, and the 75-minute timeout; it is rewritten to name
the new job set and timeouts, keeps the "succeeded or was path-skipped" rule
verbatim, and adds any new CI-executed script to the build-relevant path list.
`docs/operations.md` keeps the canonical three local commands exactly as they
are, adds the per-lane focused commands and states that their union equals the
canonical filter, notes that the template database uses server-side
`BACKUP`/`RESTORE` on the container path with a migrate-per-test fallback, and
records the manual sweep for stray `Pegasus_Template_*.bak` files. No new
Markdown file other than this plan; no ADR is required, because nothing adds a
top-level directory, project, store, runtime, migration stream, or deployment
unit.

## How this is verified

Locally, from the worktree root with PowerShell 7:

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"
```

The canonical command must still pass and still report the same totals. Around
it:

- A TRX baseline of the integration project captured before any edit and the
  same run after the change, compared on total elapsed (22 m 41 s → 17 m 28 s;
  TRX files under the ignored `artifacts/ci-speed-baseline/`).
- Each lane timed on its own: unit 0.6 s for 252 tests, browser 1 m 42 s for
  14, and the largest SQL shard 6 m 28 s for its 111, with the shard script's
  executed-equals-assigned check passing and `-VerifyPartition` confirming
  69 + 111 + 104 = 284 with no test in two shards and none in none.
- New `LocalDbTemplateDatabaseTests` proving a template-derived database is
  indistinguishable from a freshly migrated one: identical ordered applied
  migrations, equal to the compiled `Database.GetMigrations()` list and
  non-empty, with no pending migrations or model changes; and an ordered
  comparison of every column, index, foreign key, check constraint, default,
  database principal, role membership, permission, and table row count. Row
  counts are compared between the two databases rather than asserted empty,
  because the migration stream seeds provider reference data. It also asserts
  `SchemaOrigin == Template`, which turns a silent fallback into a red test
  rather than a slow green one, that two restores do not share a database, and
  that `migrate: false` is never served from the template.
- The abandoned-database sweep proved not to drop a database that is live, and
  `IsDisposableName` pinned by ten cases covering wrong prefix, wrong length,
  a non-GUID suffix, and `master`. The drop statement is the one `DisposeAsync`
  already issues on every test.
- Each lane's filter run on its own, with the executed counts summing to the
  canonical run's. Measured before merging `origin/dev`: 284 in the SQL lane
  and 14 in the browser lane against 298 for the whole non-corpus project;
  after the merge, 309 and 14 against 323.
- `./scripts/Test-DocumentationLinks.ps1` and
  `./scripts/Invoke-QdosAlphaAcceptance.ps1 -Profile CiPressure`.

In CI, run `30792948456` on the PR head. Attempt 1, with both caches cold, was
green in every job: **14 m 53 s** against the 28-minute `validate` baseline,
with the slowest shard at 13 m 55 s (4 m 28 s of checkout, restore, and build
plus 9 m 04 s of tests), `browser` 6 m 33 s, `unit` 4 m 05 s, and
`sql-integration-coverage` green, so the shards provably reassembled the
enumerated set exactly once.

Attempt 3, re-run on the same commit with both caches warm, was green again:
the slowest shard fell to 12 m 04 s as its setup dropped from 4 m 28 s to
2 m 48 s, and the Playwright install fell from 20 s to 1 s after a 7-second
cache restore. Its whole-workflow number is 15 m 15 s only because the
`changes` job took 2 m 09 s rather than its usual 20 s.

Attempt 2 was cancelled: `actions/checkout` in `changes` stalled in
`git fetch` for that job's whole five-minute timeout, which skipped every
dependent lane. Nothing in this change touches that checkout, and the same
step took 20 seconds on attempt 1.

Left unproved: the `PEGASUS_TEST_SQL_DATASOURCE` container path, since no
Linux CI job exists — server-side `BACKUP`/`RESTORE` there and its
migrate-per-test fallback are developer evidence at best; cache behaviour
across GitHub's eviction window; and anything about deployment, live
operation, or operator acceptance. This changes verification lanes only.

## Risks and stop conditions

- A filter that drops tests from every lane would leave CI green with less
  running. Guarded by the complement-pair argument, by the measured
  284 + 14 = 298 lane split, by fail-closed test enumeration, by the
  executed-equals-assigned TRX check in each shard, and by
  `sql-integration-coverage`.
- A silently failing template would leave CI green and slow. Guarded by the
  `SchemaOrigin == Template` assertion.
- Template schema drift would weaken 300 tests at once. Guarded by the
  applied-migrations and structural comparisons; any difference stops the
  template approach and the task ships caching and lanes only.
- Cache poisoning. Guarded by `--locked-mode` over every project once the two
  unlocked test projects gain lock files, and by never gating a restore or an
  install on `cache-hit`.
- `changes` now gates six lanes rather than two, so a stall in its
  full-history checkout skips the entire run — observed once on this PR, where
  `git fetch` hung for the job's whole five-minute timeout. Pre-existing and
  untouched here, but more consequential than it was, and worth either more
  headroom or a shallower diff strategy in its own task.
- The shards are balanced by class count, not by cost: 13 m 55 s, 12 m 29 s,
  and 10 m 45 s of a possible 9 m. Assigning by observed duration, or simply
  using more shards, would pull the critical path down further and is the
  obvious next increment.
- Removing `DisableParallelization = true` is potentially the largest remaining
  win and is deliberately not attempted: the flag is load-bearing for shared
  temp roots and LocalDB contention, the task line does not ask for it, and a
  flaky suite is worse than a slow one. It is recorded here as a candidate, not
  taken.
