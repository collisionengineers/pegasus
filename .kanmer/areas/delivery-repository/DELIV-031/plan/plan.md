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

## Review findings — dispositions (round 2), 2026-08-29

Adversarial verifier, verdict `needs-work`. Six findings plus two honesty
items. Every one is disposed below; commands are named so a reviewer can
re-run them.

### [major] The DROP DATABASE retry is justified by CI evidence that does not exist (error 5061 never observed)

**Disposition: partly rejected on evidence, partly fixed.**

*Rejected —* the signature is real. The verifier sampled four jobs
(98823484553, 98804254607, 98801103945, 98797031780) and correctly found no
5061 in them. It did not sample **98903659122** (PR #594, `sql-integration
(3)`, 2026-08-28T16:03:49Z), which contains:

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

Those are the exact frames of the batch this change wraps. Reproduce with
`gh run view --log-failed --job 98903659122`. The error number was confirmed
independently, not assumed: `SELECT message_id FROM sys.messages WHERE
language_id = 1033 AND text LIKE 'ALTER DATABASE failed because a lock%'`
returns **5061** on LocalDB 16.0.1000.6, and `severity` is 16 — statement
level, so the connection survives and retrying on it is valid.

*Fixed —* the citation was still wrong, and the verifier is right that it was
wrong. "The second failure signature in the ticket's evidence" was false: the
DELIV-031 body names one signature only, and 5061 appears in a fifth, later
job the ticket never listed. Every occurrence of that claim is corrected in
the plan, `files.md` and `post-implementation-report`, and the plan now
carries a premise table naming the check behind each claim.

### [major] The mandatory simplification pass was never run or recorded

**Disposition: fixed.** Run over `git diff origin/dev...HEAD` and recorded
above under "Simplification pass — 2026-08-29" with four lenses and per-lens
dispositions. It produced three real changes (removed
`ConnectRetryCount`/`ConnectRetryInterval`, named the 5061 constant, split the
conflated comment) and two recorded rejections. The stale
"n/a at plan time" promise is gone.

### [major] The approach the ticket explicitly dispreferred was delivered, with no recorded justification

**Disposition: readiness gate rejected on evidence; connection-retry deferred
to [[DELIV-033]]; timeout raise accepted with the reason recorded.**

*Readiness gate — rejected, with evidence.* Two independent checks:

1. **No failure happened at startup.** First-failure offsets from `Test run
   for` in the five logs are 3m27s, 4m50s, 5m01s, 5m27s and 6m29s. A gate
   that waits for LocalDB before the run addresses none of them.
2. **There is nothing to gate.** Neither `.github/workflows/ci.yml` nor
   `scripts/Invoke-TestShard.ps1` starts or waits on LocalDB —
   `grep -n "localdb\|sqllocaldb"` returns one comment and no command. LocalDB
   is `Auto-create: Yes` and starts implicitly on first connect.

Adding one would be a gate that gates nothing (AGENTS.md rule 21).

*Connection-retry policy — deferred to [[DELIV-033]],* which records both
candidates (EF Core `EnableRetryOnFailure`, SqlClient configurable retry
logic), the transaction-conflict risk, and the fact that it needs a full
suite run this lane must not perform. It is trigger-gated on DELIV-031's own
acceptance failing, so it is not speculative work.

*Timeout raise — accepted, and the masking risk answered.* "Never mask real
failures" holds: 60s is still 5x inside `LifecycleCommandTimeoutSeconds`
(300s) and 20x inside the 20-minute job timeout, so a genuinely wedged
instance still fails the job rather than hanging it. What the raise removes is
a budget that sat *exactly* on the measured contention window (15s vs
13999-14014 ms) — that is a mis-sized budget, not a real failure being hidden.

*The two unaddressed Approach bullets.* "Inspect the runner's SQL Server
startup/health (readiness wait, pool size, `MaxParallelThreads`, timeouts)" is
now done and is what produced the evidence above —
`xunit.runner.json: maxParallelThreads: 4`, three shards on three separate
runners per `ci.yml:163-168`, no readiness step anywhere. "Record the rerun
counts in the PR checks as evidence" is the ticket's own post-merge acceptance
and is carried to `verifying`.

### [minor] The retry backoff budget is inconsistent with the contention window the same commit asserts

**Disposition: fixed.** The verifier is right that 250+500+750+1000 = 2.5s
could not outlast a ~14s window. Two changes: the backoff is now
`TimeSpan.FromSeconds(attempt)` (1+2+3+4 = 10s across 5 attempts), and the
comment no longer borrows the connect-stall measurement to justify the drop
retry — the two failures now each carry their own comment. Stated honestly:
the lock-wait duration itself was never measured, so 10s is an
order-of-magnitude choice against the contention window observed on the same
instance, not a measurement.

### [minor] ConnectRetryCount/ConnectRetryInterval is mischaracterised and probably inert

**Disposition: fixed by removal.** The verifier is right on both counts, and
Microsoft's own documentation settles it: "Open connection resiliency refers
to the initial `SqlConnection.Open` or `OpenAsync()`" — so the plan's
"built-in idle-connection retry" was wrong — but the properties "retry only
the built-in list of transient connection errors" (4060, 40197, 40501, 40613,
49918-49920, 64, 233 …), which does not contain the client-side -2 this ticket
targets. Grepping all five failure logs for that entire family returned **zero
hits**, so the settings covered nothing observed. Removed rather than
retained-and-explained: keeping an unevidenced knob is exactly the "gate that
gates nothing" AGENTS.md rule 21 forbids. The effective lever is the
`ConnectTimeout` raise alone, and the record now says that plainly. The
documented sizing rule (`Connection Timeout >= ConnectRetryCount *
ConnectRetryInterval`) becomes moot.

### [minor] files.md dismisses other connection strings on reasoning that is right for the wrong stated reason

**Disposition: fixed.** The verifier is right that the recorded argument was
about non-competing literals and never asked whether the patched builder
actually reaches the failing classes. `files.md` now records that check as its
own section, verified rather than asserted: `OrganizationAdministrationWebTests`
and `AutomationConnectorAuthorizationTests` construct
`IntakeWebApplicationFactory`, which holds a `LocalDbTestDatabase`
(`IntakeWebTestSupport.cs:41,92`); `CaseTaskArchivePersistenceTests` and
`IntakePersistenceIntegrationTests` construct `LocalDbTestDatabase` directly.
All four therefore route through the patched `BuildConnectionString`.

### [honesty] "Per the ticket's explicit Do NOT list" — no such list exists

**Disposition: fixed.** Correct: the DELIV-031 record has What/Why/Approach/
Verification and no Do NOT list. The constraint came from the orchestrator's
lane brief. Corrected in the plan's "Out of scope" and in `files.md`;
`post-implementation-report` no longer attributes it to the ticket.

### [honesty] Root cause described the contention as cross-shard

**Found while re-checking, not raised by the verifier.** `ci.yml:163-168`
gives each shard its own `windows-latest` runner, so shards never share a
LocalDB instance. The real contention is the four parallel xUnit collections
*within* one shard. The plan's root cause is corrected.

### Verification after remediation

- `dotnet build ./Pegasus.slnx --configuration Release` — exit 0, 0 Warning(s),
  0 Error(s).
- `--filter "FullyQualifiedName~IntakePersistenceIntegrationTests"` —
  Failed: 0, Passed: 10, Skipped: 0, Total: 10 (1m16s).
- `--filter "FullyQualifiedName~CaseTaskArchivePersistenceTests|...
  OrganizationAdministrationWebTests|...AutomationConnectorAuthorizationTests|
  ...LocalDbTemplateDatabaseTests"` — Failed: 0, Passed: 48, Skipped: 0,
  Total: 48 (2m31s). Added this round: these are the classes that actually
  failed in CI, including the one that raised the 5061.

Net diff after remediation: one file, +26/-2 (was +22/-2). No assertion was
weakened, skipped, deleted or inverted at any point.
