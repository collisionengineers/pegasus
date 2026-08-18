# Plan — AUTO-001: activate the Claude Desktop Automation MCP connector

## Status

Planned and waiting only for explicit approval of the named production mutations and live evidence run in `open-questions`. No cloud state, credential, or external connector will be changed before that approval.

## Approach

Enable the existing one-client OAuth client-credentials MCP composition in Production, retaining default-off behavior when configuration is incomplete. Claude Desktop’s custom remote connector receives the public MCP URL plus OAuth client ID/secret in its Advanced settings and controls connector/tool use. Pegasus does not add a second tool-permission policy: it authenticates the bearer client, exposes its existing fifteen tools, retains its protocol-level validation, permanent history, rate limit, and Administrator kill switch.

This reuses the existing Web composition, client registry, integration-test harness, Container Apps HTTPS endpoint, and Key Vault-to-Container-App secret-reference pattern. It is preferable to a new client, scope policy, or OAuth user-login flow because those would duplicate the accepted actor boundary.

## Governing docs

- **FRD-10**: meet its required external caller, success, authorization failure, validation failure, and action-history evidence for the approved inventory.
- **ADR-0021**: retain the existing single client, direct-write tool inventory, Core use cases, leases, replay/version guards, and excluded confirmation/approval/dispatch operations. This plan does not modify either governing document or need a new ADR.

## Steps

1. Implement production-capable, fail-closed Automation MCP composition: disabled or incomplete settings expose no MCP/token/metadata route; complete Production configuration composes the existing OAuth client-credentials endpoint over the public HTTPS origin.
2. Reuse and extend `AutomationMcpIngressTests` for valid production-capable configuration, absent/malformed configuration, bearer-only endpoint behavior, existing scope denial, rate limiting, and the Administrator kill switch.
3. Extend the established Bicep and release-validation conventions with one versioned Key Vault secret URI, Container App secret reference, and non-secret `AutomationMcp`/feature/public-origin settings. No secret value is tracked or emitted.
4. Run restore, Release build, focused Automation MCP tests, Bicep compile/lint, and release-plan validation. Perform the simplification pass; retain existing Core/tool/client owners.
5. After exact approval, create the OAuth client secret in `pegasusprodkv252ow37g`, deploy the Web revision to `rg-pegasus-prod/pegasus-prod-web-252ow37gij`, and read back the deployed configuration without reading secret material.
6. Configure Claude Desktop’s custom remote connector with the public `/mcp` URL and OAuth client ID/secret. Capture success, authorization denial, validation failure, permanent-history evidence for all fifteen tools, and the existing Administrator kill switch and closed-route rollback.
7. Refresh `docs/current-architecture.md`, `docs/operations.md`, and `docs/runbook.md` from the observed release; write the post-implementation report and open the PR to `dev`.

## Verification

- `dotnet restore`
- `dotnet build --configuration Release`
- `dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --filter FullyQualifiedName~AutomationMcpIngressTests --configuration Release`
- `az bicep build --file infra/main.bicep`
- Existing release-plan validation and, after explicit approval, a fresh Container App readback plus Claude Desktop remote-connector evidence.

## Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| An incomplete setting exposes a partial endpoint or breaks startup. | Test and preserve fail-closed no-route behavior. |
| Secret leakage. | Pass only a versioned Key Vault URI; never retrieve, log, or track the secret. |
| Duplicate authorization policy. | Claude controls tool access; reuse Pegasus’s existing OAuth/client registry and protocol guards only. |
| Live rollback is unproven. | Evidence the Administrator kill switch and a deployment rollback to the closed route. |
| Current-state docs drift. | Update them only from fresh post-deploy readback. |
