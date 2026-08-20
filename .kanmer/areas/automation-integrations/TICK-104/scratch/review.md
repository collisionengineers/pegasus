## Independent review — PR #446 (orchestrator, 2026-08-20)

Verdict: **pass**, merge on green CI.

- Correct scope decision: AI-09 dispatch exists (`SendCaseToAi`, `ChannelAiHandOffTransport` with round-trip tests), so the connector configuration has a real consumer — no dark config.
- Storage at the right altitude: nullable columns on the existing `SendToAiControl` singleton (no new table, existing grants cover it, `Test-MigrationGrants` green), bounds owned once in Core (`AiChannelConnectorRules`, `SendToAiOptions` delegates — one list per concept).
- Token custody: write-only from entry, protected via the application's DataProtection ring — and I verified `Program.cs` persists that ring to blob storage (`authentication-ring/keys.xml`) with a fixed application name, so protected tokens survive revision changes and deploys. Rotation/removal attributed to permanent history; token never displayed.
- Transport builds its client per hand-off so administration overrides configuration without restart; no outbound validation ping (no caller) and the `Features:SendToAi` gate/loopback rules untouched — the ADR-0021/AI-09 decisions stand.
- Tests: AiWorkTests 31/31, SendToAiIntegrationTests 6/6 including rotated-bearer override delivery and protected-at-rest assertion.
