# Open questions — AUTO-001

- [x] Approved 2026-08-18: create the `automation-mcp-client-secret` secret in Key Vault `pegasusprodkv252ow37g`; add its secret reference to Container App `pegasus-prod-web-252ow37gij` in `rg-pegasus-prod`; deploy the resulting revision; and run connector/kill-switch/rollback evidence. The secret and exact-secret Web identity access were created; the live gate was rolled back after startup failure.
- [x] Claude Desktop is the remote MCP client. It holds the OAuth client ID/client secret and controls connector and tool access. Pegasus exposes the already-approved endpoint and tool inventory, validates bearer tokens, records permanent history, and retains the kill switch.
- [x] No new ADR is needed: production activation uses the accepted ADR-0021 boundary and existing Container Apps HTTPS / Key Vault-secret patterns.
- [ ] The deployed image proves it rejects `Features:AutomationMcp=true` in the Production profile. Do you now authorize the necessary source change and a replacement Web image deployment to remove that guard? Recommended: approve, because configuration-only activation has been disproved and safely rolled back.

## Parked (explicitly deferred)

None.
