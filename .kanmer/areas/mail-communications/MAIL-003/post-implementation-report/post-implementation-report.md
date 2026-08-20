## What was actually wrong

Root-caused by direct read-only inspection of `rg-pegasus-prod` (SQL query against
`ApprovedMailboxes`/`ApprovedSentPollStates`/`ActionHistory`, and the worker's `Graph__*` app
settings — no writes made). The comparison logic in `EfApprovedMailboxStore.IsApprovedAsync` was
already correct. `ActionHistory` for the `instructions@collisionengineers.co.uk` row shows an
administrator's 2026-08-10 update set `RouteScopes=[InboundIntake]` only, i.e. `AllowSentEvidence`
went to `false` for ~9 days, until a 2026-08-19 22:48 update restored `SentEvidence` and bound
`SentFolderIdentity`. `PollSentEvidence` was correctly rejecting a genuinely-unapproved mailbox during
that window — the 2,080-exceptions figure is that real rejection, not a bug in the check.
`ApprovedSentPollStates.LastCompletedAtUtc = 2026-08-20T03:34:15Z` with `LastFailureCode = NULL` and a
live Graph delta cursor confirms the poll completes successfully today with the current approved row.

## What was fixed

`PollSentEvidence.ExecuteAsync` threw an unhandled `UnauthorizedAccessException` on every tick whenever
the mailbox is unapproved for Sent-evidence — an expected administrative state, not a fault. Changed it
to release the lease (existing `sent_mailbox_not_approved` failure code, existing 30s backoff) and
return `PollSentEvidenceResult.Empty`, the same idiom the method already uses for "lease not due yet".
No new abstraction, no new result shape, no change to the approval-comparison logic.

## Files changed

- `src/Pegasus.Core/Workflow/PollSentEvidence.cs` — approval-check branch no longer throws; added
  `MailboxNotApprovedFailureCode` constant, reused by both the new release call and the existing
  `FailureCode` mapping (previously a duplicated literal).
- `tests/Pegasus.Core.Tests/Workflow/PollSentEvidenceTests.cs` — new
  `NotApprovedMailboxIsHandledAsAnEmptyTickWithoutThrowing` (red against pre-fix code, confirmed
  failing with the original `UnauthorizedAccessException` before the fix, green after); `CreateUseCase`
  gained an optional `IApprovedMailboxPolicy? policy` parameter; new `RejectingPolicy` fake.

## Test evidence

- `dotnet build ./Pegasus.slnx -c Release --no-restore` — Build succeeded, 0 Warning(s), 0 Error(s).
- `dotnet test .../Pegasus.Core.Tests.csproj --filter "FullyQualifiedName~PollSentEvidenceTests"` —
  13/13 passed (was 12 passed / 1 failed red before the fix).
- `dotnet test .../Pegasus.Core.Tests.csproj --filter "FullyQualifiedName~Workflow"` — 46/46 passed.
- `dotnet test .../Pegasus.ArchitectureTests.csproj` — 97/97 passed.
- `dotnet test .../Pegasus.IntegrationTests.csproj --filter "FullyQualifiedName~SentEvidencePollPersistenceTests"` — 2/2 passed.

## Scope note

`docs/frd/frd-08-email-mailbox-and-background-processing.md` does not describe this exception/skip
mechanic anywhere (checked — no matches), so no FRD change was needed.

Out of scope, tracked separately, not touched: the worker SIGABRT crash-loop (App Insights ingestion
for the whole worker stopped at 2026-08-19T11:48:15Z, which is why there is no post-fix telemetry to
compare against — a different defect from the approval check).
