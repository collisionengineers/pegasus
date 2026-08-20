# Post-implementation report — PLAT-012

## What changed

- `src/Pegasus.Infrastructure/Persistence/EfDashboardQueries.cs` —
  `GetMailActivityCountsAsync` now filters `receivedToday` to
  `SourceChannel == "mailbox"`, matching the E-mail activity section it
  renders under.
- `src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs` — widened
  `ToCode(IntakeSourceChannel)` from `private static` to `internal static`
  so the channel-code mapping is defined once and reused.
- `src/Pegasus.Core/Operations/DashboardCounts.cs` — documented that
  `ReceivedToday` is mailbox-channel only.
- `tests/Pegasus.IntegrationTests/DashboardCountersWebTests.cs` (new file) —
  `ReceivedTodayCountsMailboxChannelOnlyNotManualUploads`: seeds one mailbox
  and one manual-upload receipt, asserts the Dashboard's "Received today"
  tile reads 1, not 2.

## Counter audit

Every counter in `DashboardCounts.cs` was checked for the same
channel-mixing defect (recorded in the ticket's `files` doc):
`CaseStageCounts` and `CaseActivityCounts` legitimately span all origin
channels (a case/report is counted regardless of how it arrived);
`MailActivityCounts.NeedsSorting` is unused in the Web UI and legitimately
spans channels where it is used; `MailActivityCounts.Unidentified`
deliberately spans media kinds per INTK-009. Only `ReceivedToday` had the
defect described in the ticket.

## Test evidence

- `dotnet build ./Pegasus.slnx -c Release --no-restore` — Build succeeded, 0
  warnings, 0 errors.
- `dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj
  -c Release --no-build --filter "FullyQualifiedName~DashboardCountersWebTests"`
  — Passed: 1, Failed: 0.
- `dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj
  -c Release --no-build --filter "FullyQualifiedName~TriageQueuesWebTests"`
  — Passed: 3, Failed: 0 (regression; same infra file, disjoint method).

## Coordination with INTK-013

Branched from `origin/dev` before INTK-013 (PR #456) merged. This ticket's
diff touches only `GetMailActivityCountsAsync` and
`EfIntakeReceiptStore.ToCode`; INTK-013 touches only
`GetCaseStageCountsAsync` and `EfImageIntakeStore.ToCode` — disjoint regions
of the same two files, so the two PRs merge independently in either order
without conflict.

## Left out / parked

Nothing parked.
