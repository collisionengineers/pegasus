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

## Correction — 2026-08-29, after the cross-model pre-merge review

### The build claim above is stale; here is the current state

This report said the solution build "breaks on `origin/dev` today". That was
**true when written and is no longer true.** The break was
`ProviderSubmissionTests.cs:284` (CS1739, `QueuedIntakeStatus` no longer has
`CaseId`), inherited from `origin/dev` and never this lane's defect. [[DELIV-035]]
fixed it in PR #625, merge commit `55e23b02`, and this branch has merged that
`dev` forward.

Re-run by the orchestrator on the merged branch:

| Command | Result |
| --- | --- |
| `dotnet build ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release -nodeReuse:false` | **Build succeeded** |
| Reviewer's independent `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | exit 0, 0 warnings, 0 errors |

The historical failure is preserved above deliberately — rule 20 says a later
pass does not erase a failure — but the current state is a pass.

The PR body carries the same stale "currently fails" wording and should be read
against this correction.

### The flake rate this ticket exists to fix was itself misstated

The fix is correct; the *explanation* was not. The comment introduced by this
change said the no-op tamper happened "roughly one run in four", and the
pre-existing sibling comment in `ProviderApiSubmissionTests.cs` said "one run in
sixty-four". Both are wrong.

Measured over 200,000 sampled secrets generated the way
`PrincipalCredentials.cs:293-297` generates them:

```
distinct final characters: 16   (048AEIMQUYcgkosw)
P(last == 'A')             6.175 %   ->  1 in 16.19
secret tail length         43 characters
```

32 random bytes are 256 bits; base64 carries 6 bits per character, so the 43rd
and final character encodes only the leftover **4 bits** and has 16 possible
values, one of which is `'A'`. **The true rate is one run in sixteen.**

Both comments now say so, with the derivation, so this does not have to be
worked out a third time. The sibling file belongs to the merged TICK-058 lineage
and no in-flight lane owns it — corrected under D19 case 2, and flagged here
rather than done silently.

### The "no other instance" claim is now evidenced

The `files` document originally claimed the broader sweep found "only" the
Provider API match. It missed several. The orchestrator re-ran it across `src/`
and `tests/` and dispositioned every hit; the full table is in the plan's
"Pre-merge review dispositions" section. **Conclusion is unchanged — no other
site carries the defect — but it is now shown rather than asserted.** Two hits
that look similar are safe for structural reasons: `PrincipalCredentialsTests.cs:32`
truncates rather than substitutes (so the length always differs), and
`BoxDocumentContentStoreTests.cs:81,84` hashes two different string literals.
