# Files — TICK-026

Surveyed before planning. Two tables, and the second is the one that earns its keep.

## Where the change lands

What this ticket will modify, and why each file is in scope.

| Path | Why |
| --- | --- |
| `tests/Pegasus.IntegrationTests/AutomationMcpIngressTests.cs` | Add MCP-04 success, validation, and `automation.documents` scope-denial HTTP tests beside the existing lease-conflict add test. Same factory, token, and JSON-RPC helpers. |

No production code change is expected. `DocumentMcpTools.cs` already wraps the Core ports.

## Context files

What an implementer must **read** to avoid a trap — files they will not necessarily edit.

| Path | What it tells the implementer |
| --- | --- |
| `src/Pegasus.Web/Mcp/DocumentMcpTools.cs` | Exact tool names, parameter contracts, 10 MiB / 20 MiB / 64 KiB limits, `automation:` identity rule, lease requirements. |
| `src/Pegasus.Web/Mcp/AutomationMcpExtensions.cs` | How `DocumentMcpTools` is registered; gate is `Features:AutomationMcp` + DevelopmentOffline. |
| `src/Pegasus.Web/Mcp/AutomationMcpErrors.cs` | Lease/version refusals name the guard and current version and never leak the token. |
| `src/Pegasus.Core/Documents/DocumentContracts.cs` | `CaseNotInReviewException`; export selections; `DocumentSource.Automation`. |
| `src/Pegasus.Infrastructure/Persistence/EfDocumentCustodyStore.cs` | Add is lease-guarded and Confirmed; export refuses any stage other than Review; replay matches operation key + content. |
| `tests/Pegasus.IntegrationTests/AutomationAssessmentIngressTests.cs` | Working `SeedAcceptedCaseAsync` + `pegasus_case_edit_begin` + structured-content assertions to copy. |
| `tests/Pegasus.IntegrationTests/AutomationMcpIngressTests.cs` | Existing `WithAutomationMcp`, token helper, `ARefusedDocumentToolReportsTheRefusingGuardAndTheCurrentCaseVersion`. |
| `docs/frd/frd-10-mcp-automation-and-actor-boundary.md` | Per-tool evidence bar this ticket must meet. |

## Ripple effects

- Callers: none. Staff pages and Core ports stay unchanged.
- Tests: `AutomationMcpIngressTests` is `SqlServer`-trait LocalDB; the new facts lengthen that class only.
- Docs: no `docs/` edit unless a current-state sentence is factually wrong (it is not).
- Sibling tickets [[TICK-023]] [[TICK-024]] [[TICK-025]] [[TICK-027]] also land in this test file later — do not take them while this PR is open.

## Out of scope

- New MCP host, MCPB, stdio proxy, or workspace package.
- Enabling the composition gate outside DevelopmentOffline.
- `ILogicallyRemoveDocument` / a delete tool.
- A dedicated document-list tool (inventory is on `pegasus_case_get`).
- Network-drive scanner client (FRD-10: client lives outside Pegasus).
- Tier-5 external Claude caller ([[TICK-023]]).
- MCP-05 / MCP-07 / [[SIMPLI-012]].
