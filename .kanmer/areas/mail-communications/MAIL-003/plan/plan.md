## Root cause (verified read-only against production)

The approval-check comparison in `EfApprovedMailboxStore.IsApprovedAsync` is **not** buggy: Address
normalisation, State, and the `AllowSentEvidence` flag are compared correctly, and a live query of
`rg-pegasus-prod` / `pegasus-prod-sql-252ow37gij` confirms the row for
`instructions@collisionengineers.co.uk` (Id `49f47eb9-c5b0-464f-b8f0-8c90ba061728`) now matches the
worker's `Graph__MailboxId`/`Graph__SentFolderId`/`Graph__MailboxAddress` app settings exactly.

`ActionHistory` for that row shows why the exceptions happened:
- 2026-08-10 22:27 (v2→v3): an administrator bound `MailboxIdentity`/`InboxFolderIdentity` but the
  update's `RouteScopes` was `[InboundIntake]` only — **`AllowSentEvidence` went to `false`** and
  `SentFolderIdentity` stayed null.
- 2026-08-19 22:48 (v3→v4): a later update added `SentEvidence` back to `RouteScopes` and bound
  `SentFolderIdentity`. From this point the row is fully approved for Sent-evidence.

So for the ~9 days between those two updates, `instructions@collisionengineers.co.uk` genuinely was
not approved for Sent-evidence polling, and `PollSentEvidence.ExecuteAsync` was correctly rejecting it
— the 2,080-in-48h count reflects that (real) rejected window, not a comparison bug. Application
Insights ingestion for the worker stopped at 2026-08-19T11:48:15Z (separate SIGABRT crash-loop issue,
already tracked elsewhere — not in this ticket's scope) so there is no telemetry to compare against
after the row was fixed, but `ApprovedSentPollStates` shows `LastCompletedAtUtc = 2026-08-20T03:34:15Z`
with `LastFailureCode = NULL` and a live Graph delta cursor — proof the poll is now completing
successfully with today's approved row. **No change to the comparison logic itself is needed or made.**

What the ticket's second requirement stands on regardless: an administrator-disabled or
not-yet-approved mailbox is an expected state (visible in the exact 9-day window above), and today it
makes `PollSentEvidence.ExecuteAsync` throw an unhandled `UnauthorizedAccessException` every poll tick
— exactly the exception-storm/telemetry-noise problem the ticket calls out. That is the one real fix.

## Fix (reuses the existing "empty tick" idiom already in this method)

`PollSentEvidence.ExecuteAsync` already returns `PollSentEvidenceResult.Empty` without error for "lease
not due yet" (`lease is null`). The convention elsewhere (`PollApprovedInbox` via
`IApprovedIntakeMailboxes.ListPollableAsync`) never even sees an unapproved mailbox — it is filtered
out before polling starts. `PollSentEvidence` is a single, config-driven mailbox, so the equivalent
shape is: on a failed approval check, release the already-claimed lease with the existing
`sent_mailbox_not_approved` failure code (same string the `FailureCode` mapping already produces for
this case, now pulled into one named constant instead of duplicated) and the existing 30s
`FailureRetryDelay` backoff, then return `PollSentEvidenceResult.Empty` — no throw, no unhandled
exception, no new abstraction, no new result shape.

1. `src/Pegasus.Core/Workflow/PollSentEvidence.cs`: add `private const string
   MailboxNotApprovedFailureCode = "sent_mailbox_not_approved";`. Replace the
   `throw new UnauthorizedAccessException(...)` in the approval-check branch with
   `await pollStore.ReleaseAsync(lease.MailboxId, lease.LeaseToken, timeProvider.GetUtcNow().Add(FailureRetryDelay), MailboxNotApprovedFailureCode, cancellationToken); return PollSentEvidenceResult.Empty;` (inside the existing `try`, so no other control flow changes). Point the `FailureCode` switch's
   `UnauthorizedAccessException => "sent_mailbox_not_approved"` arm at the same constant (one list, one
   place) — kept because other calls inside the try (`recordEmailResponseEvidence`,
   `retainReportEvidence`, `autoLinkReportEvidence`) can still throw `UnauthorizedAccessException` for
   unrelated staff-authorization reasons and that mapping stays meaningful for them.
2. `tests/Pegasus.Core.Tests/Workflow/PollSentEvidenceTests.cs`: add
   `NotApprovedMailboxIsHandledAsAnEmptyTickWithoutThrowing` — a fake `IApprovedMailboxPolicy` returning
   `false` for the lease's address+`SentEvidence` scope (mirroring the production row shape:
   `AllowSentEvidence=false`, matching address). Asserts (red before the fix, green after): the call
   does not throw; the result equals `PollSentEvidenceResult.Empty`; `pollStore.Releases` has exactly
   one entry with `FailureCode == "sent_mailbox_not_approved"` and `DueAtUtc == NowUtc + 30s`; the
   `IApprovedSentSource` is never called (`source.CallCount == 0`); no outcome is recorded. Extend
   `CreateUseCase` with an optional `IApprovedMailboxPolicy? policy = null` parameter (defaults to the
   existing `ApprovedPolicy()`) so this is the only test-fixture change needed.

## Verification

- `dotnet build ./Pegasus.slnx -c Release --no-restore`
- `dotnet test ./Pegasus.slnx --filter "FullyQualifiedName~PollSentEvidenceTests"`
- Confirm the new test fails against the pre-fix code (red) and passes after (green) before committing
  the fix.

## Simplification pass (2026-08-20)

Applied inline as part of the single-purpose fix — no separate pass needed: the fix reuses
`PollSentEvidenceResult.Empty` (existing idiom), the existing `ReleaseAsync`/backoff mechanism, and
collapses a duplicated failure-code string literal into one constant. No new abstraction, no new
dependency, no new result type. Nothing further to simplify.
