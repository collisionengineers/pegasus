# Files — MAIL-017

| Path | Role |
| --- | --- |
| `src/Pegasus.Infrastructure/Persistence/Migrations/20260826151807_ApprovedMailboxStableIdentityAndSubscriptions.cs:158-163` | The defect: `UpdateData(ActivatedAtUtc = null)` on the seeded, identity-bound mailbox. Read only. |
| `src/Pegasus.Infrastructure/Persistence/AdministrationPolicyModelConfiguration.cs:42-51` | Seed omits `ActivatedAtUtc`; unchanged (a seed change would regenerate `UpdateData`). |
| `src/Pegasus.Infrastructure/Persistence/EfApprovedMailboxStore.cs:37-39,72,211-215` | Consumers requiring activation; admin re-save path that sets it. Read only. |
| `src/Pegasus.Infrastructure/Persistence/EfApprovedMailboxSubscriptionStore.cs:27,46` | Subscription maintenance / webhook lookup filters. Read only. |
| `src/Pegasus.Infrastructure/Persistence/EfApprovedInboxPollStore.cs:43-48,78-88` | Poll claim and cursor reset on activation change. Read only. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/<stamp>_ReactivateBoundApprovedMailboxes.cs` (+ `.Designer.cs`) | **New** raw-SQL data-repair migration. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs` | Unchanged (no model change); asserted by build. |
| `docs/operations.md` | Release-33 entry gains the de-activation defect; MAIL-017 named as the repair. |
