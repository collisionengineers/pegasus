# Research — PR-057: reconcile the Automation MCP contract with one Export

## Question

What is the smallest authoritative documentation change needed when ENG-016 removes the two EVA-specific Automation MCP tools but retains the rest of the Automation Actor and Send to AI contracts?

## Findings

- `docs/adr/0021-automation-actor-direct-write-assessment-contract.md` is accepted and its decision 2 expressly requires `pegasus_eva_bundle_generate` and `pegasus_eva_handoff_status` as part of the Automation MCP inventory. Its decisions 1 and 3 also own the wider direct-write and Send to AI contracts.
- `AGENTS.md` requires a superseding ADR with the next free stable number and requires the old ADR to become `status: superseded`; editing the accepted decision in place or merely deleting its two tool names is not permitted.
- `docs/adr/README.md` and the directory census show ADR-0030 is the highest issued number, so the replacement is ADR-0031.
- The ENG-016 PR diff removes both tool records, handlers, injected EVA handoff dependencies, and their expected MCP inventory entries from `src/Pegasus.Web/Mcp/AssessmentMcpTools.cs` and `tests/Pegasus.IntegrationTests/AutomationMcpIngressTests.cs`. No other runtime registration for either exact tool name remains on the PR branch.
- The same PR makes staff Export the single EVA-package act through `IExportCaseBundle`. `docs/frd/frd-07-eva-and-external-engineering-handoff.md` and `docs/current-architecture.md` already describe manual Export as the current route and expressly say the separate Automation MCP EVA surface is removed.
- `docs/capabilities.md` is the remaining direct contradiction: MCP-06 still promises “EVA bundle generate and status” and attributes the contract to ADR-0021.
- Current direct-write and Send to AI descriptions still cite ADR-0021 in FRD-11, current architecture, capabilities, operations, design documentation, and source comments. Once ADR-0021 is formally superseded, current-contract citations should point to ADR-0031; historical records may continue to cite ADR-0021 as history.
- FRD-10 describes the general Automation MCP behaviour without naming either EVA tool, so it needs no behavioural rewrite. The expected-inventory integration test already proves the intended 33-tool surface and the absence of the two removed names.

## Implications

Use one concise ADR-0031 to replace ADR-0021 as the current Automation Actor contract. Carry forward the existing direct-write, safeguarding and Send to AI boundaries, but define the inventory without a separate EVA generate/status route: sending to engineering is the authenticated staff Export described by FRD-07. Mark ADR-0021 superseded by ADR-0031, update the ADR index, remove the two EVA promises from MCP-06, and update current-contract citations. Do not add an automation replacement, adapter, compatibility path, feature flag, or new export abstraction.

The implementation proof remains the existing MCP inventory test plus focused documentation searches; this ticket does not change export behaviour.

## Open questions

None. The operator has directed that the duplicate EVA automation surface be removed and that the unreleased repository converge on one simple target state.
