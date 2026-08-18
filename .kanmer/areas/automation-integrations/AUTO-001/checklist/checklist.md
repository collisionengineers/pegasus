# Checklist — AUTO-001

- [ ] Record exact approval for the production subscription, resource group, Container App, Key Vault secret creation, deployment, and live evidence session.
- [ ] Record the named external Automation Actor and its minimum approved scope set.
- [ ] Record approval for the production token-key/HTTPS transport ADR.
- [ ] Create and link the accepted production Automation MCP security/transport ADR.
- [ ] Make Automation MCP composition production-capable while retaining default-off and DevelopmentOffline-only ephemeral-key behavior.
- [ ] Add focused configuration, bearer transport, and fail-closed regression coverage.
- [ ] Extend Bicep/release validation for the versioned Key Vault secret reference and non-secret MCP settings.
- [ ] Run restore, Release build, focused Automation MCP tests, Bicep compile/lint, and release-plan validation.
- [ ] Complete the simplification pass and record findings/dispositions in this plan.
- [ ] After exact approval, create the approved Key Vault secret, deploy the named Container App revision, and read back configuration without retrieving a secret.
- [ ] Run the named external actor’s fifteen-tool success/denial/validation/action-history evidence, then prove the kill switch and closed-route rollback.
- [ ] Refresh current-architecture, operations, and runbook from observed deployment facts; write the post-implementation report and open the PR.

## Progress notes

- 2026-08-18: Ticket taken into Preparing; read-only Azure inventory and live endpoint checks completed. No cloud state changed.
