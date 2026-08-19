# Plan — TICK-214: Decide the long-term MCPB host and distribution boundary

## Approach

Treat TICK-214 as a decision-only prerequisite already subsumed by [[SIMPLI-014]], not as an independent host/distribution implementation. The operator and EPIC-004 binding direction resolve ADR-0025's former conditional: no renderer MCPB channel survives. The workspace stdio host, manifest, bundle build, browser bootstrap/install, local output descriptors, and MCP-specific tests are retired as SIMPLI-014 migrates only caller-backed engine mechanics into Infrastructure. Pegasus's existing authenticated Automation MCP inventory remains unchanged, and accepted-assessment generation uses the single Core-owned application path. SIMPLI-014 actively owns every overlapping source, test, solution, workflow, and architecture assertion; TICK-214 owns only post-merge acceptance and traceability.

No new ADR is required. ADR-0025 already selects integration into the application rather than a standalone product/package boundary and explicitly made MCPB survival conditional. Resolving that condition to “none” introduces no new project, runtime, deployment unit, protocol boundary, or distribution mechanism; it removes the optional standalone mechanism and conforms to the existing four-project architecture.

## Governing docs

- **Meets — `docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md`.** The renderer is integrated behind a Core port and no standalone package/product boundary survives. The ADR's conditional MCPB possibility is not activated; no new technical boundary is created and no ADR modification is needed.
- **Meets — `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md`.** Report readiness, generation, approval, issue, and dispatch remain Core/application behaviour. Retiring arbitrary local template/payload/path rendering prevents a second path from bypassing accepted case identity, custody, finality, and human approval. No FRD modification is needed from this ticket.
- **Shared EPIC-004 constraint.** The renderer is explicitly not an MCP host, API, package, repository, service, or independent deployment. SIMPLI-014 is the sole implementation owner for removing the obsolete workspace surfaces.

## Steps

1. Confirm that SIMPLI-014's final plan/checklist retains the TICK-214 disposition: retire CollisionRenderer.Mcp, its MCPB manifest/build/browser/output surfaces and host-only tests; add no renderer MCP tool or route to `Pegasus.Web`; and expose rendering only through the Core-owned application use case. Reuse SIMPLI-014 as the sole implementation authority.
2. After SIMPLI-014's independently reviewed PR is merged, inspect its exact merged diff and evidence for this acceptance slice: no MCPB manifest/build artifact, stdio renderer host, local renderer output distribution, browser-install tool, MCP-only dependency/test project, or second renderer caller remains in the live source/build tree; reusable engine cases, if retained, test the Infrastructure adapter rather than MCP transport.
3. Verify on merged `dev` that `Pegasus.Web/Mcp` has no renderer inventory/tool/route addition and architecture/build checks enforce one Core caller plus one Infrastructure implementation without a standalone host/distribution project. Confirm documentation does not claim a renderer MCPB is supported or deployed.
4. Record a no-code post-implementation report and outcome linking the SIMPLI-014 PR, merge commit, focused checks, and proof. State that TICK-214 was subsumed and created no repository branch, worktree, commit, PR, deployment, distribution artifact, or cloud action; then complete its remaining Kanmer gates from that evidence.

## Verification

The post-implementation report and eventual proof will cite SIMPLI-014's exact merged PR/commit and record read-only checks on merged `dev`:

- focused `rg`/file/build checks proving no `CollisionRenderer.Mcp`, MCPB manifest/build script, stdio host, browser bootstrap/install, local output access, or MCP-only project/dependency remains;
- focused MCP inventory and route checks proving no renderer tool was added to `Pegasus.Web/Mcp`;
- architecture-test evidence proving no standalone renderer host/project/deployment boundary and only the Core → Infrastructure application path;
- inspection of retained tests to ensure useful engine behavior was migrated without preserving MCP transport/distribution semantics;
- confirmation that TICK-214 itself has no repository commit, PR, worktree, deployment, bundle, or cloud action.

The final retirement cannot be proved until SIMPLI-014's workspace migration is merged. TICK-214 owns the decision and acceptance slice only; SIMPLI-014 owns all removal, migration, build, and test changes.

## Risks / open questions

- **Active overlap:** every surveyed TICK-214 file is inside SIMPLI-014's claimed workspace removal, MCP retirement, build, test, or architecture surface. Mitigation: no independent worktree or diff.
- **Hidden useful engine logic:** MCP mappings may contain reusable validation or fixture logic. Mitigation: SIMPLI-014 inventories it and migrates only caller-backed engine tests behind the application adapter, without retaining transport or local-output contracts.
- **False retirement:** deleting the manifest while leaving a build reference, dependency, route, or documented distribution claim would preserve part of the boundary. Mitigation: verify source, solution, package locks, workflows, MCP inventory, architecture tests, and current-state docs together.
- **Future Automation status demand:** it requires a separately allocated caller-backed Core contract and returns Pegasus identities/status, never local artifacts. It remains parked.
- **Operator questions:** none remain; the binding direction explicitly excludes a renderer MCP host/distribution.
