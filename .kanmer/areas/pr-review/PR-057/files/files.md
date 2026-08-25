# Files — PR-057

## Where the change lands

| Path | Why |
|---|---|
| `docs/adr/0031-automation-actor-contract-without-eva-export-tools.md` | New current decision: retain the Automation Actor direct-write and Send to AI boundaries, remove the two EVA-specific tools, and make staff Export the sole EVA-package act. |
| `docs/adr/0021-automation-actor-direct-write-assessment-contract.md` | Set `status: superseded` and `superseded_by: [ADR-0031]`; preserve its body as historical context. |
| `docs/adr/README.md` | Move ADR-0021 to the superseded table and add accepted ADR-0031 with its owner capabilities. |
| `docs/capabilities.md` | Reconcile MCP-06 with the real assessment/case-detail inventory, remove the EVA generate/status promise, and cite ADR-0031. Update other present-tense ADR-0021 citations where they describe the carried-forward contract. |
| `docs/current-architecture.md` | Cite the current ADR for the retained Send to AI/Automation contract; retain its already-correct 33-tool and one-Export description. |
| `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` | Point the retained direct-write Send to AI behaviour at ADR-0031; no feature-behaviour change. |
| `docs/design/README.md` | Update the current Automation assessment-contract citation. |
| `docs/operations.md` | Update present-tense operational contract citations without rewriting historical deployment evidence. |
| `src/Pegasus.Core/AiWork/AiWorkContracts.cs` | Update the source comment that identifies the active transport decision. |
| `src/Pegasus.Core/Assessment/AssessmentContracts.cs` | Update the source comment that identifies the active direct-write decision. |
| `src/Pegasus.Web/Mcp/AssessmentMcpTools.cs` | Update its active ADR comment; the tool removals themselves are already in ENG-016. |
| `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml` | Update the current design-authority citation. |
| `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs` | Update the current Send to AI contract citation. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `AGENTS.md` | ADR IDs are stable; supersession requires a new ADR and a superseded old status. It also forbids compatibility machinery without a current consumer. |
| `docs/adr/0026-enable-automation-mcp-by-explicit-deployment-configuration.md` | Production activation amends ADR-0021's old DevelopmentOffline limitation and must remain carried into the replacement contract. |
| `docs/adr/0027-authorization-code-for-external-mcp-connectors.md` | Authentication refinement remains active and is not changed by removal of EVA tools. |
| `docs/frd/frd-07-eva-and-external-engineering-handoff.md` | Staff Export is the one current send-to-engineering route and no EVA network client exists. |
| `docs/frd/frd-10-mcp-automation-and-actor-boundary.md` | General MCP scopes, attribution and guard parity continue; it does not require the removed EVA tools. |
| `tests/Pegasus.IntegrationTests/AutomationMcpIngressTests.cs` | The expected inventory on the ENG-016 branch is 33 tools and excludes both EVA-specific names. |
| `src/Pegasus.Web/Mcp/AssessmentMcpTools.cs` | ENG-016 already removes the two handlers and their obsolete EVA dependencies; PR-057 should not reimplement or replace them. |
| `docs/open-decisions.md` | Its ADR-0021 reference records the dated 2026-08-03 decision history and should remain historical rather than being rewritten. |
| `docs/adr/0013-qdos-alpha-implementation-contract.md` | Its links describe later historical supersession/amendment and need not be mechanically rewritten. |

## Ripple effects

- ADR frontmatter and the ADR index must agree exactly about supersession.
- A repository search must leave no present-tense claim that MCP-06 includes EVA generation/status.
- The existing Automation MCP inventory integration test remains the runtime contract proof; no new test framework or duplicate inventory is required.
- ENG-016's PR/report must include this documentation commit before its blocker can close.

## Out of scope

- Reintroducing an Automation MCP export action, exposing staff Export through MCP, or designing the future EVA API/direct estimating-system routes.
- Changing Export readiness, package contents, persistence, Box reads, replay handling, or migrations; those belong to the other ENG-016 review tickets.
- Rewriting historical ADR references whose purpose is to document what was decided at that time.
- Release/deployment documentation changes beyond updating current-contract citations; live state is changed only by the later release workflow.
