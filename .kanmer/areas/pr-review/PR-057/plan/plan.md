# Plan — PR-057: Supersede the EVA MCP decision before deleting its tools

## Approach

Create one thin ADR-0031 that supersedes ADR-0021 as the current Automation Actor contract: carry forward the existing actor, direct-write, safeguarding, production-enablement and Send to AI boundaries, but remove `pegasus_eva_bundle_generate` and `pegasus_eva_handoff_status` because staff Export is the sole current EVA-package act. Then mechanically reconcile the ADR index, MCP-06 and present-tense citations. Do not add a replacement tool, compatibility route, flag or abstraction; runtime deletion and inventory coverage already belong to ENG-016.

## Governing docs

- **New ADR — `docs/adr/0031-automation-actor-contract-without-eva-export-tools.md`:** Explicitly authorized by the operator's direction to remove the duplicate EVA automation surface and resolve PR-057. It becomes the current technical owner for the Automation Actor direct-write/tool-inventory and Send to AI transport contract, incorporating ADR-0026's production composition rule and ADR-0027's connector authentication refinement without changing either.
- **Modifies — `docs/adr/0021-automation-actor-direct-write-assessment-contract.md`:** Set only its frontmatter/status to `superseded` and `superseded_by: [ADR-0031]`. Preserve its body as the historical decision.
- **Modifies — `docs/capabilities.md`:** MCP-06 must describe the implemented assessment/case-detail surface without EVA generate/status and cite ADR-0031. Current AI-09/direct-write citations move to ADR-0031; historical statements remain on ADR-0021.
- **Meets — `docs/frd/frd-07-eva-and-external-engineering-handoff.md`:** ADR-0031 points sending to engineering to the already-defined authenticated staff Export; it does not change Export behavior.
- **Modifies citations only — `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md`, `docs/current-architecture.md`, `docs/design/README.md` and `docs/operations.md`:** Present-tense Automation Actor/Send to AI claims cite ADR-0031 and the current 33-tool state; functional and historical meaning stays unchanged.
- **Meets — `docs/frd/frd-10-mcp-automation-and-actor-boundary.md`:** Its general scopes, attribution and guard-parity rules remain unchanged and require no edit.

## Steps

1. Add ADR-0031 with complete frontmatter (`status: accepted`, `supersedes: [ADR-0021]`, relevant MCP/AI capabilities and FRDs), stating the single current architectural change: the Automation Actor inventory has no separate EVA generate/status route; FRD-07 staff Export owns EVA package generation. Carry forward ADR-0021's remaining direct-write/Send to AI boundaries and ADR-0026/0027 refinements without inventing new behavior.
2. Mark ADR-0021 superseded by ADR-0031 and update `docs/adr/README.md` so the current and superseded tables match both ADRs' frontmatter. Link ADR-0031 to PR-057 in Kanmer and clear `docs_todo` once the file exists.
3. Reconcile `docs/capabilities.md`, `docs/current-architecture.md`, `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md`, `docs/design/README.md` and `docs/operations.md`: remove the MCP-06 EVA-tool promise, retain the 33-tool/no-outward-dispatch description, and replace only present-tense ADR-0021 contract citations with ADR-0031. Leave dated/historical ADR-0021 references intact.
4. Update active ADR citations in `AiWorkContracts.cs`, `AssessmentContracts.cs`, `AssessmentMcpTools.cs` and the Assessment Razor page/model comments. Make no executable code or test-inventory changes.
5. Review the diff and searches for accidental behavioral edits or stale current claims, run the documentation validators and focused Automation MCP inventory test, then record every changed file and result in the post-implementation report.

## Verification

Run from the ticket worktree:

```powershell
pwsh ./scripts/Test-MarkdownPlacement.ps1
pwsh ./scripts/Test-DocumentationLinks.ps1
rg -n "pegasus_eva_bundle_generate|pegasus_eva_handoff_status|ADR-0021" docs src
$env:MSBUILDDISABLENODEREUSE = '1'
dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --disable-build-servers --filter "FullyQualifiedName~AutomationMcpIngressTests.IngressIsBearerOnlyWithDiscoveryAndTheApprovedToolInventory"
```

Manual readback must also confirm:

- ADR-0021 and ADR-0031 frontmatter/index entries agree in both directions;
- every remaining ADR-0021 match is deliberately historical or an ADR cross-reference;
- MCP-06 and current architecture describe the same 33-tool surface and neither promises the deleted EVA tools;
- the focused runtime inventory test remains green.

`kanmer-verify` will repeat the relevant checks on the merged branch and write `proof.md`.

## Risks / open questions

- **Historical over-rewrite:** a blanket replacement would falsify decision history. Mitigation: classify each ADR-0021 match and update only present-tense ownership claims.
- **ADR sprawl:** ADR-0031 must replace one affected contract, not redesign Automation MCP or future engineering routes. Keep the decision thin and point behavior to FRD-07/10/11.
- **Count drift:** 33 is an as-built fact for current-state docs/tests, not a permanent extensibility promise in the ADR.
- No open questions. The operator has explicitly authorized convergence on the one-Export pre-release target state.
