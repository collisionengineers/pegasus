# Plan — TICK-203: Reconcile the renderer MCP design against the merged Automation Actor inventory

## Approach

Treat TICK-203 as a decision-only prerequisite already subsumed by [[SIMPLI-014]], not as a second repository implementation. The completed research resolves the question: the integrated product gains no renderer MCP tool or MCPB host; accepted-assessment rendering is one internal Core-owned application path, while the existing Automation Actor MCP inventory remains unchanged. SIMPLI-014 is actively implementing that disposition by retiring the standalone CollisionRenderer MCP/MCPB surface and adding no HTTP, Razor, MCP, CLI, or second host. This ticket therefore owns only confirmation and traceability after SIMPLI-014 completes. It must not create a branch/worktree or modify any repository file independently, because its surveyed files and intended removals overlap the active SIMPLI-014 claim.

## Governing docs

- **Meets — `docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md`.** The accepted integration direction requires a real application caller behind a Core-owned port and forbids retaining the workspace as a separate product/package boundary. TICK-203 resolves ADR-0025's former MCP/tool-consolidation sub-decision by selecting no renderer MCP/MCPB surface for this activation. SIMPLI-014 owns the corresponding source removal and integrated adapter implementation; this ticket makes no ADR change.
- **Meets — `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md`.** FRD-11 keeps report outcome selection, readiness, approval, issue, and outward dispatch under Core and authorised-human control. Leaving the Automation Actor inventory unchanged prevents arbitrary template/payload/path rendering and prevents Automation from confirming, approving, issuing, or sending a report. This ticket makes no FRD change.
- **Shared EPIC-004 constraint.** The renderer is not a separate MCP host or deployment unit, and `reference/rendererref1/` remains evidence rather than policy. SIMPLI-014 is the single implementation owner for that constraint.

## Steps

1. Confirm that SIMPLI-014's final plan and checklist retain the resolved disposition: no renderer tool is added to `Pegasus.Web/Mcp`, the standalone `CollisionRenderer.Mcp`/MCPB surface is retired, and the only renderer caller is the Core-owned application path. Reuse SIMPLI-014's plan/checklist as the implementation authority rather than restating or editing its code scope.
2. After SIMPLI-014's independently reviewed PR is merged, inspect its exact merged diff and evidence for the TICK-203 acceptance slice: no new renderer MCP inventory/tool/route exists; the standalone MCP/MCPB host and tests are absent from the live product tree/build; and the integrated renderer is reached only through the Core contract. Do not modify the repository from TICK-203.
3. Record a no-code post-implementation report and outcome linking the SIMPLI-014 PR/merge/proof, explicitly stating that TICK-203 was subsumed and introduced no separate branch, worktree, commit, PR, deployment, or Automation capability. Use that evidence to complete TICK-203 through its remaining Kanmer gates.

## Verification

The post-implementation report and eventual proof will cite SIMPLI-014's exact merged PR and commit, then record read-only checks on merged `dev`:

- inspect the SIMPLI-014 merged diff and its completed checklist/proof;
- focused `rg`/architecture-test evidence that no production `CollisionRenderer.Mcp`, MCPB manifest/build, renderer MCP tool, arbitrary template/payload/path operation, or second renderer host remains;
- focused evidence that `Pegasus.Web/Mcp` has no renderer tool addition and the integrated renderer has one Core-owned caller/Infrastructure adapter boundary;
- confirmation that TICK-203 itself has no repository commit, PR, worktree, deployment, or cloud action.

This ticket cannot supply those final merged-code facts before SIMPLI-014 completes. It records only the decision and later verifies the owning implementation; SIMPLI-014 owns all repository changes and their build/test evidence.

## Risks / open questions

- **Active overlap:** Every plausible TICK-203 repository edit is already in SIMPLI-014's claimed source-removal, architecture-test, build, documentation, or composition surface. Mitigation: make no independent repository change and verify the owning merged diff instead.
- **Premature completion:** Research and a plan prove the decision, not that the standalone MCP surface has been retired. Mitigation: keep the checklist's merged-diff verification open until SIMPLI-014 is merged and independently reviewed.
- **Future Automation demand:** A future application-identity/status tool remains explicitly parked and needs its own allocated caller and authorization contract. It is not inferred here.
- **Operator questions:** none remain; the operator direction and accepted architecture already decide the disposition.
