# Files — PR-054

| Path | Change |
| --- | --- |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` | Consolidate and invoke the existing folder/queue validation before every exact-message POST business call. |
| `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` | Authenticated forged-context tests cover all six handlers for unknown and Deleted+queue contexts and prove classification/move/association/lease state does not change. |

No second parser, filter, authorization layer, Core/EF/schema change, or external call.
