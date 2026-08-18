# Open questions — AUTO-001

- [ ] Do you explicitly approve a production activation release for subscription e6076573-23a5-46a8-acef-7e22d264e5db, resource group rg-pegasus-prod, Container App pegasus-prod-web-252ow37gij, and Key Vault pegasusprodkv252ow37g—including creation of one Claude Desktop OAuth client secret, a Container App revision, and live external-client/rollback evidence? Recommended: approve only after the plan's preflight and secret-custody steps are reviewed.
- [x] Claude Desktop is the remote MCP client. It holds the OAuth client ID/client secret and controls connector and tool access. Pegasus exposes the already-approved endpoint and tool inventory, validates bearer tokens, records permanent history, and retains the kill switch.
- [x] No new ADR is needed: production activation uses the accepted ADR-0021 boundary and the existing Container Apps HTTPS / Key Vault-secret patterns; it introduces no new technical architecture boundary.

## Parked (explicitly deferred)

None.
