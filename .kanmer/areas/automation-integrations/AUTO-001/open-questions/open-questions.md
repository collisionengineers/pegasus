# Open questions — AUTO-001

- [x] Approved 2026-08-18: create the `automation-mcp-client-secret` secret in Key Vault `pegasusprodkv252ow37g`; add its secret reference to Container App `pegasus-prod-web-252ow37gij` in `rg-pegasus-prod`; deploy the resulting revision; and run connector/kill-switch/rollback evidence. The secret and exact-secret Web identity access were created; the live gate was rolled back after startup failure.
- [x] Claude Desktop is the remote MCP client. It holds the OAuth client ID/client secret and controls connector and tool access. Pegasus exposes the already-approved endpoint and tool inventory, validates bearer tokens, records permanent history, and retains the kill switch.
- [x] No new Pegasus-side tool-permission design is required. ADR-0026 records the explicit Production composition boundary.
- [x] Approved 2026-08-18: change the existing source composition guard and deploy a replacement Web image. The change is limited to allowing the already-defined `Features:AutomationMcp` activation in the production deployment profile; OAuth validation, tool inventory, and Pegasus-side authorization boundaries remain unchanged.
- [ ] The replacement image requires an Azure SQL migration before it can pass readiness: its database health check reports `The configured database schema is not current.` Do you authorize the exact production database-migration release for source revision `a593bc890cf14b247841c1e878230f919e2e7f94` (including pre-migration validation and the existing immutable migration bundle), then re-deploy its image `sha256:e5d1d01d36039cfb220b941bd442846016baf06a670d95630797a4653ac7d072`? No migration has been applied; the app is rolled back healthy and closed.

## Parked (explicitly deferred)

None.
