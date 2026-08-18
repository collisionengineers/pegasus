# Open questions — AUTO-001

- [ ] Do you explicitly approve a production activation release for subscription `e6076573-23a5-46a8-acef-7e22d264e5db`, resource group `rg-pegasus-prod`, Container App `pegasus-prod-web-252ow37gij`, and Key Vault `pegasusprodkv252ow37g`—including creation of one Claude Desktop OAuth client secret, a Container App revision, and live external-client/rollback evidence? Recommended: approve only after the plan's preflight and secret-custody steps are reviewed.
- [x] Claude Desktop is the sole external MCP client. It holds the OAuth client ID/client secret and controls the actor's tool-use policy. Pegasus exposes its approved endpoint and existing tool inventory; it does not run a second tool-permission policy.
- [ ] Do you approve a durable production token-key/HTTPS transport decision (new ADR) before code changes? Recommended: yes—use managed, rotatable production signing/encryption keys and enforce HTTPS, while retaining ephemeral keys only for DevelopmentOffline evidence runs.

## Parked (explicitly deferred)

None.
