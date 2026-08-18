# Checklist — AUTO-001

- [x] Record approval for the production secret, Container App configuration, deployment revision, and Claude Desktop evidence run.
- [x] Confirm Claude Desktop custom remote connector accepts OAuth client ID/client secret and controls connector/tool access.
- [x] Confirm no new Pegasus-side tool-permission design is required.
- [x] Record ADR-0026 to permit the explicitly configured Production composition gate.
- [x] Create the Key Vault secret and assign the Web identity Key Vault Secrets User on that exact secret.
- [x] Attempt the direct Container App feature setting, observe the failed revision, and roll the gate back to false.
- [x] Read back the healthy rollback revision and closed MCP routes without reading secret material.
- [x] Remove only the deployed source guard and add the matching IaC configuration.
- [x] Compile the solution and Bicep template; no test suite run by operator direction.
- [ ] Build, publish, and deploy the replacement Web image to the approved Container App.
- [ ] Configure Claude Desktop and capture the fifteen-tool success/denial/validation/history evidence, kill switch, and closed-route rollback.
- [ ] Refresh current-state docs, write the post-implementation report, and open the PR.

## Progress notes

- 2026-08-18: Live configuration produced revision `pegasus-prod-web-252ow37gij--0000002`, which exited with `Features:AutomationMcp requires the DevelopmentOffline runtime profile.`
- 2026-08-18: Rollback set `Features__AutomationMcp=false`. Revision `pegasus-prod-web-252ow37gij--0000003` is healthy; live/ready return 200 and MCP metadata/endpoint return the pre-activation 302 closed state.
- 2026-08-18: ADR-0026 superseded the former DevelopmentOffline-only composition rule. The source and IaC change compiled successfully; no test suite was run by operator direction.
