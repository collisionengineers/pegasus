# Open questions — AUTO-001

- [x] Approved 2026-08-18: create the `automation-mcp-client-secret` secret in Key Vault `pegasusprodkv252ow37g`; add its secret reference to Container App `pegasus-prod-web-252ow37gij` in `rg-pegasus-prod`; deploy the resulting Web revision; and run the Claude Desktop remote-connector, kill-switch, and rollback evidence. Do not retrieve, display, log, or commit the secret value.
- [x] Claude Desktop is the remote MCP client. It holds the OAuth client ID/client secret and controls connector and tool access. Pegasus exposes the already-approved endpoint and tool inventory, validates bearer tokens, records permanent history, and retains the kill switch.
- [x] No new ADR is needed: production activation uses the accepted ADR-0021 boundary and the existing Container Apps HTTPS / Key Vault-secret patterns; it introduces no new technical architecture boundary.

## Parked (explicitly deferred)

None.
