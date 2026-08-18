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
- [x] Build and publish the immutable replacement Web image; attempt deployment and roll it back when its database readiness check failed.
- [x] Merge `origin/dev` into the branch; Release build 0/0; architecture 96/96; MCP ingress + document + assessment 15/15; `Test-AzureDeploymentPlan -Mode Local` pass; documentation links pass. (claude-code, 2026-08-18)
- [ ] Post-implementation report, PR to `dev`, independent review, merge.
- [ ] Release 9 ([[DELIV-008]]) promotes the merged SHA, applies the two pending migrations and provisions the web revision with `Features__AutomationMcp=true` from this branch's bicep (`AUTOMATION_MCP_CLIENT_SECRET_URI` set in the azd env).
- [ ] Live evidence on the deployed estate: OAuth token for `pegasus-automation`, `/mcp` reachable, tool inventory listed, denial + validation + permanent-history evidence, Administrator kill switch closes the routes and reopening restores them; proof written; docs refreshed.

## Progress notes

- 2026-08-18: Live configuration produced revision `pegasus-prod-web-252ow37gij--0000002`, which exited with `Features:AutomationMcp requires the DevelopmentOffline runtime profile.`
- 2026-08-18: Rollback set `Features__AutomationMcp=false`. Revision `pegasus-prod-web-252ow37gij--0000003` is healthy; live/ready return 200 and MCP metadata/endpoint return the pre-activation 302 closed state.
- 2026-08-18: ADR-0026 superseded the former DevelopmentOffline-only composition rule. The source and IaC change compiled successfully; no test suite was run by operator direction.
- 2026-08-18: Replacement image `sha256:e5d1d01d36039cfb220b941bd442846016baf06a670d95630797a4653ac7d072` failed only on database-schema readiness. No migration was applied. Rollback revision `pegasus-prod-web-252ow37gij--rollbacka593b` is healthy and has the gate false.
- 2026-08-18 (claude-code takes over): the live app currently carries the `AutomationMcp__*` env + `automation-mcp-client-secret` reference with the flag `false` on `--rollbacka593b` (image `aecad247`). Release 9's `azd provision` from the promoted `main` will own that configuration going forward. Merged `origin/dev` (`17696a9c`), kept the architecture snapshot stateless (`db3f57db`).
