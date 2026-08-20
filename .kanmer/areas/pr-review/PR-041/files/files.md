# Files — PR-041

| Path | Change |
|---|---|
| `RetainedMail.cs`, `EfRetainedMailboxMessageStore.cs` | Project latest successful logical folder and let existing search include moved retained rows. |
| `Index.cshtml`, `Message.cshtml.cs` | State that search spans retained current folders and keep list-context semantics accurate. |
| `RetainedMailPersistenceTests.cs`, `MailWorkspaceWebTests.cs` | Prove Inbox exclusion, search inclusion, paging, mailbox scoping and immutable arrival evidence. |

Out of scope: new destination tabs, second search implementation, new folder taxonomy.
