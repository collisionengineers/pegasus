# Open questions — AUTO-001

- [x] Approved 2026-08-18: create the `automation-mcp-client-secret` secret in Key Vault `pegasusprodkv252ow37g`; add its secret reference to Container App `pegasus-prod-web-252ow37gij` in `rg-pegasus-prod`; deploy the resulting revision; and run connector/kill-switch/rollback evidence. The secret and exact-secret Web identity access were created; the live gate was rolled back after startup failure.
- [x] Claude Desktop is the remote MCP client. It holds the OAuth client ID/client secret and controls connector and tool access. Pegasus exposes the already-approved endpoint and tool inventory, validates bearer tokens, records permanent history, and retains the kill switch.
- [x] No new Pegasus-side tool-permission design is required. ADR-0026 records the explicit Production composition boundary.
- [x] Approved 2026-08-18: change the existing source composition guard and deploy a replacement Web image. The change is limited to allowing the already-defined `Features:AutomationMcp` activation in the production deployment profile; OAuth validation, tool inventory, and Pegasus-side authorization boundaries remain unchanged.
- [x] Resolved 2026-08-18 (claude-code took the ticket over): the out-of-band image `a593bc89…` is **not** redeployed. The database migrations it needs are the same two pending on `dev` (`20260814092852_AddWorkerCaseCreationGrants`, `20260814094632_DropBoxFileRequests`); the operator approved applying them, and the release, as part of [[DELIV-008]] (release 9). This branch is merged onto current `dev`, reviewed and merged, and the promoted `main` SHA is what release 9 builds and provisions — the MCP gate turns on through the bicep in this branch (`Features__AutomationMcp=true`, Key Vault secret reference), not through a manual Container App edit.

## Parked (explicitly deferred)

None.
