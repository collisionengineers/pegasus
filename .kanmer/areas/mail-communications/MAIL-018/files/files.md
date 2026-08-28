# MAIL-018 files

| File | Change |
| --- | --- |
| `src/Pegasus.Core/Identity/ApprovedMailboxSubscriptions.cs` | add `ListAsync(CancellationToken)` to `IApprovedMailboxSubscriptionStore` |
| `src/Pegasus.Infrastructure/Persistence/EfApprovedMailboxSubscriptionStore.cs` | implement `ListAsync` (AsNoTracking, ordered, via existing `Map`) |
| `src/Pegasus.Web/Pages/Administration/Mailboxes.cshtml.cs` | inject store; load subscriptions; `SubscriptionStatusFor(mailbox)` |
| `src/Pegasus.Web/Pages/Administration/Mailboxes.cshtml` | two new columns: Activated, Subscription |
| `tests/Pegasus.IntegrationTests/GraphMailWebhookTests.cs` | fake store gains `ListAsync` |
| `tests/Pegasus.IntegrationTests/ApprovedMailboxAdministrationWebTests.cs` | new test: activation + subscription values render; absent case renders plain values |
| `docs/design/test-ui/pages/administration-mailboxes--default.html` | regenerated snapshot (script, not hand-edited) |
