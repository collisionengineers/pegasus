# Files — TICK-058

## Where the change lands

| Path | Why |
|---|---|
| `src/Pegasus.Core/Intake/` | Add only the provider-facing request translation contract if the existing grouped submission request cannot be used directly; preserve ReceiveIntake as the sole intake owner. |
| `src/Pegasus.Web/` | Add principal-client authentication, the provider submission endpoint, request limits, multipart translation, response mapping, and composition. |
| `src/Pegasus.Infrastructure/Persistence/` | Read the API-04 credential/principal binding and support any provider-source query needed by authentication; add no second intake store. |
| `tests/Pegasus.Core.Tests/`, `tests/Pegasus.IntegrationTests/`, `tests/Pegasus.ArchitectureTests/` | Prove translation, replay/conflict, principal actor attribution, request limits, authentication, composition, and dependency direction. |
| `docs/frd/frd-09-provider-and-intermediary-routes.md`, `docs/adr/0004-provider-api-and-staff-mcp-authentication.md`, `docs/capabilities.md`, `docs/open-decisions.md`, `docs/current-architecture.md` | Record the operator-authorized simplified receipt/result contract and as-built caller; supersede rather than rewrite ADR-0004. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `src/Pegasus.Core/Intake/DurableIntake.cs` | The durable receipt, source identity, operation-key, replay, and work-state owner already exists. |
| `src/Pegasus.Core/Intake/GroupedIntake.cs` | Existing bounded multi-file ordering and child-token semantics must be reused. |
| `src/Pegasus.Web/Pages/Upload.cshtml.cs` | Existing Web translation and upload limits are precedent, not a business-policy owner to copy. |
| `src/Pegasus.Worker/IntakeFunctions.cs` | Submission completion is deliberately separate from background processing. |
| `docs/boundaries.md` | Provider endpoints and credentials remain absent until exact activation evidence exists. |
| `docs/design/README.md` | Provider clients receive no staff shell or Administration access. |

## Ripple effects

API-04 must provide an enabled credential before API-01 can authenticate. API-03 consumes the returned receipt. Migration, Web route authorization, OpenAPI/contract tests, telemetry, current-state docs, and deployment configuration follow when implemented.

## Out of scope

Transient processing status, general Case search/read, Case workflow mutation, email-domain tenancy, live provider activation, cloud writes, report delivery, and performance optimization.
