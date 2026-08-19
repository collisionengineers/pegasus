# Files — renderer MCP disposition

## Change files

| Path/module | Expected change | Risk |
| --- | --- | --- |
| `workspaces/report-renderer/src/CollisionRenderer.Mcp/**` | Exclude and remove during workspace migration | Losing useful engine behavior hidden in host mapping |
| `workspaces/report-renderer/tests/CollisionRenderer.Mcp.Tests/**` | Retire host tests; migrate only engine-level contract cases that remain relevant | Accidental loss of payload validation coverage |
| `workspaces/report-renderer/CollisionRenderer.sln` | Retired with workspace integration | Build inventory |
| `src/Pegasus.Web/Mcp/**` | No renderer tool addition; update inventory tests/docs only if needed to prove absence | Authorization surface drift |
| `src/Pegasus.Core/Reports/**` | One internal report use case and contracts | Must remain transport-neutral |
| `src/Pegasus.Web/Pages/Cases/Assessment/**` | Existing staff caller/status surface | Must preserve human acceptance |
| `tests/Pegasus.ArchitectureTests/**` | Prove no standalone MCP/API/CLI renderer production host | Boundary regression |
| `docs/current-architecture.md`, `docs/operations.md` | Record actual integrated surface after delivery | Evidence tier |

## Context files

| Path | Why read it |
| --- | --- |
| `docs/adr/0021-automation-actor-direct-write-assessment-contract.md` | Automation cannot confirm professional findings or approve/send reports |
| `docs/adr/0026-automation-mcp-composition-gate.md` | Existing single MCP composition and activation |
| `docs/frd/frd-10-mcp-automation-and-actor-boundary.md` | MCP behavior/authorization |
| `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` | Report generation/approval/finality |
| `EPIC-004/context.md` | Binding no-separate-MCP direction |
| `workspaces/report-renderer/src/CollisionRenderer.Mcp/**` | Standalone local host being retired |

## Out of scope

- A new Automation report-generation tool.
- MCP report approval, sending, arbitrary payload rendering, local file access, or browser installation.
- Preserving the standalone MCPB distribution.
