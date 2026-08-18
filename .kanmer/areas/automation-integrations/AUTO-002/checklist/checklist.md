# Checklist — AUTO-002

- [x] Options: `AutomationMcp:RedirectUris` parsed and validated; endpoint/lifetime constants.
- [x] Server: authorization endpoint, auth-code + PKCE, refresh flow, resource registration, passthrough.
- [x] Registry: descriptor permissions/redirect URIs when configured; consent history helper.
- [x] Token endpoint: authorization-code and refresh grants with kill-switch re-check.
- [x] Consent page `/authorize` (Administrator, ManageAutomationClients): render, approve → code, deny → access_denied, history.
- [x] Bicep + parameters: `AutomationMcp__RedirectUris` (default claude.ai callback).
- [x] Tests: round trip, refresh, deny, redirect mismatch, PKCE required, disabled client, history; existing ingress suites green (19/19).
- [x] Docs: ADR-0027 + index; operations MCP section.
- [x] Build 0/0, architecture 96/96, Local validator, doc links; simplification pass recorded; report; PR #405 to dev.
- [ ] Independent review; merge.
- [ ] Release 10: promotion, provision (new env), smoke; live connector evidence; proof; docs refresh.

## Progress notes

- 2026-08-18: Implemented on `task/auto-002-connector-auth-code` (`17545b6f`); PR https://github.com/collisionengineers/pegasus/pull/405. Two OpenIddict-7 facts learned: the client registration must exist before OpenIddict validates `/authorize` (seed in the automation middleware), and the `resource` indicator MCP clients send must be registered (`RegisterResources`) and permitted per client.
