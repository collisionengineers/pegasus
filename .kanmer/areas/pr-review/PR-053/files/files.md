# Files — PR-053

| Path | Change |
| --- | --- |
| `src/Pegasus.Web/Pages/Mail/Index.cshtml` | Remove only the new current-view hint and queue-only empty-state branch; keep the native labelled selector and existing list empty state. |
| `src/Pegasus.Web/Pages/Mail/Index.cshtml.cs` | Remove `ActiveViewLabel` if it has no remaining caller. |
| `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` | Assert one selected native option and absence of the new explanatory copy while preserving the queue key. |

No Core, SQL, policy, persistence, action, or governing-doc behavior changes.
