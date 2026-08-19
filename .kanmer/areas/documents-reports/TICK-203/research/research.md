# Research — renderer MCP disposition

## Question

Should CollisionRenderer's standalone MCP tools be merged into Pegasus's existing Automation Actor MCP surface?

## Findings

1. Pegasus already has one authenticated streamable-HTTP MCP endpoint in `Pegasus.Web`. Its tools wrap existing Core case/intake/document/assessment use cases, use Automation Actor authorization, and intentionally expose no report approval or outward-dispatch tool. Sources: `src/Pegasus.Web/Mcp/**`, `docs/current-architecture.md`, ADR-0021/ADR-0026, FRD-10/FRD-11.
2. CollisionRenderer's workspace MCP is a standalone stdio/local-artifact host. Its render and output tools map transport requests directly to `CollisionRenderer.Core`, write local artifacts, expose local file descriptors, and include browser installation. Sources: `workspaces/report-renderer/src/CollisionRenderer.Mcp/**`, workspace MCP tests and manifest.
3. Those workspace tools bypass Pegasus case identity, accepted assessment readiness, Core authorization, immutable report version/hash/provenance, custody, idempotency, and human approval boundaries. Importing them would create a second business path and a second renderer caller.
4. The operator has now directed that CollisionRenderer is not separate and that a complete accepted assessment is the caller. EPIC-004 explicitly excludes a separate MCP host. The approved initial template scope is only the caller-backed rendererref1 assessment/fee-note families.
5. No MCP tool is required to satisfy the real caller. Report generation is an internal Core-owned application operation triggered by accepted assessment completion. Staff status/action belongs in the existing Web UI; Automation writes assessment data through existing guarded tools but cannot confirm findings, approve, issue, or dispatch a report.
6. If a future Automation tool is justified, it must invoke the same Core report use case and return application identities/status—not accept arbitrary templates/payloads, paths, or local output operations. That requires a separately allocated caller and authorization contract.

## Implications

- Retire `CollisionRenderer.Mcp`, its manifest/build script/tests, browser-install tool, local artifact output surface, and MCPB distribution from production integration.
- Do not add renderer tools to the existing Pegasus MCP inventory for this activation.
- Preserve one Core-owned report-generation use case as the only business path.
- Existing Automation assessment tools may supply unconfirmed data but cannot cause accepted report generation until ordinary staff/Engineer acceptance completes the readiness gate.
- TICK-214's long-term MCPB-host decision is resolved by the same constraint: no MCPB host in the integrated product.
