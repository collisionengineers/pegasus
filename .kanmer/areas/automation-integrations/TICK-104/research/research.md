# Research — MCP-07

## Question

How does the Send-to-AI channel connector get its base URL, token and timeout today, and what is the honest minimal path to administer them from Administration?

## Verified findings (read-only checks on `dev`)

- **The dispatch caller is real.** AI-09 is implemented: `src/Pegasus.Core/AiWork/AiWorkOperations.cs` (`SendCaseToAi`), the one outbound adapter `src/Pegasus.Web/AiWork/ChannelAiHandOffTransport.cs`, and `tests/Pegasus.IntegrationTests/SendToAiIntegrationTests.cs` prove the round trip. Connector configuration therefore has a concrete consumer; the ticket is not config-for-nothing.
- **Configuration today** (`src/Pegasus.Web/AiWork/SendToAi.cs`): `SendToAiOptions.TryCreate` reads `SendToAi:ChannelBaseUrl` / `ChannelToken` / `TimeoutSeconds` from configuration/user-secrets at composition, behind `Features:SendToAi`, which **fails closed outside the DevelopmentOffline runtime profile** (research-preview transport, ADR-0021). Validation: loopback http origin without path/query; token ≥ 32 characters; timeout 1–60 s. The named `HttpClient` is configured once at startup.
- **A DB-backed administrator setting already exists for this exact boundary**: `SendToAiControl` singleton row (`EfSendToAiControlStore` in `src/Pegasus.Infrastructure/Persistence/EfAiWorkRequestStore.cs`, entity in `AssessmentEntities.cs`), administered from `Pages/Administration/Automation/Index` with reason + operation key + attributed `ActionHistory`. The `SendToAiControl` table already carries Web `SELECT/INSERT/UPDATE` grants (migration `20260803205759_SendToAiAssessmentToolset`), so added columns need no new grant or census entry.
- **Secret handling conventions**: there is no runtime secret store; secrets live in configuration/Key Vault at composition. The application has ASP.NET DataProtection configured (`Program.cs:169`, blob-persisted key ring in production; framework-default locally). No existing Infrastructure class uses `IDataProtectionProvider`; using it for the stored token requires the `Microsoft.AspNetCore.DataProtection.Abstractions` package in `Pegasus.Infrastructure`.
- **Capabilities boundary** (`docs/capabilities.md` MCP-07): conditionally allocated behind a decision on exact fields and a secret-custody/rotation contract; the fields are named by the capability itself (base URL, token entry/rotation, timeout, health/status display). Operator instruction 2026-08-20 places all MCP capability tickets in active implementation scope.

## Assumed (not separately verified)

- The Worker never resolves the connector settings port (the send path is Web-only); registering an Infrastructure implementation that depends on `IDataProtectionProvider` is safe because resolution is lazy and Worker does not enable DI validate-on-build.

## Implication

Extend the existing owners — the `SendToAiControl` singleton row, `EfSendToAiControlStore`'s conventions, and the Administration/Automation page — rather than adding any new store, page family, or abstraction. Administration values override configuration when present; the composition gate, loopback restriction, and DevelopmentOffline constraint are deliberately unchanged (they are AI-09/ADR-0021 decisions, not this ticket's).
