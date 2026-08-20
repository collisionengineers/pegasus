# Proof — MAIL-003

Type: command-log. Released in **release 14** (`d91fd7d7…`, PR #439), production smoke passed 2026-08-20; promoted to `main` (`39bb118a`).

- Live production readback post-deploy: `ApprovedSentPollStates` LastCompletedAtUtc **2026-08-20 12:29:15Z** with **NULL** LastFailureCode (completing every minute); zero `UnauthorizedAccessException` rows in App Insights post-deploy — the 2080-exception storm is gone.
- Verification lane at the cut: a not-approved mailbox now releases the lease with `sent_mailbox_not_approved` + backoff and returns an empty tick instead of throwing; regression test `NotApprovedMailboxIsHandledAsAnEmptyTickWithoutThrowing`. Root cause honestly recorded: the mailbox's SentEvidence approval had genuinely been removed 2026-08-10→2026-08-19; the code change removes the crash-on-rejection failure mode.
- Full transcript: DELIV-013 scratch.
