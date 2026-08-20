# Files — PLAT-012

## Root cause (confirmed by prod-diagnostics §3 and code read)

- `src/Pegasus.Infrastructure/Persistence/EfDashboardQueries.cs`
  `GetMailActivityCountsAsync` (lines 82-107): `receivedToday` counts
  **every** `IntakeReceipts` row received today with no `SourceChannel`
  filter, even though the UI renders it under the "E-mail activity" section
  as "Received today" (`src/Pegasus.Web/Pages/Index.cshtml`, line ~48-52).
  Production: mailbox = 11 all-time / manual_upload = 14 all-time; the
  unfiltered count reports 25, not 11 — a manual image upload visibly
  inflates the emails-received tile, exactly the operator's report.
- `IntakeReceipts.SourceChannel` is the persisted snake_case channel code
  (`"mailbox"` / `"manual_upload"` / `"automation"`), written by
  `EfIntakeReceiptStore` and read back with a private
  `ToCode(IntakeSourceChannel)` (line 1213) /
  `internal static IntakeSourceChannel ParseSourceChannel(string)` (line
  1226) pair — the single place this mapping is defined.
- `RetainedMailboxMessages` (confirmed 11 rows, matching mailbox receipts
  exactly) is an alternative source but requires a join/different table for
  no benefit — `IntakeReceipts` already carries `SourceChannel` and
  `ReceivedAtUtc` together, so filtering the same query in place is the
  minimal fix and needs no new table access.

## Audit of every counter in `src/Pegasus.Core/Operations/DashboardCounts.cs`

- `CaseStageCounts` (NotReady/Review/Held) — sourced from `CaseWorkflows`,
  not receipt-channel scoped by nature (a case stage, not "material
  received"). No defect.
- `CaseActivityCounts` (NewCasesToday, SentToEngineer*, ReportsSent*) —
  "New cases today" legitimately counts cases from every origin channel (a
  case is a case regardless of how its evidence arrived); Sent-to-Engineer
  and Reports-sent are keyed on Case activity, not intake channel. No
  defect.
- `MailActivityCounts.ReceivedToday` — **defect, fixed here** (channel
  filter added).
- `MailActivityCounts.NeedsSorting` — counts `IntakeReceipts` where
  `Decision == NeedsSorting`, across every channel. This field is **not
  rendered anywhere in the Web UI** (verified: no `MailActivity.NeedsSorting`
  reference outside `DashboardCounts.cs` itself); the visible "Unidentified"
  tile reads the separately-computed `Unidentified` property instead.
  "Needs sorting" is legitimately a decision outcome that can arise from any
  channel (a manual upload can equally need sorting), not a per-channel
  receive count, so spanning all channels is correct — not the same defect.
  No change.
- `MailActivityCounts.Unidentified` — open `UnidentifiedItems`, which by
  design (INTK-009) spans both media kinds (image and email) as one queue;
  it is not a "received per channel" counter, so mixing origins here is the
  intended behaviour, not a channel-mixing bug. No change.

Conclusion: exactly one defect in `DashboardCounts.cs`'s counters —
`MailActivityCounts.ReceivedToday`.

## Files to change

- `src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs` — widen
  `private static string ToCode(IntakeSourceChannel value)` (line 1213) to
  `internal static`, so `EfDashboardQueries` reuses the one channel-code
  mapping instead of duplicating the `"mailbox"` literal (same convention
  already used for `ToCode(IntakeDecision)` on the line above it, and just
  applied to `ImageInitiatedCaseState` for INTK-013).
- `src/Pegasus.Infrastructure/Persistence/EfDashboardQueries.cs` —
  `GetMailActivityCountsAsync`: add
  `item.SourceChannel == EfIntakeReceiptStore.ToCode(IntakeSourceChannel.Mailbox)`
  to the `receivedToday` count's predicate.
- Tests: a new integration test asserting a manual-upload receipt does not
  change `ReceivedToday` while a mailbox receipt does, pinning the channel
  filter per the ticket's own verification checklist.

## Coordination note (both this ticket and INTK-013 touch `EfDashboardQueries.cs`)

INTK-013 (PR #456, not yet merged) edits `GetCaseStageCountsAsync` only.
This ticket edits the separate `GetMailActivityCountsAsync` method — a
disjoint region of the same file — so the two diffs do not overlap and can
merge independently in either order.
