# Checklist — AUTO-001

- [x] Record approval for the production secret, Container App configuration, deployment revision, and Claude Desktop evidence run.
- [x] Confirm Claude Desktop custom remote connector accepts OAuth client ID/client secret and controls connector/tool access.
- [x] Confirm no new ADR or Pegasus-side tool-permission design is required.
- [ ] Retain safe IaC secret-reference configuration for a future code-compatible deployment; do not set the live gate true from IaC while the deployed image retains its Production guard.
- [x] Create the Key Vault secret and assign the Web identity Key Vault Secrets User on that exact secret.
- [x] Attempt the direct Container App feature setting, observe the failed revision, and roll the gate back to false.
- [x] Read back the healthy rollback revision and closed MCP routes without reading secret material.
- [ ] Obtain approval to change the deployed source guard and rebuild the Web image; without it Claude Desktop evidence cannot run.
- [ ] After a code-compatible deployment, configure Claude Desktop and capture the fifteen-tool success/denial/validation/history evidence, kill switch, and closed-route rollback.
- [ ] Refresh current-state docs, write the post-implementation report, and open the PR.

## Progress notes

- 2026-08-18: Live configuration produced revision `pegasus-prod-web-252ow37gij--0000002`, which exited with `Features:AutomationMcp requires the DevelopmentOffline runtime profile.`
- 2026-08-18: Rollback set `Features__AutomationMcp=false`. Revision `pegasus-prod-web-252ow37gij--0000003` is healthy; live/ready return 200 and MCP metadata/endpoint return the pre-activation 302 closed state.
