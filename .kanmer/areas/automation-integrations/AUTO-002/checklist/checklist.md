# Checklist — AUTO-002

- [ ] Options: `AutomationMcp:RedirectUris` parsed and validated; endpoint/lifetime constants.
- [ ] Server: authorization endpoint, auth-code + PKCE, refresh flow, passthrough.
- [ ] Registry: descriptor permissions/redirect URIs when configured; consent history helper.
- [ ] Token endpoint: authorization-code and refresh grants with kill-switch re-check.
- [ ] Consent page `/authorize` (Administrator, ManageAutomationClients): render, approve → code, deny → access_denied, history.
- [ ] Bicep + parameters: `AutomationMcp__RedirectUris` (default claude.ai callback).
- [ ] Tests: round trip, refresh, deny, redirect mismatch, PKCE required, disabled client, history; existing ingress suites green.
- [ ] Docs: ADR-0027 + index; operations MCP section.
- [ ] Build, architecture tests, Local validator, doc links; simplification pass recorded; report; PR to dev.
- [ ] Independent review; merge.
- [ ] Release 10: promotion, provision (new env), smoke; live connector evidence; proof; docs refresh.

## Progress notes
