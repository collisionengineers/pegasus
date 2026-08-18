# Research — AUTO-001: activate the production Automation MCP gate

## Question

What currently prevents the deployed Automation Actor from reaching Pegasus, and what code, infrastructure, evidence, and approval work is required to enable it safely?

## Findings

- **The production target is known and currently healthy.** Read-only Azure inventory on 2026-08-18 identified subscription `e6076573-23a5-46a8-acef-7e22d264e5db`, resource group `rg-pegasus-prod`, and Container App `pegasus-prod-web-252ow37gij` in UK South. Its latest ready revision is `pegasus-prod-web-252ow37gij--azd-1786687080`; `/health/live` and `/health/ready` both returned 200.
- **The surface is actually absent on the live revision.** The live Container App has `ASPNETCORE_ENVIRONMENT=Production` and `Runtime__Profile=Production`, but no `Features__AutomationMcp` or `AutomationMcp__*` settings. Read-only requests to `/mcp`, `/.well-known/oauth-protected-resource/mcp`, and `/connect/token` redirect to staff sign-in rather than exposing the bearer-only automation surface.
- **The block is code, not merely an omitted setting.** `AutomationMcpOptions.TryCreate` returns no options while the flag is off and throws if the flag is enabled outside `DevelopmentOffline`. `AutomationMcpExtensions` then uses ephemeral OpenIddict signing/encryption keys and disables the transport-security requirement specifically because it is local-only. Production cannot be activated by adding an environment variable alone.
- **The existing implementation supplies the Core boundary and kill switch.** When composed, `Pegasus.Web` registers one client-credentials client, four scopes, a 10-minute token lifetime, and a 120-request/minute limiter; each of the fifteen MCP tools calls existing Core use cases, has permanent history, and the Administrator client switch rejects new and already-issued credentials within the short registration-cache window.
- **Production configuration cannot yet supply the required client secret.** `infra/main.bicep` and `infra/modules/platform.bicep` pass only the current Box/DVLA/DVSA Key Vault references. The Web Container App exposes only two Key Vault-backed secrets. Enabling this gate needs an approved Automation Actor client identifier, a newly created versioned Key Vault secret, a Container App secret reference, and environment values for the feature flag, client ID, secret, and public origin. Secret values must never enter Git, ticket documents, logs, or plan output.
- **The linked implementation work is not a deployment substitute.** [[TICK-027]] is still Preparing and documents local HTTP evidence for the assessment tranche. It does not supply the tier-5 external-client evidence or authorise a live activation.
- **Governing sources explicitly reserve activation.** FRD-10 requires real-caller success, authorization failure, validation failure, and action-history evidence per tool. ADR-0021 and operations state that production certificate/transport decisions, deployment, activation, and external caller proof are separate from local evidence.
- **A current-state discrepancy is already visible.** The live `/diagnostics/version` returned source SHA `aecad2479f52dadfedca109413a458c60c85323e`, while the most recent dated release note in `docs/operations.md` records an older un-numbered deployment. The activation release must refresh `docs/current-architecture.md` and `docs/operations.md` from a fresh readback.

## Implications

This is a production architecture and release change, not a configuration flip. The implementation must preserve default-off behaviour, introduce production-grade token signing/encryption and HTTPS transport requirements, source the client secret only from the existing production Key Vault, and deploy only after the user supplies exact approval for the named target and an external-client evidence run. The ticket should remain in Preparing until that approval and transport/key decision are recorded.

## Clarification — 2026-08-18

The operator confirms the intended boundary: **tool selection/permissioning is owned by the external MCP client, not Pegasus.** The client is Claude Desktop, using OAuth client credentials (client ID and client secret) to obtain a bearer token. Pegasus’s role is to expose the endpoint and its approved tool inventory, authenticate that OAuth client, enforce only protocol/security boundaries, and record permanent history.

The existing per-area scope claims remain a protocol enforcement mechanism in the current implementation; they must not become a second Pegasus-side business approval workflow. The activation does not need a user-selected “minimum scope set.” Claude Desktop’s configured client/tool policy determines what it asks to use; Pegasus must neither add a new inventory nor duplicate that policy.

## Claude Desktop connector compatibility — 2026-08-18

Official Anthropic guidance confirms the intended caller model: a Claude Desktop custom **remote** MCP connector accepts an OAuth client ID and OAuth client secret in Advanced settings. Connector and per-conversation tool controls are configured in Claude, matching the operator clarification. The remote connection is brokered from Anthropic cloud infrastructure, not from the desktop machine; the Pegasus public Container App endpoint is therefore the required reachable HTTPS endpoint.

This validates the existing server's one confidential OAuth client/client-credentials design as the correct activation path. The work is limited to enabling its already-designed production composition, wiring the non-secret settings and the secret reference, and proving the real Claude connector caller. It must not introduce a second permission model, new tool scopes, a new actor model, or an OAuth authorization-code/user-login flow.

Sources: Anthropic Help Center, “Use connectors to extend Claude's capabilities” (updated 2026-08-11), custom connector steps; and “Get started with custom connectors using remote MCP” (updated 2026-08-18), network and OAuth settings.
