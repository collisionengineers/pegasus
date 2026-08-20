# Plan — PLAT-012

## Chosen fix

Filter `MailActivityCounts.ReceivedToday` to `IntakeReceipts` rows with
`SourceChannel == "mailbox"`, using the codebase's own persisted channel
code and its one mapping function (`EfIntakeReceiptStore.ToCode`), widened
to `internal` for reuse — not a second copy of the channel-code strings.
`RetainedMailboxMessages` was considered (prod-diagnostics §3 confirms it
holds exactly the same 11 rows as mailbox-channel receipts) but rejected: it
would require a join/different DbSet for a result the existing
`IntakeReceipts` query already reaches with one more predicate.

## Steps

1. **`EfIntakeReceiptStore.cs`**: widen
   `private static string ToCode(IntakeSourceChannel value)` (line 1213) to
   `internal static`. No behaviour change.
2. **`EfDashboardQueries.GetMailActivityCountsAsync`**: change
   `receivedToday` to
   `.CountAsync(item => item.ReceivedAtUtc >= dayStartUtc && item.SourceChannel
   == EfIntakeReceiptStore.ToCode(IntakeSourceChannel.Mailbox), cancellationToken)`.
   Add a one-line comment: the "E-mail activity" tile counts mail arrivals,
   not every intake channel.
3. **Audit every other counter in `DashboardCounts.cs`** for the same
   defect (documented in `files`): only `ReceivedToday` is affected.
   `NeedsSorting` (unused in the UI) and `Unidentified` (deliberately spans
   media kinds per INTK-009) legitimately span channels and are left as-is.
4. **Test** — add
   `tests/Pegasus.IntegrationTests/DashboardCountersWebTests.cs` (new file;
   no existing dashboard-counter integration test exists to extend) with one
   test: seed one mailbox-channel `IntakeReceipts` row and one
   manual-upload-channel row, both `ReceivedAtUtc` today, via
   `IIntakeReceiptStore.StoreAsync` (the same store used by
   `TriageQueuesWebTests.StoreMinimalReceiptAsync`, just varying
   `IntakeSourceIdentity`'s channel). Assert `GET /` renders `ReceivedToday
   == 1`, not 2.
5. Build + focused test run (below).

## Verification commands

- `dotnet build ./Pegasus.slnx -c Release --no-restore`
- `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --filter "FullyQualifiedName~DashboardCountersWebTests"`

## Coordination

Branched from `origin/dev` (does not yet include INTK-013's PR #456, still
under review). This ticket's diff touches only
`GetMailActivityCountsAsync`; INTK-013 touches only
`GetCaseStageCountsAsync` in the same file — disjoint regions, so both PRs
can merge into `dev` independently regardless of order.

## Simplification pass

To be recorded after implementation, before PR, under a dated heading.
