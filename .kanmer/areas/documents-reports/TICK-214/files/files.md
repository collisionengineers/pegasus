# Files — MCPB boundary

| Path | Expected change | Risk |
| --- | --- | --- |
| `workspaces/report-renderer/src/CollisionRenderer.Mcp/**` | Retire | Hidden engine logic in mapping |
| `workspaces/report-renderer/tests/CollisionRenderer.Mcp.Tests/**` | Retire/migrate only reusable engine cases | Coverage loss |
| `workspaces/report-renderer/CollisionRenderer.sln` | Retire with workspace | Build inventory |
| `src/Pegasus.Web/Mcp/**` | No renderer tool addition | Tool inventory assertions |
| `tests/Pegasus.ArchitectureTests/**` | Assert no standalone renderer host/deployment | Boundary |

## Context files

| Path | Why |
| --- | --- |
| `TICK-203 research` | MCP reconciliation |
| `EPIC-004/context.md` | Binding no-separate-host direction |
| `docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md` | Monolith integration |
| `workspaces/report-renderer/src/CollisionRenderer.Mcp/manifest.json` | Retired distribution contract |

## Out of scope

- Creating replacement MCP tools.
- Maintaining local renderer output distribution.
