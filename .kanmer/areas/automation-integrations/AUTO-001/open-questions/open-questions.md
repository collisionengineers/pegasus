# Open questions — AUTO-001

- [ ] Do you explicitly approve a production activation release for subscription `e6076573-23a5-46a8-acef-7e22d264e5db`, resource group `rg-pegasus-prod`, Container App `pegasus-prod-web-252ow37gij`, and Key Vault `pegasusprodkv252ow37g`—including creation of one new Automation Actor client secret, a Container App revision, and live external-client/rollback evidence? Recommended: approve only after the plan's ADR, preflight, and secret-custody steps are reviewed.
- [ ] Which named external Automation Actor will hold the client credential and run the live evidence session, and which of the four existing scopes does it require? Recommended: use the single vendor-neutral actor with the minimum documented scope set; do not grant all four by default.
- [ ] Do you approve a durable production token-key/HTTPS transport decision (new ADR) before code changes? Recommended: yes—use managed, rotatable production signing/encryption keys and enforce HTTPS, while retaining ephemeral keys only for DevelopmentOffline evidence runs.

## Parked (explicitly deferred)

None.
