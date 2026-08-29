# Files — AUTO-006

## Changed

| Path | Change |
| --- | --- |
| `src/Pegasus.Web/Pages/Administration/Automation/Index.cshtml` | rewritten onto the design system: `page-header`, `admin-layout` + `_AdminNav`, Automation panel (status chip, Registered clients / Active jobs / Failed jobs fact grid, danger Stop/Start opening `_ReasonDialog`), AI settings panel (one form: Channel address, Timeout, New channel token, enabled checkbox, Reason, Save), Remove-token danger control behind a second reason dialog |
| `src/Pegasus.Web/Pages/Administration/Automation/Index.cshtml.cs` | adds `AutomationComposed` and `JobCounts` (from `IAiJobQueries.GetCountsAsync`); replaces `SetSendToAiEnabled` + `UpdateConnector` + `RotateChannelToken` with one `SaveAiSettings` handler; keeps `SetEnabled` (kill switch) and `ClearChannelToken` |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | **append only**, new nested `static class Automation` inside `OperatorLabels`; no existing member reordered |
| `tests/Pegasus.IntegrationTests/AutomationAdministrationWebTests.cs` | **new** — the focused web test for this page |
| `tests/Pegasus.IntegrationTests/SendToAiConnectorAdministrationTests.cs` | the two connector posts retargeted at the single `SaveAiSettings` handler; every assertion kept |

## Read, not changed

- `src/Pegasus.Web/Pages/Administration/Shared/_AdminNav.cshtml` (shared by PLAT-025/026/027/028)
- `src/Pegasus.Web/Pages/Shared/_ReasonDialog.cshtml`, `_StatusChip.cshtml`, `_PageHeader.cshtml`
- `src/Pegasus.Core/AiWork/AiJobs.cs`, `AiWorkContracts.cs`
- `src/Pegasus.Web/Mcp/AutomationClientRegistry.cs`, `AutomationMcp.cs`
- `src/Pegasus.Web/wwwroot/css/site.css` (class vocabulary only)
- `src/Pegasus.Web/Pages/Operations/Index.cshtml` (ported reference)

## Deliberately untouched

- `src/Pegasus.Web/Pages/Administration/Automation/Activity.*` — superseded by
  §1.14; PLAT-051 replaces it, UIIMP-009 deletes it. The Index page drops its
  link to it; the inherited PLAT-015 defects inside it are reported, not fixed.
- every other `Pages/Administration/**` folder, `site.css`, `site.js`,
  `Pages/Shared/**`, `docs/design/test-ui/**` (snapshots regenerate once per
  merge, on the merging branch).
