# Files — TICK-061

## Where the change lands

| Path | Why |
|---|---|
| `src/Pegasus.Core/Cases/` | Add Principal-owned credential status, commands, validation, authorization, lifecycle semantics, and ports beside existing Principal administration. |
| `src/Pegasus.Core/Identity/` | Add only the provider-client actor/authentication contract needed by Web; keep staff authorization separate. |
| `src/Pegasus.Infrastructure/Persistence/` | Add the credential entity, EF mapping/migration, one-way secret verification, concurrency, operation replay, and permanent history. |
| `src/Pegasus.Web/Pages/Administration/Organizations/` or the PLAT-028 successor surface | Present credential status and generate/reset/revoke/pause/resume controls with one-time secret display. |
| `src/Pegasus.Web/Authentication/`, `src/Pegasus.Web/Program.cs` | Compose the provider authentication scheme used by API-01/API-03 without accepting staff cookies. |
| `tests/Pegasus.Core.Tests/`, `tests/Pegasus.IntegrationTests/`, `tests/Pegasus.ArchitectureTests/` | Prove policy, hashing, one-time display, concurrency/replay, audit history, authorization, authentication, and composition. |
| `docs/frd/frd-09-provider-and-intermediary-routes.md`, superseding ADR, `docs/design/README.md`, `docs/capabilities.md`, `docs/current-architecture.md` | Authorize the administration surface and simplified API contract; supersede ADR-0004 rather than rewriting its accepted decision. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `src/Pegasus.Core/Cases/OrganizationAdministration.cs` | Existing Principal/Organization authorization, validation, versioning, and mutation ownership to extend rather than duplicate. |
| `src/Pegasus.Infrastructure/Persistence/EfOrganizationAdministration.cs` | Transaction, operation-key, and permanent history conventions. |
| `src/Pegasus.Web/Pages/Administration/Organizations/Edit.cshtml.cs` | Administrator-only handlers, expected version, reason, and operation-key precedent. |
| `src/Pegasus.Web/Pages/Administration/Automation/Index.cshtml.cs` | Enable/disable and rotate-once UI precedent; reuse the convention, not the Automation owner. |
| `docs/design/README.md` | No explanatory copy, no provider staff shell, and current prohibition that the authorized docs change must narrow. |
| `docs/adr/0004-provider-api-and-staff-mcp-authentication.md` | Existing security decision that must be superseded for the changed administration/status contract. |

## Ripple effects

API-04 blocks activation of API-01 and API-03. It adds a database migration and runtime authentication reads. PLAT-028 owns visual redesign and browser acceptance; API-04 owns backend capability and may coordinate the UI slice through the same PR only if the final ticket plan remains one reviewable unit.

## Out of scope

Multiple concurrent credentials, OAuth for providers, staff/MCP authentication changes, provider self-service, cloud secret stores, live provider issuance, general route configuration, and permanent deletion of Principals.
