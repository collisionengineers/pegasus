# Open questions — MCP-07

- [x] **May MCP-07 be implemented despite its "Later / post-alpha, conditionally allocated" designation?** — Resolved by the operator on 2026-08-20: all MCP capability tickets are in active implementation scope, post-alpha labels notwithstanding. The exact fields are the ones the capability names (base URL, token entry/rotation, timeout, status display), and the secret-custody/rotation contract is recorded in this ticket's plan: write-only DataProtection-protected token in the existing `SendToAiControl` singleton, never displayed or logged, rotation and every change as attributed permanent history.
- [x] **Does a real dispatch caller exist for the connector configuration?** — Resolved by read-only check on `dev` (2026-08-20): AI-09's `SendCaseToAi` and `ChannelAiHandOffTransport` are implemented with round-trip integration evidence, so the configuration has a concrete consumer. The `Features:SendToAi` gate, loopback restriction and DevelopmentOffline constraint remain AI-09/ADR-0021 decisions and are not changed here; no outbound validation ping is implemented because no caller for one exists.

## Parked (explicitly deferred)
