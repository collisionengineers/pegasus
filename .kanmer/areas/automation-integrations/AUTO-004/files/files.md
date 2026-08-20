# Files — AUTO-004

## Where the change lands

| Path | Why |
|---|---|
| `src/Pegasus.Web/Mcp/AutomationMcpExtensions.cs` | Register `UnidentifiedMcpTools`; this is the immediate runtime reachability defect. |
| `src/Pegasus.Web/Mcp/UnidentifiedMcpTools.cs` | Reuse the existing list/get/resolve adapters, enrich exact detail from existing receipt/group sources, and add bounded exact-source/member retrieval without duplicating policy. |
| `tests/Pegasus.IntegrationTests/AutomationMcpIngressTests.cs` | Correct the canonical `tools/list` inventory so the three existing Unidentified tools cannot remain silently absent. |
| New focused `tests/Pegasus.IntegrationTests/AutomationUnidentifiedIngressTests.cs` (or the existing Automation ingress file if still proportionate) | Exercise real HTTP discovery/calls, scope denial, validation, action history, retained content, group-member selection, resolution parity, and integrity failure. |
| `docs/capabilities.md` | Correct MCP-03/related status wording to distinguish the narrow original queue-list/submission commitment from the separately proven Unidentified inventory. |
| `docs/current-architecture.md` and `docs/operations.md` | Refresh as-built/deployed tool inventory only after implementation and activation evidence; remove any claim that file presence alone made Unidentified reachable. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `docs/frd/frd-10-mcp-automation-and-actor-boundary.md` | Governs typed, scoped Automation tools and requires a real caller, denial, validation, and history proof; registration/file presence is insufficient. |
| `docs/frd/frd-02-intake-and-source-identity.md` | Requires one U-reference per occurrence/group while preserving every original member's bytes, identity, custody, and chronology. |
| `docs/frd/frd-03-triage.md` | Triage is distinct from Unidentified; AUTO-004 must not invent a Triage inventory. |
| `docs/adr/0011-restrict-mcp-to-automation-actor.md` | Requires MCP and Web to call the same Core use cases and forbids a parallel staff/business-policy surface. |
| `src/Pegasus.Web/Pages/Unidentified/Details.cshtml.cs` | Shows current staff orchestration: direct Unidentified store reads, shared `IResolveUnidentified`, and receipt enrichment through `IGetIntake`. |
| `src/Pegasus.Web/Pages/Intake/Source.cshtml.cs` | Shows the staff retained-source caller and its safe response/integrity-failure behavior. |
| `src/Pegasus.Core/Intake/DownloadIntakeSource.cs` | Existing integrity-checking Core owner for retained source bytes; it already accepts Automation actors through `PerformCasework`. |
| `src/Pegasus.Core/Intake/IntakeContracts.cs` | Defines `IGetIntake`, `IDownloadIntakeSource`, receipt evidence, source hashes, and download result contracts. |
| `src/Pegasus.Core/Intake/GroupedIntake.cs` | Defines durable group/member identity and the existing store operations needed for plural Unidentified origins. |
| `src/Pegasus.Core/Intake/Unidentified/UnidentifiedContracts.cs` | Owns U-reference parsing, states, reasons, history, versioning, resolution targets, and the one resolution command. |
| `src/Pegasus.Web/Mcp/DocumentMcpTools.cs` | Existing bounded-inline content response convention; reuse the convention, not the case-document identity model. |
| `src/Pegasus.Web/Mcp/AutomationActorResolver.cs`, `AutomationMcpAuditor.cs`, and `AutomationMcpErrors.cs` | Existing actor, scope, permanent-history, correlation, validation, and content-safe error conventions. |
| AUTO-003 research/files | Confirms broader mail actions are separately tracked and must wait for their landed Core owners. |
| TICK-025 research/report/proof | Demonstrates the prior false-positive evidence path: source presence and a passing incomplete inventory were treated as exposure. |
| INTK-007 research/report/proof | Shows the cross-cutting change that introduced the orphan class and the intended shared Core boundary. |

## Ripple effects

- MCP tool discovery and the externally visible approved inventory change.
- `automation.intake` scope and Automation action-history/security-event assertions gain Unidentified calls; no new scope is justified.
- Receipt-origin and SubmissionGroup-origin fixtures are both required.
- The production inventory/deployment evidence must be refreshed after release; no cloud write is part of research or local implementation.
- Existing Web behavior and `IResolveUnidentified` semantics must remain unchanged.
- [[AUTO-005]] owns the separate Triage authority decision; [[AUTO-003]] owns classified-mail caller parity.

## Out of scope

No Triage tools, mail-workspace tools, generic “material” framework, direct artifact-store access from MCP, new authorization scope, second U-reference parser/reason taxonomy, Case-document identity changes, live cloud/Outlook/Box writes, deployment, or claim that unexercised registration is delivered.
