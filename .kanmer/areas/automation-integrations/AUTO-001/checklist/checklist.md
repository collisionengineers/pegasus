# Checklist — AUTO-001

- [ ] Record exact approval for the production subscription, resource group, Container App, Key Vault secret creation, deployment, and Claude Desktop remote-connector evidence session.
- [x] Confirm Claude Desktop custom remote connector accepts OAuth client ID/client secret and controls connector/tool access.
- [x] Confirm no new ADR or Pegasus-side tool-permission design is required.
- [ ] Make existing Automation MCP composition Production-capable while preserving default-off/no-route behavior.
- [ ] Add production-capable configuration, bearer-only, failure, rate-limit, and kill-switch regression coverage.
- [ ] Extend Bicep/release validation with the Key Vault secret reference and non-secret MCP settings.
- [ ] Run restore, Release build, focused MCP tests, Bicep compile/lint, and release-plan validation.
- [ ] Complete and record the simplification pass.
- [ ] After exact approval, create the Key Vault secret, deploy the Container App revision, and read back configuration without reading the secret.
- [ ] Configure Claude Desktop and capture the fifteen-tool success/denial/validation/history evidence, kill switch, and closed-route rollback.
- [ ] Refresh current-state docs, write the post-implementation report, and open the PR.

## Progress notes

- 2026-08-18: Read-only Azure inventory and live endpoint checks completed. No cloud state changed.
- 2026-08-18: Claude Desktop custom remote-connector OAuth settings verified from official Anthropic documentation.

- 2026-08-18: Paused before configuration mutation. The exact deployed source revision from `/diagnostics/version` (`aecad2479f52dadfedca109413a458c60c85323e`) has the same explicit `DevelopmentOffline` guard as the worktree; setting `Features__AutomationMcp=true` with `Runtime__Profile=Production` would fail startup. Source/IaC changes drafted during execution were discarded after the operator specified config-only activation. Awaiting reconciliation of that contradiction.
