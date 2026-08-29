# Proof — DELIV-031: CI sql-integration connect-timeout flake

## What was verified, and where

Verified on merged `dev` at `b92cb9a7b8bf7727b452aa397d9df04084da1270`, which
*is* the ticket's own merge commit: `Merge pull request #612 from
collisionengineers/task/deliv-031-sql-connect-timeout`, merged
2026-08-29T09:15:51Z from `task/deliv-031-sql-connect-timeout` into `dev`.
Both recorded commits are reachable on the merge target
(`git merge-base --is-ancestor <sha> HEAD` exits 0 for each):
`cc543922` "fix(tests): harden LocalDB lifecycle connections" and `2d67cefa`
"fix(tests): retarget the LocalDB lifecycle fix on verified CI evidence".

The net change is one file, **+27/-2**, exactly as the ticket's records claim
(`git diff --stat cc543922^ 2d67cefa` → `1 file changed, 27 insertions(+), 2
deletions(-)`).

This proof splits into what is **proven now** (the shipped change, its caller
chain, the build, the focused tests, and the post-merge shard runs gathered so
far) and what is **not yet gatherable** (the ticket's own ten-run acceptance).
The ticket is **held in Verifying** on that basis — see Outstanding.

## Evidence

### The connect-timeout budget was raised on the one shared builder

Tier: **shipped source on merged `dev`** (not a deployed-feature claim).

`tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs:528-539`:

```csharp
private static string BuildConnectionString(string databaseName)
{
    var dataSource = Environment.GetEnvironmentVariable(DataSourceVariable);
    var builder = new SqlConnectionStringBuilder
    {
        InitialCatalog = databaseName,
        // Four parallel collections (xunit.runner.json) drive DDL against
        // one LocalDB instance, so an ordinary open queues behind it: CI
        // measured 13999-14014 ms against the previous 15s budget. 60s
        // clears that and still fails a wedged instance inside the
        // 20-minute job timeout (DELIV-031).
        ConnectTimeout = 60,
        MultipleActiveResultSets = true
    };
```

`MultipleActiveResultSets` and `InitialCatalog` are unchanged, as claimed.

There is exactly one `ConnectTimeout` in the LocalDB harness:
`git grep -n "ConnectTimeout\|Connect Timeout" -- tests/` returns only
`IntakePersistenceIntegrationTests.cs:539` (=60) plus
`ReadinessEndpointTests.cs:82,141`, which set `Connect Timeout=1` against
`Server=127.0.0.1,1` — a deliberately unreachable loopback used to exercise
readiness-probe failure paths, not the LocalDB harness. No competing builder
defeats the fix.

### The 5061 DROP DATABASE retry shipped

Tier: **shipped source on merged `dev`**.

`IntakePersistenceIntegrationTests.cs:770-790`:

```csharp
drop.CommandTimeout = LifecycleCommandTimeoutSeconds;

// CI hit 5061 here -- "a lock could not be placed ... Try again
// later" -- with a parallel collection holding the instance.
// Retry is the documented remedy; 10s of backoff, then the fifth
// attempt rethrows (DELIV-031).
const int lockNotPlacedErrorNumber = 5061;
const int maximumAttempts = 5;
for (var attempt = 1; ; attempt++)
{
    try
    {
        await drop.ExecuteNonQueryAsync();
        break;
    }
    catch (SqlException exception)
        when (exception.Number == lockNotPlacedErrorNumber
            && attempt < maximumAttempts)
    {
        await Task.Delay(TimeSpan.FromSeconds(attempt));
    }
}
```

Backoff is `1+2+3+4 = 10s` across five attempts, and the fifth attempt
rethrows (the `when` clause stops filtering at `attempt == 5`) — matching the
revised plan, not the superseded 2.5s version. `LifecycleCommandTimeoutSeconds`
is `300` (`:503`), so each attempt stays bounded well inside the 20-minute job
timeout.

### The rejected lever really was removed, not retained

Tier: **shipped source on merged `dev`**.

`git grep -n "ConnectRetryCount\|ConnectRetryInterval" -- .` over the whole
repository at `b92cb9a7` returns **no matches**. The plan's disposition
("removed rather than retained-and-explained", AGENTS.md rule 21) is what
actually shipped.

### Done means wired — the patched builder reaches the code that failed

Tier: **caller chain on merged `dev`** (this is a CI-harness change; it has no
`src/` production caller *by design*, and should not have one — its consumers
are the integration-test estate and the `sql-integration` CI lane).

The single builder feeds both connection paths, so there is no bypass:

- `IntakePersistenceIntegrationTests.cs:576` —
  `ConnectionString = BuildConnectionString(databaseName);`, which is then
  passed to `options.UseSqlServer(ConnectionString)` for every EF composition
  and used by `CreateConnection()`.
- `IntakePersistenceIntegrationTests.cs:835` —
  `internal static string MasterConnectionString() =>
  BuildConnectionString("master");`, the `CREATE`/`RESTORE`/`DROP DATABASE`
  path.

The classes that actually failed in CI all route through it:

| Failing class | Call site on `b92cb9a7` |
| --- | --- |
| `OrganizationAdministrationWebTests` | `new IntakeWebApplicationFactory()` at `:17`, `:143` |
| `AutomationConnectorAuthorizationTests` | `new IntakeWebApplicationFactory(TimeProvider.System)` at `:31`, `:125`, `:149`, `:178` |
| `CaseTaskArchivePersistenceTests` | `LocalDbTestDatabase.CreateAsync(` at `:801` |
| `IntakePersistenceIntegrationTests` | `LocalDbTestDatabase` directly |

`IntakeWebApplicationFactory` holds the harness:
`IntakeWebTestSupport.cs:41` `private readonly LocalDbTestDatabase database;`
and `:92` `database = LocalDbTestDatabase.CreateAsync().GetAwaiter()
.GetResult();`.

Breadth of the caller set on merged `dev`:
`git grep -l "LocalDbTestDatabase" -- tests/` → **41 files**;
`git grep -l "IntakeWebApplicationFactory" -- tests/` → **64 files**.

### Both failure signatures are real, and originate in this code

Tier: **CI log evidence**, re-fetched independently for this proof rather than
taken from the ticket's records.

Connect timeout — `gh run view --log-failed --job 98823484553`:

```
Microsoft.Data.SqlClient.SqlException : Connection Timeout Expired.  The
timeout period elapsed during the post-login phase. ... The duration spent
while attempting to connect to this server was - [Pre-Login]
initialization=1; handshake=0; [Login] initialization=0; authentication=0;
[Post-Login] complete=14000;
```

`complete=14000` ms against the former 15s budget — the plan's sizing claim
holds.

Error 5061 — `gh run view --log-failed --job 98903659122`
(`sql-integration (3)`, run 33187292311, 2026-08-28T16:03:49Z):

```
Microsoft.Data.SqlClient.SqlException : ALTER DATABASE failed because a lock
could not be placed on database 'Pegasus_Test_e9a4f3feb9754331ad4e65b9fc72ef1a'.
Try again later.
ALTER DATABASE statement failed.
   at Pegasus.IntegrationTests.LocalDbTestDatabase.DisposeAsync() in
      ...\IntakePersistenceIntegrationTests.cs:line 767
   at Pegasus.IntegrationTests.LocalDbTestDatabase.DisposeAsync() in
      ...\IntakePersistenceIntegrationTests.cs:line 773
```

Those frames are the `DisposeAsync` DROP path this change wraps. The plan's own
correction stands: this job is *not* among the four the ticket body listed, and
the records now say so.

### The contention premises

Tier: **shipped configuration on merged `dev`**.

- `tests/Pegasus.IntegrationTests/xunit.runner.json` — `"maxParallelThreads":
  4`.
- `.github/workflows/ci.yml` `sql-integration` — `runs-on: windows-latest`,
  `timeout-minutes: 20`, `matrix: shard: [1, 2, 3]`. Each shard is its own
  runner, so shards never share a LocalDB instance; the contention is the four
  xUnit collections inside one shard. The plan's corrected root cause is the
  accurate one.

### Build and full-suite test

Tier: **build/test** — cited from the canonical gate evidence for merged `dev`
at `b92cb9a7`, not re-run here.

```
dotnet restore ./Pegasus.slnx --locked-mode            -> exit 0
dotnet build ./Pegasus.slnx --configuration Release --no-restore
  -> Build succeeded. 0 Warning(s), 0 Error(s).
dotnet test ./Pegasus.slnx --configuration Release --no-build
  --filter 'Category!=Corpus&Category!=Browser'
  -> Pegasus.ArchitectureTests   Failed: 0, Passed:  100
  -> Pegasus.Core.Tests          Failed: 0, Passed: 1133
  -> Pegasus.IntegrationTests    Failed: 0, Passed: 1022, Skipped: 2
```

The two skips are pre-existing and unrelated to this ticket. The one failure in
the gate run (`EvaBundleContractTests`) was diagnosed there as a stale
working-copy CRLF artefact, unrelated to DELIV-031, and required no repository
change.

### Post-merge shard runs gathered so far

Tier: **CI execution of the shipped bytes**.

The merged file is byte-identical to the branch tip CI actually exercised —
`git rev-parse 2d67cefa:tests/.../IntakePersistenceIntegrationTests.cs` and
`git rev-parse b92cb9a7:tests/.../IntakePersistenceIntegrationTests.cs` both
return blob `fe3cdf427c116dae85b495e4c747061669b9532a`. So run 33243741194
counts as a run of the shipped code.

Every CI run whose head commit has `2d67cefa` as an ancestor
(`git merge-base --is-ancestor`), as at 2026-08-29 ~10:05Z:

| Run | Head | Branch | sql-integration shards |
| --- | --- | --- | --- |
| 33243741194 | `2d67cefa` | `task/deliv-031-sql-connect-timeout` | 3/3 success |
| 33245424905 | `44bcb8c0` | `task/plat-054-office-boundaries` | 3/3 success |
| 33246463997 | `d393ecd5` | `task/plat-049-operations-features` | 3/3 success |
| 33246469257 | `48df8f58` | `task/plat-052-eva-submission-route` | 2 success, 1 running |
| 33246576093 | `b4d0f88a` | `task/auto-006-automation-admin` | 1 success, 2 running |

**12 completed `sql-integration` shard jobs on the fixed code, all green, zero
connection-timeout failures and zero 5061 failures.** Three of those are
complete workflow runs (all three shards green).

**Correction to a premise in the closeout brief.** The brief asked for "the
shard results on the seven PRs merged today". Six of the seven merged *before*
DELIV-031 — `b92cb9a7` is the newest merge on `dev`, and
`git merge-base --is-ancestor 2d67cefa <merge>` fails for `940062c2`
(PLAT-053), `a01a640b` (INTK-046), `d7f6201c` (CASE-026), `c87e8d5d`
(UIIMP-008), `21b35398` (ENG-025) and `210727dd` (CASE-012). Their PR CI ran
**without** the fix, so it is not evidence for it and is not counted above.

## The ticket's own verification items

| Item | Status | Evidence |
| --- | --- | --- |
| Ten consecutive `sql-integration` runs without a connection-timeout failure | **UNPROVEN** | 12 shard jobs / 3 complete workflow runs on the fixed code are green, but "run" in the ticket body means a workflow run (it distinguishes runs from shards), so this is 3 of 10. Not tickable. |

The plan's and post-implementation report's own verification lines, checked
against the shipped bytes:

| Claim | Status | Evidence |
| --- | --- | --- |
| Build exit 0, 0 warnings | Proven | Gate evidence for `b92cb9a7` |
| `IntakePersistenceIntegrationTests` 10/10 | Superseded and covered | Not re-run; the full `Pegasus.IntegrationTests` lane is green at `b92cb9a7` (1022 passed), which is a strictly larger claim |
| The four CI-failing classes 48/48 | Superseded and covered | Same — all four are inside the green full lane |
| Net diff one file, +27/-2 | Proven | `git diff --stat cc543922^ 2d67cefa` |
| No assertion weakened, skipped, deleted or inverted | Proven | The whole diff is additive: one changed literal (`15`→`60`) and a retry wrapper around an existing call. No test method or assertion is touched. |
| `ConnectRetryCount`/`Interval` removed | Proven | Repo-wide grep at `b92cb9a7` returns nothing |
| Follow-up [[DELIV-033]] raised and trigger-gated | Proven | DELIV-033 exists, `backlog`, links DELIV-031, and its Trigger says "Open it only if DELIV-031's own acceptance fails" |

## Outstanding

- **The ticket's sole acceptance item is not yet gatherable.** Ten consecutive
  `sql-integration` runs is post-merge CI evidence accumulating in real time;
  three complete runs exist about 50 minutes after the merge. It cannot be
  forced locally — a single-process local run does not reproduce four-way
  collection contention on a four-vCPU runner. **DELIV-031 itself owns this**
  and stays in Verifying until the count is reached.
- **The green runs so far are not yet statistically decisive.** Five failing
  shard jobs are known from 2026-08-28 (PRs #588, #589, #592, #581, and the
  5061 in job 98903659122). Twelve clean shard jobs against that base rate is
  encouraging but is not the ten-run bar the ticket set, and this proof does
  not claim it is.
- **[[DELIV-033]]** owns the deferred half of the ticket's stated preference
  (an execution-strategy or `SqlConnection.RetryLogicProvider` connection
  retry). It is correctly trigger-gated on this acceptance failing, so it is
  not speculative work. If the ten runs come back clean it closes as not
  needed.
- **The 5061 lock-wait duration was never measured**, so the 10s backoff is an
  order-of-magnitude choice, not a sizing. The records say so plainly. Owned by
  DELIV-031's acceptance window; if 5061 recurs, that budget is the first thing
  to revisit.
- **PR #612 carries no GitHub review object** (`gh pr view 612 --json reviews`
  → `[]`). The independent pre-merge review demonstrably happened — the plan
  carries a full round-2 adversarial-verifier finding set with six findings and
  two honesty items, each disposed — but it is recorded in the ticket
  documents, not on the PR. Noted so the evidence location is not mistaken for
  its absence.
- **Minor accuracy note on the simplification pass.** Its reuse lens recorded
  "No retry helper exists anywhere in `Pegasus.IntegrationTests` — confirmed by
  grep". That is true as written (no *helper*), but it missed a same-shape
  precedent: `GroupedImageIntakeConcurrencyTests.cs:319-334` already runs an
  identical hand-rolled loop (`const int maximumAttempts = 5;` /
  `catch (SqlException exception) when (exception.Number == 1205 && attempt <
  maximumAttempts)`). The precedent *supports* the shape chosen here rather
  than contradicting it, so this is a record-accuracy note, not a defect and
  not a finding requiring disposition.

## Scope of this proof

Written against merged `dev` at `b92cb9a7` per decision D15. `main` has not been
promoted; the exact-SHA `dev` → `main` promotion happens at wave 5.

---

# Re-verification and acceptance closure — 2026-08-29, closeout board walk

## Scope (decision D15)

Re-verified against **merged `dev` at
`450b9234a6f5626f21adea3c4da244550a3bdace`** (2026-08-29 18:03:20 +0100).
`b92cb9a7`, the SHA the body above was written at, is an ancestor of it
(`git merge-base --is-ancestor b92cb9a7 450b9234` → exit 0), so nothing above
is invalidated.

This remains **dev-merged evidence, pending the single wave-5 `dev` → `main`
promotion**.

## The one Outstanding item is now discharged

The body above held this ticket in Verifying for one reason: its sole
acceptance item — *"Ten consecutive `sql-integration` runs without a
connection-timeout failure"* — stood at **3 of 10** complete workflow runs,
about 50 minutes after the merge. Eight hours later the evidence is in.

Every completed `ci.yml` run whose head commit has `2d67cefa` as an ancestor
was enumerated with `gh run list --workflow=ci.yml --limit 40` and
`git merge-base --is-ancestor`, and each run's `sql-integration` shard
conclusions were read with `gh run view <id> --json jobs`:

**29 completed workflow runs executed the `sql-integration` shards on the
fixed code. Zero of them failed with a connection-timeout.**

That is 10 consecutive runs met roughly three times over.

### Every shard failure that did occur was a different, named cause

This is the part that matters for honesty — a green *count* is worthless
without checking what the reds were. Each failing shard's log was fetched with
`gh run view --log-failed --job <id>`:

| Run / job | Failing test | Actual cause |
| --- | --- | --- |
| 33262541659 sh1 · 33248414281 sh2 · 33255824260 sh2 | `AdministrationSearchAccountWebTests.CanonicalAdministrationSearchAndPasswordRoutesRenderRealCallers`, `…AdministrationAndPasswordFormsRenderAntiforgeryTokens` | the `task/plat-026-mail-settings` lane's own in-flight work |
| 33262541659 sh2 · 33254603119 sh1 · 33246469257 sh1 · 33256486409 sh1 | `PrincipalCredentialPersistenceTests.IssueResetPauseResumeRevokeAreHashOnlyReplaySafeAndFailClosed` — `Assert.Null() Failure: Value is not null` | **[[DELIV-034]]**, the credential tamper no-op flake (~6.25 %) |
| 33247156620 sh1 | `IntakePersistenceIntegrationTests.CommittedMigrationCreatesTheSqlServerSchema` | the `task/eng-027-case-valuations` lane's in-flight migration |
| 33257817356, 33258113418, 33258498388, 33258576839, 33258657075 — **all four shards** | compile failure | **[[DELIV-035]]**, the `CS1739` `QueuedIntakeStatus` arity break (14:31–14:50, fixed at 15:22 by `55e23b02`). These runs never reached the tests, so they are not connection-timeout failures |

Not one `Connection Timeout Expired`, `pre-login handshake`, `post-login
phase` or `5061` lock-not-placed failure appears anywhere in the post-fix
window.

## Acceptance table, restated

| Item | Status | Evidence |
| --- | --- | --- |
| Ten consecutive `sql-integration` runs without a connection-timeout failure | **PASS** | 29 completed runs on the fixed code, zero connection-timeout failures; every red attributed by name to DELIV-034, DELIV-035, or an in-flight lane's own tests |

## Commands run, with exit codes

```
dotnet build ./Pegasus.slnx --configuration Release -nodeReuse:false
  -> Build succeeded. 0 Warning(s), 0 Error(s).   exit 0

gh run list --workflow=ci.yml --limit 40 --json databaseId,headSha,status   -> exit 0
gh run view <id> --json jobs -q '…sql-integration… .conclusion'            -> exit 0 (×36)
gh run view --log-failed --job <id>                                         -> exit 0 (×9)
```

## What this added evidence does NOT prove

- **It does not prove the flake is impossible**, only that it did not recur in
  29 runs. The pre-fix base rate was roughly one shard in a handful over four
  runs on 2026-08-28; 29 clean runs is a strong result against that base rate,
  not a proof of absence.
- **It is CI evidence, not deployed evidence.** This is a test-harness change
  that ships in no artifact; there is nothing to deploy and no production
  behaviour to observe.
- **The 5061 lock-wait duration is still unmeasured.** The 10 s backoff remains
  an order-of-magnitude choice. No 5061 recurred in the window, so it was not
  exercised either.
- **[[DELIV-033]]** was trigger-gated on this acceptance *failing*. The
  acceptance passed, so on its own stated trigger DELIV-033 closes as **not
  needed**. That disposition belongs to DELIV-033's own record; this proof only
  supplies the trigger evidence, and did not move it.
- **PR #612 still carries no GitHub review object.** The independent review is
  recorded in the ticket documents, not on the PR. Unchanged by this
  re-verification.
- **Two flakes remain live and are somebody's problem, not this ticket's:**
  **DELIV-034** (observed 4× in this window — the highest-frequency red in
  CI today) and **DELIV-036** (the Qdos regex production defect). Both have
  their own tickets and are in flight.
