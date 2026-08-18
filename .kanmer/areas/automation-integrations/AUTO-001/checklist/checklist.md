# Checklist — AUTO-001

- [ ] Record exact approval for the production subscription, resource group, Container App, Key Vault secret creation, deployment, and Claude Desktop remote-connector evidence session.
- [x] Confirm Claude Desktop custom remote connector accepts the OAuth client ID/client secret and controls connector/tool access.
- [x] Confirm no new ADR or Pegasus-side tool-permission design is required.
- [ ] Make the existing Automation MCP composition production-capable while retaining default-off behavior and secure public HTTPS bearer transport.
- [ ] Add focused configuration, bearer transport, and fail-closed regression coverage.
- [ ] Extend Bicep/release validation for the versioned Key Vault secret reference and non-secret MCP settings.
- [ ] Run restore, Release build, focused Automation MCP tests, Bicep compile/lint, and release-plan validation.
- [ ] Complete the simplification pass and record findings/dispositions in this plan.
- [ ] After exact approval, create the approved Key Vault secret, deploy the named Container App revision, and read back configuration without retrieving a secret.
- [ ] Configure the Claude Desktop custom remote connector with the public MCP endpoint URL and OAuth client ID/secret; capture all fifteen-tool success/denial/validation/action-history evidence, then prove the kill switch and closed-route rollback.
- [ ] Refresh current-architecture, operations, and runbook from observed deployment facts; write the post-implementation report and open the PR.

## Progress notes

- 2026-08-18: Ticket taken into Preparing; read-only Azure inventory and live endpoint checks completed. No cloud state changed.
- 2026-08-18: Official Anthropic documentation verified the Claude Desktop custom remote-connector model and OAuth client-ID/secret advanced settings.
