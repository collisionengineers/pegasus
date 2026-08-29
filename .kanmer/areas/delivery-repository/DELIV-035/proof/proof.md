# Proof — DELIV-035: the `dev` build break is fixed

## Scope of this proof (decision D15)

Written against **merged `dev` at `450b9234a6f5626f21adea3c4da244550a3bdace`**
(2026-08-29 18:03:20 +0100).

This is **dev-merged evidence, pending the single wave-5 `dev` → `main`
promotion**. `main` serves release 36 (`783b4b88`). Per D15 the ticket walks to
Done on this evidence; the exact-SHA, non-force promotion to `main` happens
once, at wave 5.

For this ticket the distinction is narrow: the defect *was* a `dev`-only build
break, so `dev` is the only place it could exist and the only place a fix can
be proven.

## The work is on `dev`

PR [#625](https://github.com/collisionengineers/pegasus/pull/625) merged as
`55e23b02` (2026-08-29 17:12:12 +0100).

```
git merge-base --is-ancestor 55e23b02 450b9234   -> exit 0 (ancestor)
git show --stat 55e23b02
  tests/Pegasus.Core.Tests/ProviderApi/ProviderSubmissionTests.cs | 1 -
  1 file changed, 1 deletion(-)
```

## Capability → evidence

This ticket names no runtime capability. Its deliverable is a green build, so
rule 14's "named production caller" resolves to the build itself and to the
test that failed to compile. All three of its Verification items are checked
below with real exit codes.

| Ticket verification item | Status | Evidence |
| --- | --- | --- |
| `dotnet build ./Pegasus.slnx --configuration Release` is green | **PASS** | Run twice on `dev` at `450b9234` (below): `Build succeeded. 0 Warning(s), 0 Error(s).` exit 0. Zero `CS####` diagnostics — in particular no `CS1739` for `QueuedIntakeStatus` |
| `ProviderSubmissionTests` passes with its assertions unchanged | **PASS** | Included in the focused Core run below (49 passed / 0 failed). The diff deleted a constructor argument, not an assertion — `git show 55e23b02` shows one `-` line, `CaseId: null,` |
| No other construction of `QueuedIntakeStatus` uses the removed parameter | **PASS** | `git grep -n "QueuedIntakeStatus(" 450b9234 -- src/ tests/` returns exactly two sites: the record declaration `src/Pegasus.Core/Intake/DurableIntake.cs:93`, and one construction `tests/Pegasus.IntegrationTests/QdosIntakeWebTests.cs:136`. The latter compiles — the build is green — so it does not use the removed parameter |

`CaseId` is confirmed absent from the record at `450b9234`
(`DurableIntake.cs:93`–`:100`): the members are `StagedReceiptId`,
`SourceFileName`, `ReceivedAtUtc`, `Status`, `ProcessedReceiptId`,
`FailureCode`, `RetryDueAtUtc`. INTK-001's removal stands; nothing was
re-added.

## Commands run, with exit codes

Run in the main checkout on `dev`, Windows + PowerShell 7.

```
# first run, before dev advanced
dotnet build ./Pegasus.slnx --configuration Release -nodeReuse:false
  -> Build succeeded. 0 Warning(s), 0 Error(s).   exit 0

# re-run pinned at 450b9234
dotnet build ./Pegasus.slnx --configuration Release -nodeReuse:false
  -> Build succeeded. 0 Warning(s), 0 Error(s).   Time Elapsed 00:00:50.41
     exit 0

dotnet test ./Pegasus.slnx --configuration Release --no-build -nodeReuse:false
  --filter "…|FullyQualifiedName~ProviderSubmissionTests"
  -> Passed!  Failed: 0, Passed: 49, Skipped: 0, Total: 49
     - Pegasus.Core.Tests.dll (net10.0)     exit 0
```

Every project built: `Pegasus.Core`, `Pegasus.Infrastructure`, `Pegasus.Web`,
`Pegasus.Worker`, `Pegasus.Core.Tests`, `Pegasus.IntegrationTests`,
`Pegasus.ArchitectureTests`. `Pegasus.Core.Tests` — the one project that failed
before this fix — built clean.

Neither environment hazard occurred: no `MSB3027`/`MSB3021` "file is locked by
.NET Host" and no `SqlException` transport-level error. The result is a clean
PASS, not INCONCLUSIVE.

CI on the branch head `31ec8898` (run 33260069094): **success**, all four
`sql-integration` shards green.

## What this evidence does NOT prove

- **Nothing here is deployed**, and nothing needs to be: the change is in a
  test project that ships in no artifact. `main` is unaffected — the break
  never reached it.
- **The 49-test figure is an aggregate** across five filtered classes, not a
  per-class breakdown. What is proven is that none of them failed.
- **The full suite was not run.** Per instruction only focused filters were
  used; `Category=Browser` and the full `Category!=Corpus` sweep were not run
  here. The build — which is what this ticket is about — was full-solution.
- **This proof does not revalidate TICK-058 or INTK-001.** It proves only that
  their collision no longer breaks the build. TICK-058's own delivery status is
  assessed on its own record, where it is **held** (its capability sits behind
  `Features:ProviderApi`, closed in the deployed estate).
- **The board record was reconstructed retrospectively.** This ticket carried
  no `plan`, `files` or `post-implementation-report` when the closeout began,
  and no `commits`/`prs` were recorded on it. The post-implementation report
  and this proof were written from the merged result, not from a contemporaneous
  working record.
