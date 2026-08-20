# Proof — PLAT-012

Type: command-log + visual. Released in **release 14** (`d91fd7d7…`, PR #457), production smoke passed 2026-08-20.

- Live: Dashboard "Received today" shows **1** (the day's single mailbox receipt); the day's manual uploads no longer inflate it. SQL cross-check: IntakeReceipts today = 1 mailbox vs manual_upload rows excluded.
- Verification lane: `receivedToday` filtered `SourceChannel == mailbox` via `EfIntakeReceiptStore.ToCode(IntakeSourceChannel.Mailbox)`; test `ReceivedTodayCountsMailboxChannelOnlyNotManualUploads` present at the cut.
- Full transcript: DELIV-013 scratch.
