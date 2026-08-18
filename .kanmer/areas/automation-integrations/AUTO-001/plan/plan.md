# Plan — AUTO-001: enable the Automation MCP in production

## Approach

Enable the existing Automation MCP Web composition in Production. The initial configuration-only attempt showed that the deployed image rejected the feature; ADR-0026 therefore supersedes the DevelopmentOffline-only composition decision. The source change removes only that profile restriction. Existing OAuth client validation, Core use cases, scopes, rate limit, permanent history, and Administrator kill switch remain unchanged.

The external MCP client selects its connector and tool-access policy. Pegasus provides the existing HTTPS `/mcp` and `/connect/token` surfaces and continues its existing authentication and safety boundary; it does not introduce a new Pegasus-side tool-permission design.

## Governing docs

- **FRD-10**: preserves the Automation Actor boundary, ordinary Core actions, and required real-caller evidence.
- **ADR-0026**: permits explicit Production configuration of the existing composition gate while retaining its fail-closed default and safeguards.
- **ADR-0021**: superseded by ADR-0026 for the former DevelopmentOffline-only activation rule; its direct-write inventory remains unchanged.

## Steps

1. Add ADR-0026 and update the ADR index/ADR-0021 status to record the approved Production composition boundary.
2. Remove the DevelopmentOffline-only check from `AutomationMcpOptions.TryCreate`; retain every validation of the feature flag, client ID, client secret, public origin, and registration-cache lifetime.
3. Add the versioned Automation MCP secret URI, Key Vault secret reference, and non-secret Container App settings to Bicep so a future infrastructure deployment preserves the enabled state.
4. Build the replacement Web release artifact, push its immutable Linux/AMD64 OCI image to the existing production ACR, and update only `pegasus-prod-web-252ow37gij` to the new digest. No test suite is run at the operator's direction.
5. Confirm the new revision is healthy, read back only secret-reference/configuration names, and exercise the existing OAuth/MCP routes. Configure Claude Desktop with its OAuth client ID/secret and record the requested real-caller evidence.
6. Refresh current-state documents, complete the report, and open the PR.

## Verification

- `dotnet build --configuration Release --no-restore` and Bicep compilation succeed. No .NET tests are run by direction.
- Exact new Web image digest and source SHA are read back from the existing production target.
- `/health/live` and `/health/ready` succeed; OAuth metadata, token-denial/success, MCP authorization denial, and configured-client access are observed without retrieving the secret value.
- The existing Administrator kill switch returns the ingress to its closed state and rollback restores the prior healthy revision if needed.

## Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| Missing or malformed Automation MCP settings | Retain existing startup validation and roll back immediately if the new revision is unhealthy. |
| Future infrastructure deployment removes activation | Declare the exact Key Vault reference and settings in the existing Bicep Container App resource. |
| Secret exposure | Keep the generated value in Key Vault only; read back names/references rather than values. |
| Incorrect external-client access | Exercise only the existing OAuth/client-credentials boundary and record success/denial/history evidence. |

## Take-over amendment — 2026-08-18 (claude-code)

Steps 4–5 as originally written (push an out-of-band image built off `dev@5ab3b773`
and update only the Container App) are replaced: `main` is the sole revision
eligible for an authorised release (`docs/engineering.md`), so this branch is
merged onto current `dev`, reviewed, merged, and shipped by [[DELIV-008]]
(release 9) — promotion, migrations, `azd provision` with this branch's bicep
(`Features__AutomationMcp=true`, Key Vault secret reference), Worker package,
smoke. Live MCP evidence (token, `/mcp`, inventory, denial/validation/history,
kill switch, closed-route rollback) is then captured against that release.
The already-published image `pegasus/web:a593bc89…` is not deployed.

## Simplification pass — 2026-08-18

Lenses over the branch diff (ADR-0026, ADR index/0021 status, bicep parameter +
secret + four settings, `AutomationMcpOptions.TryCreate` signature, two
comments, operations note):
- Reuse — the secret follows the existing `box-client-secret` Key Vault
  reference pattern; no new mechanism. Applied as-is.
- Simplification — `TryCreate` lost its now-meaningless `developmentOfflineProfile`
  parameter and its only caller was updated. Nothing else to remove.
- Efficiency — n/a.
- Altitude — one finding: `docs/current-architecture.md` had gained a dated
  event narrative (belongs to operations); replaced with a stateless pointer
  (`db3f57db`). ADR-0021 is marked `superseded` wholesale while ADR-0026 carries
  its inventory forward by reference — acceptable under the append-only ADR
  rule; left as the author wrote it.
