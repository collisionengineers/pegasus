# Checklist — AUTO-001

- [x] Record approval for the production secret, Container App configuration, deployment revision, and Claude Desktop evidence run.
- [x] Confirm Claude Desktop custom remote connector accepts OAuth client ID/client secret and controls connector/tool access.
- [x] Confirm no new ADR or Pegasus-side tool-permission design is required.
- [ ] Retain the IaC secret-reference and non-secret gate settings for the next deployment.
- [ ] Create the Key Vault secret and assign the Web identity Key Vault Secrets User on that exact secret.
- [ ] Apply the Automation MCP secret reference and environment settings directly to the existing Container App without rebuilding its image.
- [ ] Read back the revision health and configuration references without reading secret material.
- [ ] Configure Claude Desktop and capture the existing fifteen-tool success/denial/validation/history evidence, kill switch, and closed-route rollback.
- [ ] Refresh current-state docs, write the post-implementation report, and open the PR.

## Progress notes

- 2026-08-18: Read-only Azure inventory and live endpoint checks completed.
- 2026-08-18: Claude Desktop custom remote-connector OAuth settings verified from official Anthropic documentation.
- 2026-08-18: Source/test changes discarded at operator direction. Activation is configuration-only; no .NET test or rebuild is required.
- 2026-08-18: Read-only RBAC census found the Web identity has secret-level Key Vault Secrets User access only to the two Box secrets. The new Automation MCP secret needs one matching exact-secret assignment.

- 2026-08-18: Approved live configuration attempt stopped before any mutation. The current Azure identity is forbidden from `Microsoft.KeyVault/vaults/secrets/setSecret/action` on `pegasusprodkv252ow37g`; no Automation MCP secret, role assignment, Container App setting, or revision was created.
