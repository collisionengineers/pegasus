# Post-implementation report — MCP-07 (TICK-104)

## What was delivered

Administration of the Send to AI channel connector, replacing the configuration/user-secrets-only setup with administration-entered values that override it:

- **One bounds owner in Core** — `AiChannelConnectorRules` (loopback http origin, token ≥ 32 characters, timeout 1–60 s); `SendToAiOptions.TryCreate` (composition) and the store/page (administration) both validate against it.
- **Storage on the existing singleton** — four nullable columns on `SendToAiControl` (migration `20260820040337_SendToAiConnectorSettings`; existing grants cover it). `EfAiChannelConnectorStore` follows the switch store's serializable-transaction and attributed-history conventions; the token is DataProtection-protected (purpose `Pegasus.SendToAi.ChannelToken`), write-only after entry, and an unreadable stored token fails closed with a rotate instruction.
- **Per-hand-off effect** — `ChannelAiHandOffTransport` builds its client per call: administration base URL/timeout/token override the composed options; with none set, behaviour is unchanged.
- **Administration UI** — a "Send to AI connector" section on the Automation page: effective values ("From deployment configuration" fallback), base URL/timeout form, token entry/rotation and removal, each with reason + operation key; the token value is never displayed, echoed, or written into history.

The `Features:SendToAi` gate, DevelopmentOffline restriction, and loopback rule are unchanged (AI-09/ADR-0021 decisions). No outbound validation ping was implemented — no caller exists for one.

## Files changed

- `src/Pegasus.Core/AiWork/AiWorkContracts.cs` — rules, settings/runtime records, `IAiChannelConnectorStore`
- `src/Pegasus.Web/AiWork/SendToAi.cs` — options delegate to the rules
- `src/Pegasus.Web/AiWork/ChannelAiHandOffTransport.cs` — per-call effective client; store registration
- `src/Pegasus.Infrastructure/Persistence/AssessmentEntities.cs` / `AssessmentModelConfiguration.cs` / `EfAiWorkRequestStore.cs` — entity, mapping, store
- `src/Pegasus.Infrastructure/Persistence/Migrations/20260820040337_SendToAiConnectorSettings.*` — column-add migration
- `src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj` — `Microsoft.AspNetCore.DataProtection.Abstractions`
- `src/Pegasus.Web/Pages/Administration/Automation/Index.cshtml(.cs)` — connector section + handlers
- `tests/Pegasus.Core.Tests/AiWork/AiWorkTests.cs` — rules bounds
- `tests/Pegasus.IntegrationTests/SendToAiConnectorAdministrationTests.cs` — page flow, override delivery, protection, attribution, fail-closed refusals

## Commands and results

- `dotnet restore ./Pegasus.slnx`; `dotnet build ./Pegasus.slnx -c Release --no-restore`: 0 warnings, 0 errors.
- `dotnet test tests/Pegasus.Core.Tests --filter AiWorkTests`: 31/31 passed.
- `dotnet test tests/Pegasus.IntegrationTests --filter SendToAiIntegrationTests`: 6/6 passed (4 existing regression + 2 new: administration override reaches the overridden channel with the rotated bearer and the composed channel receives nothing; token protected at rest and never in any page or history row; non-administrator and out-of-bounds refusals).
- `pwsh scripts/Test-MigrationGrants.ps1`: 55 migration files checked, pass.

## Residual risks and qualification

- Local evidence only; Send to AI itself remains gated to DevelopmentOffline (ADR-0021), so the connector section is only reachable where that gate is on. Production activation of the transport is AI-09's separate decision; nothing here claims it.
- A DataProtection key-ring change (e.g. ring replacement) makes a stored token unreadable; the failure is visible and the recovery is stated (rotate from Administration).
- "Health/status display" is delivered as configuration state (effective values, token held/rotated date, composed/enabled states already on the page); an operator-triggered live ping is deliberately not implemented (no caller) and recorded as such.
