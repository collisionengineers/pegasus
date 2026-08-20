# Plan — MCP-07

## Chosen approach

Extend the existing Send-to-AI owners; add nothing new-shaped. The `SendToAiControl` singleton row (already administrator-written, already granted) gains the connector fields; `EfSendToAiControlStore` gains the settings store using its existing serializable-transaction + attributed-`ActionHistory` conventions; the Administration/Automation page gains a connector section beside the existing switches; `ChannelAiHandOffTransport` resolves effective settings per call so an administration change takes effect without restart. Configuration/user-secrets remain the seed and fallback — administration values override when set, which is the honest reading of "replacing the configuration-only setup" without breaking the composed gate.

Rejected: a new secrets table/page family (second store for one concept); Key Vault writes from the app (cloud write, approval-gated, no caller); making `Features:SendToAi` or the loopback/DevelopmentOffline constraints administrable (ADR-0021/AI-09 decisions, separately owned).

## Secret-custody and rotation contract (the direct decision capabilities.md asks for)

- The token is write-only from Administration: entered, validated (≥ 32 characters, same bound as composition), protected with ASP.NET DataProtection (purpose `Pegasus.SendToAi.ChannelToken`) and stored in the singleton row. It is never displayed, echoed, logged, or exportable; the UI shows only whether an administration-entered token is held and when it was last rotated.
- Rotation = entering a new token over the old one, with reason + operation key; every update writes attributed permanent history (`send_to_ai_connector_updated` / `send_to_ai_channel_token_rotated`) recording *that* the value changed, never the value.
- Clearing the administration token returns the transport to the composition-configured token.

## Ordered steps (each names what it reuses)

1. **Core** (`AiWorkContracts.cs`): `AiChannelConnectorSettings` (base URL, timeout seconds, token-held flag, rotated-at, version) + `IAiChannelConnectorStore` (get/update) beside `ISendToAiControl`; static `AiChannelConnectorRules` owning the loopback-origin, token-length and timeout bounds now duplicated implicitly in `SendToAiOptions.TryCreate`. Update command validates via `StaffAuthorization.Require(actor, ManageAutomationClients)` — the same right as the existing switches.
2. **Web options** (`SendToAi.cs`): `TryCreate` delegates bounds to `AiChannelConnectorRules` — one validation list.
3. **Infrastructure**: nullable columns on `SendToAiControlEntity` (`ChannelBaseUrl`, `TimeoutSeconds`, `ChannelTokenProtected`, `TokenRotatedAtUtc`); mapping; add-columns migration (no new table → existing `SendToAiControl` grants suffice; census untouched); `EfSendToAiControlStore` implements the port with its existing transaction/history pattern, protecting the token via `IDataProtectionProvider` (new `DataProtection.Abstractions` package reference).
4. **Transport** (`ChannelAiHandOffTransport`): resolve effective settings per call — administration row values override the composed `SendToAiOptions`; base address/token/timeout set on the per-call client instance from `IHttpClientFactory` (unchanged client name, redirects still disabled).
5. **Administration UI** (`Automation/Index`): a "Send to AI connector" section following the page's existing form conventions (reason, operation key, status chips, one-sentence consequence guidance, no identifiers): effective base URL and timeout, whether an administration token is held and when rotated, forms to update base URL/timeout and to enter/rotate/clear the token.
6. **Tests**: Core rules tests beside `AiWorkTests`; integration: administration update + token rotation writes attributed history and never round-trips the token to the page; the transport uses administration values (extend `SendToAiIntegrationTests`' fake channel to assert the overridden bearer/base URL); migration applies.
7. Locked restore/Release build, focused tests, simplification pass with dispositions here.

## Acceptance

- An Administrator can set base URL, timeout, and enter/rotate the token from Administration within the same bounds composition enforces; changes take effect on the next hand-off without restart; every change is attributed history with reason; the token value never appears in any page, log, or history row.
- With no administration values, behaviour is exactly today's configuration-driven behaviour (regression: existing `SendToAiIntegrationTests` stay green).

## Dependencies and sequencing

AI-09 code is on `dev` (verified in research); no ticket blocks this work. Live/production activation of Send-to-AI itself remains AI-09's separate transport decision — this ticket claims local evidence only.

## Simplification pass — 2026-08-20 (reuse, simplification, efficiency, altitude)

- Reuse: connector fields live on the existing `SendToAiControl` singleton (no new table, existing grants); `EfAiChannelConnectorStore` follows the switch store's transaction/history conventions; the admin section follows the page's existing form pattern; integration facts are a second partial of `SendToAiIntegrationTests` reusing its fake channel and form helpers. Applied.
- Simplification: the three page handlers repeat the page's existing preamble (actor, store, operation key) rather than extracting a helper — the existing convention on this page wins; extraction not applied, reason recorded.
- Efficiency: one extra singleton-row read per hand-off (runtime settings), negligible beside the outbound HTTP call. No finding.
- Altitude: bounds live once in Core (`AiChannelConnectorRules`) and are consumed by composition options, the page, and the store; the HTML `minlength` hint was switched to the Core constant so no literal copy remains. Applied.
- Not applied (named): a connector "health ping" — deliberately out of scope; no caller exists for an outbound validation call and none is invented.

## Implementation notes — 2026-08-20

- Migration `20260820040337_SendToAiConnectorSettings`: four nullable columns on `SendToAiControl`; no new table, so the existing object-level grants cover it — `scripts/Test-MigrationGrants.ps1` passes (55 files checked).
- `Features:SendToAi` still composes the whole surface (DevelopmentOffline only, ADR-0021); with the gate off the connector section states there is nothing to configure, and with no administration values set behaviour is byte-for-byte the configuration-driven behaviour (existing suite green).
