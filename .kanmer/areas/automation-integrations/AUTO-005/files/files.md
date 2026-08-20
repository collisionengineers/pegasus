# Files — AUTO-005

## Where the change lands

| Path | Why |
|---|---|
| New `src/Pegasus.Web/Mcp/TriageMcpTools.cs` | Add typed list/detail/source and lifecycle adapters over existing Core Triage/intake/Case owners; no business rules live here. |
| `src/Pegasus.Web/Mcp/AutomationMcpExtensions.cs` | Register the Triage tool class in the configuration-gated MCP composition. |
| `tests/Pegasus.IntegrationTests/AutomationMcpIngressTests.cs` | Add the governed Triage tool names to the exact runtime inventory. |
| New focused `tests/Pegasus.IntegrationTests/AutomationTriageIngressTests.cs` | Prove HTTP success, denial, validation, replay, state/evidence guards, source retrieval, Case lease parity, attribution, and permanent history. |
| `docs/frd/frd-10-mcp-automation-and-actor-boundary.md` | Clarify the already-decided comprehensive ordinary-casework parity by naming the Triage inventory and its staff-identity exclusions; this is behaviour, not a new architectural decision. |
| `docs/capabilities.md` | Record the Triage Automation caller allocation/status without claiming delivery before evidence. |
| `docs/current-architecture.md` and `docs/operations.md` | Refresh the as-built and deployed inventory after implementation/release evidence. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `docs/adr/0011-restrict-mcp-to-automation-actor.md` | MCP calls the same Core use cases, has a distinct actor identity, and cannot create a second policy engine or staff impersonation route. |
| `docs/adr/0021-automation-actor-direct-write-assessment-contract.md` | Automation holds exactly `PerformCasework`; Pegasus owes a comprehensive typed toolset and logging parity, with explicit exclusions only. |
| `docs/frd/frd-03-triage.md` | Owns Triage states, findings, evidence, completion, assignment, Case association, cancellation, and reopen behaviour. |
| `src/Pegasus.Core/Identity/StaffAuthorization.cs` | The one access-level rule: Staff and Automation share `PerformCasework`; management/system/request-link rights do not. |
| `src/Pegasus.Core/Triage/TriageQueryUseCases.cs` | Existing authorised list/detail owners; both already accept Automation actors. |
| `src/Pegasus.Core/Triage/TriageContracts.cs` and `TriageLifecycle.cs` | Existing commands and all version/reason/replay/state/evidence rules; mutation actor strings must come from the resolved Automation actor, never tool input. |
| `src/Pegasus.Web/Pages/Triage/Index.cshtml.cs` | Staff list caller and queue/state filters; other queue tabs are not Triage. |
| `src/Pegasus.Web/Pages/Triage/Details.cshtml.cs` | Exact staff action inventory and the Case lease choreography to mirror through existing Core tools. |
| `src/Pegasus.Web/Pages/Triage/Details.cshtml` | Shows assignment is “to me,” explaining why an Automation/staff-assignment tool would not be parity. |
| `src/Pegasus.Core/Intake/DownloadIntakeSource.cs` | Shared integrity-checked retained-source owner, already Automation-authorised. |
| `src/Pegasus.Web/Mcp/CaseMcpTools.cs` | Existing Case get/edit-lease tools used before Triage Case association. |
| `src/Pegasus.Web/Mcp/AutomationActorResolver.cs`, `AutomationMcpAuditor.cs`, `AutomationMcpErrors.cs` | Required scope, actor, auditing, correlation, operation-key, and safe-error conventions. |
| `tests/Pegasus.IntegrationTests/QdosTriageIntegrationTests.cs`, `QdosTriageReplayIntegrationTests.cs`, `QdosTriageCaseAssociationIntegrationTests.cs` | Existing real-store fixtures and expected Triage outcomes/replay/Case association semantics to reuse. |

## Ripple effects

- The external MCP inventory grows under `automation.intake`.
- AUTO-004's plan/checklist must include both Unidentified and Triage and deliver them in one worktree/PR.
- Tool-level Automation action history supplements existing Triage domain history without a second Triage store.
- Exact response-evidence candidates may contain mailbox identities but no message mutation or external send is authorised.
- Production capability/as-built/operations claims change only after real caller and deployment evidence.

## Out of scope

No staff impersonation, Automation-as-assignee representation, arbitrary staff assignment, Triage creation from unaccepted material, mailbox mutation/send, report approval, professional Case-assessment confirmation, administration, generic queue/action framework, new scope, new store, or direct persistence access.
