## Live interim repair — 2026-08-27 (read-only checks against prod SQL)

- 10:20:33Z operator re-saved the instructions mailbox in Administration › Mailboxes → `ApprovedMailboxes.ActivatedAtUtc = 10:20:33Z`, `Version 6`.
- 10:25 tick of `InboxRecoveryFunction`: `ApprovedMailboxSubscriptions` gained one row — `SubscriptionId 09018cc2-99a5-4084-a374-397a9b7d4560`, `LifecycleState Active`, `ExpiresAtUtc 2026-09-02 10:25Z`, resource = Inbox folder `/messages`; `ApprovedInboxPollStates.LastCompletedAtUtc = 10:25:19Z`, no failure code; `RetainedMailboxMessages` = 2; operator confirms the e-mails appear on the Mail page.
- This proves the recovery timer fires and the subscription path is wired; the migration in this ticket guards against the same de-activation on any database that walks the release-33 migration chain.
- Telemetry caveat: App Insights stopped ingesting Worker/Web records around 09:59Z / 05:31Z (MAIL-020), so the timer proof above is from the database, not App Insights.
