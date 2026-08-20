## Files touched

- `src/Pegasus.Core/Workflow/PollSentEvidence.cs` — the approval-check branch in `ExecuteAsync` (currently throws `UnauthorizedAccessException`); the `FailureCode` mapping.
- `tests/Pegasus.Core.Tests/Workflow/PollSentEvidenceTests.cs` — new regression test for the unapproved-mailbox outcome; extend `CreateUseCase` to accept an injectable `IApprovedMailboxPolicy`.

## Files read (context, not touched)

- `src/Pegasus.Infrastructure/Persistence/EfApprovedMailboxStore.cs` — `IsApprovedAsync` (Address/State/AllowSentEvidence match, confirmed correct against the exact production row).
- `src/Pegasus.Infrastructure/Persistence/EfSentEvidencePollStore.cs` — `ClaimAsync`/`ReleaseAsync` lease lifecycle.
- `src/Pegasus.Infrastructure/Persistence/AdministrationPolicyModelConfiguration.cs` — seed row (`AllowSentEvidence=false` at creation; irrelevant to the live defect, since the row has since been updated by an administrator — see research).
- `src/Pegasus.Worker/MailboxFunctions.cs` (`InboxPollFunction`), `src/Pegasus.Core/Intake/MailboxIntake.cs` (`PollApprovedInbox`) — the existing "expected-skip" convention: `ListPollableAsync` pre-filters to approved mailboxes only, so an unapproved mailbox is never seen as a failure, just absent from the list.
- `src/Pegasus.Worker/EmailEvidenceFunctions.cs` (`SentEvidencePollFunction`) — logs `PollSentEvidenceResult` at Information level; already treats an all-zero `Empty` result (the "nothing due" case) as a normal, silent outcome, so returning `Empty` for "not approved" needs no Worker-side change.
