# Files — INTK-059

## Likely changed files

| File | Change and risk |
| --- | --- |
| `src/Pegasus.Core/Triage/TriageContracts.cs` | Carry an optional principal relationship through the Triage create, record, detail and summary contracts. Constructor changes ripple to every caller and fake. |
| `src/Pegasus.Core/Intake/DurableIntake.cs` | Pass the already accepted route/declaration principal into Triage creation. Must preserve the one accepted-match gate and fail-closed behaviour. |
| `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` | Add the optional `Triage` → `Principal` relationship and its restrictive foreign key. |
| `src/Pegasus.Infrastructure/Persistence/EfTriageStore.cs` | Persist and map the relationship in creation, detail and queue queries. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/*` and `PegasusDbContextModelSnapshot.cs` | Add the schema migration; include database grants in the same change. |
| `src/Pegasus.Web/Pages/Triage/Details.cshtml(.cs)` | Project and render the known principal read-only without a second lookup or inferred value. |
| `tests/Pegasus.IntegrationTests/QdosTriageIntegrationTests.cs` | Prove accepted-route Triage persistence and the rendered principal. |
| `tests/Pegasus.Core.Tests/Triage/*` and Triage store fakes | Update contract construction and cover the absent-principal manual path. |
| Provider-API Triage integration coverage | Prove the authenticated declaration path supplies the same optional relationship without QDOS coupling. |

## Context files

| File | Why it must be read |
| --- | --- |
| `docs/frd/frd-03-triage.md` | Authoritative rule: manual Triage may not invent a Principal. |
| `docs/frd/frd-02-intake-and-source-identity.md` | Principal-resolution and fail-closed intake invariants. |
| `src/Pegasus.Core/Intake/ProcessIntake.cs` | Owns accepted route/declaration assessment and establishes the principal code. |
| `src/Pegasus.Core/Intake/DurableIntake.cs` | Sole automatic Triage-creation call site and accepted-evidence gate. |
| `src/Pegasus.Core/Triage/TriageContracts.cs` | Aggregate/port ownership; no second business rule belongs outside Core. |
| `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` | Existing Triage schema and principal FK conventions. |
| `src/Pegasus.Infrastructure/Persistence/EfTriageStore.cs` | One persistence implementation for Triage reads and writes. |
| `src/Pegasus.Web/Pages/Triage/Details.cshtml(.cs)` | Existing production caller and the page to extend. |
| [[INTK-033]] and [[INTK-046]] | Respect the current creation pipeline and Triage-page ownership. |

## Deliberately out of scope

- Adding a new Triage matcher, changing QDOS classification tells, or expanding
  supported mail routes.
- Inferring a principal from message text or backfilling historic Triage rows.
- Changing Case allocation, the Triage lifecycle, or manual-classification
  semantics.
