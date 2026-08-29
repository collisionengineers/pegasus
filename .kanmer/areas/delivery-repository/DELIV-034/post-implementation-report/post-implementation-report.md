## What changed

`tests/Pegasus.IntegrationTests/PrincipalCredentialPersistenceTests.cs`, inside
`IssueResetPauseResumeRevokeAreHashOnlyReplaySafeAndFailClosed`:

- Replaced the unconditional-append mutation
  (`firstSecret[..^1] + "A"`, a no-op whenever `firstSecret` already ends in
  `A`) with the guaranteed-mutation shape already established in
  `ProviderApiSubmissionTests.cs:67`:
  `firstSecret[..^1] + (firstSecret[^1] == 'A' ? 'B' : 'A')`.
- Added `Assert.NotEqual(firstSecret, tamperedSecret)` before the
  authenticate call, so a future change that makes the mutation a no-op again
  fails loudly instead of passing silently.
- The proving assertion itself —
  `Assert.Null(await authenticate.ExecuteAsync(firstKeyId, tamperedSecret,
  default))` — is unchanged in strength; only its input is now
  deterministically different from the real secret.

No production code changed. No new package, no new top-level directory.

## Sweep for the same shape

`git grep -n '\[\.\.\^1\]' -- tests/` and a broader
`wrong/invalid/bad/tampered(Secret|Key|Hash|Credential)` grep, each hit
checked individually — full disposition recorded in the ticket's `files`
document. Summary: no other instance has the defect.
`ProviderApiSubmissionTests.cs:67` already uses the correct shape (source of
the reused convention); `PrincipalCredentialsTests.cs:32` truncates rather
than replaces and is deterministically length-mismatched, not flaky;
`EvaSubmissionPolicyTests.cs` and `CaseEvaMapping.cs` slice unrelated
non-secret arrays.

## Build

Full-solution build (`dotnet build ./Pegasus.slnx --configuration Release
--no-restore`) reported **Build FAILED**, but the failure is a pre-existing,
unrelated defect on `origin/dev` outside this ticket's lane: `CS1739` in
`tests/Pegasus.Core.Tests/ProviderApi/ProviderSubmissionTests.cs:284`
(`QueuedIntakeStatus` has no `CaseId` parameter — the record's actual
parameter list is `StagedReceiptId, SourceFileName, ReceivedAtUtc, Status,
ProcessedReceiptId, FailureCode, RetryDueAtUtc`, per
`src/Pegasus.Core/Intake/DurableIntake.cs:93`). It reproduces on a clean
checkout of `origin/dev` at `cba29a4f` with no changes from this branch, so it
predates this ticket. **Not fixed here** (out of lane; reported, not
repaired) — every other project in the solution built clean, including
`Pegasus.IntegrationTests`, which carries this ticket's actual change:

```
Pegasus.Core -> ...Pegasus.Core.dll
Pegasus.Infrastructure -> ...Pegasus.Infrastructure.dll
Pegasus.Web -> ...Pegasus.Web.dll
WorkerExtensions -> ...WorkerExtensions.dll
Pegasus.Worker -> ...Pegasus.Worker.dll
Pegasus.IntegrationTests -> ...Pegasus.IntegrationTests.dll
Pegasus.ArchitectureTests -> ...Pegasus.ArchitectureTests.dll
Pegasus.Core.Tests -> FAILED: CS1739 (pre-existing, out of lane)
```

Standalone rebuild of the owning project succeeded clean:

```
dotnet build ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-restore
Build succeeded. 0 Warning(s) 0 Error(s)
```

## Test — ten consecutive real runs

`dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj
--configuration Release --no-build --filter
"FullyQualifiedName~PrincipalCredentialPersistenceTests"`, run ten times
independently after the fix:

| Run | Result | Duration |
| --- | --- | --- |
| 1 | Passed (1/1) | 44s |
| 2 | Passed (1/1) | 33s |
| 3 | Passed (1/1) | 1m |
| 4 | Passed (1/1) | 40s |
| 5 | Passed (1/1) | 37s |
| 6 | Passed (1/1) | 1m 2s |
| 7 | Passed (1/1) | 53s |
| 8 | Passed (1/1) | 43s |
| 9 | Passed (1/1) | 34s |
| 10 | Passed (1/1) | 2m 57s |

10/10 real, independently re-run, `Failed: 0` each time.

## Verification checklist

- [x] The tampered secret is asserted to differ from the real one before it
      is used (`Assert.NotEqual(firstSecret, tamperedSecret)`).
- [x] `IssueResetPauseResumeRevokeAreHashOnlyReplaySafeAndFailClosed` passes
      on ten consecutive runs (table above).
- [x] No other test mutates a secret or hash in a way that can produce the
      original (sweep above; full disposition in the `files` document).

## Simplification pass (2026-08-29)

n/a — two-line test diff reusing an existing in-repo convention verbatim
(`ProviderApiSubmissionTests.cs`); no reuse/simplification/efficiency/altitude
finding.

## Out-of-lane defect reported, not fixed

`tests/Pegasus.Core.Tests/ProviderApi/ProviderSubmissionTests.cs:284` — `CS1739`,
`QueuedIntakeStatus` object-initializer/positional call names a `CaseId`
parameter that does not exist on the record
(`src/Pegasus.Core/Intake/DurableIntake.cs:93`). Breaks the full-solution
build on `origin/dev` today, independent of this branch. Outside this
ticket's file (`Pegasus.Core.Tests`, not `Pegasus.IntegrationTests`) and
outside its scope (a Core.Tests compile error, not the credential-tamper
flake) — left untouched per the hard rule to report defects outside the lane
rather than fix them.
