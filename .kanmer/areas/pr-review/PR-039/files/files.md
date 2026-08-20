# Files — PR-039

| Path | Change |
|---|---|
| `RetainedMailFolderMove.cs`, `EfRetainedMailFolderMoveStore.cs` | Carry safe replay identity/reason and keep failure detail separate. |
| `Message.cshtml(.cs)` | Render a same-key “check move status” action for uncertainty. |
| `MailWorkspaceWebTests.cs`, `RetainedMailPersistenceTests.cs` | Prove destination/source/unresolved recovery through the authenticated caller without a second move. |

Out of scope: browser transport identity, automatic/background retry.
