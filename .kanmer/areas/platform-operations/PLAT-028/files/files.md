# Files — PLAT-028

## Where the change lands

| Path | Why |
|---|---|
| `src/Pegasus.Web/Pages/Administration/Organizations/` | Redesign list/detail as the consolidated Organization/Principal surface and add thin API-04 credential handlers/views. |
| `src/Pegasus.Web/Pages/Administration/Principals/` | Retain create/replace routes where useful, remove the duplicate index destination, and redirect legacy navigation safely. |
| `src/Pegasus.Web/Pages/Administration/Index.cshtml` and shared navigation | Point Administration to the consolidated surface without adding a second destination. |
| `src/Pegasus.Core/Cases/OrganizationAdministration.cs` | Extend projections only as needed to display API-04 credential status; business lifecycle remains owned by TICK-061. |
| `tests/Pegasus.IntegrationTests/` and browser/accessibility tests | Prove authorization, existing workflows, one-time secret handling, lifecycle controls, responsive layout, keyboard flow, axe, and no horizontal overflow. |
| `docs/frd/frd-04-parties-accounts-and-access.md`, `docs/frd/frd-09-provider-and-intermediary-routes.md`, `docs/design/README.md`, `docs/capabilities.md` | Authorize the narrow credential UI and document the consolidated design. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `src/Pegasus.Web/Pages/Administration/Organizations/Edit.cshtml(.cs)` | Existing combined data, authorization, reason, expected-version and operation-key behavior to preserve. |
| `src/Pegasus.Web/Pages/Administration/Principals/Create.cshtml(.cs)` | Current Principal creation validation and redirect behavior. |
| `src/Pegasus.Web/Pages/Administration/Principals/Replace.cshtml(.cs)` | Immutable Principal replacement flow that redesign must not collapse into editing. |
| `src/Pegasus.Web/Pages/Administration/Automation/Index.cshtml(.cs)` | Existing enable/disable and rotate-once UI convention, while API-04 remains a different owner. |
| `src/Pegasus.Core/Cases/OrganizationAdministration.cs` | Core remains the sole policy owner for Organizations and Principals. |
| `docs/design/README.md` | Page economy, no explanatory copy, destructive consequence, accessibility, and responsive constraints. |
| `docs/frd/frd-04-parties-accounts-and-access.md` | Current access matrix must be deliberately narrowed for provider-key administration. |

## Ripple effects

Depends on TICK-061 backend contracts. Existing bookmarks to Principal routes need deliberate redirect/not-found behavior. Navigation, authorization tests, screenshots, CSS only where existing primitives cannot carry the layout, and current-state docs follow.

## Out of scope

Provider API submission/result endpoints, credential hashing/authentication implementation, multiple credentials, provider self-service, staff/MCP credentials, generic rules, cloud secrets, live issuance, and changes to immutable Principal replacement policy.
