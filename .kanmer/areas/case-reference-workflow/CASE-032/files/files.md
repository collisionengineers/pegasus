# Files — CASE-032

## Pegasus.Core

`src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs` — change: add the Core-facing image custody projection value to `ImageIntakeSummary`; its vocabulary must not expose Infrastructure-only literals directly.

`src/Pegasus.Core/Triage/TriageContracts.cs` — change: add Triage reference and provider projection values to `TriageSummary`, once their business sources and absent-value rules are resolved.

`tests/Pegasus.Core.Tests/ImageIntake/ImageIntakeCasePairingTests.cs` — possible compatibility update: its `ImageIntakeSummary` helper is the only Core test constructor with explicit positional summary values.

`tests/Pegasus.Core.Tests/Operations/DashboardBoundaryTests.cs` — possible compatibility update: its `NewTriage` helper constructs `TriageSummary`.

## Pegasus.Infrastructure

`src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs` — change: select the persisted custody state in `ProjectAsync` and map it into the extended summary without an extra per-row read.

`src/Pegasus.Infrastructure/Persistence/EfTriageStore.cs` — change: project the resolved reference/provider from the authoritative origin data in the existing list read, if the operator resolves their required semantics.

`src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` — no change under the stated projection-only approach; change only if the operator requires newly persisted Triage identity/provider data.

`src/Pegasus.Infrastructure/Persistence/Migrations/**` — no change under the stated projection-only approach; required only if a new persistent Triage reference/provider is authorised.

## Pegasus.Web

`src/Pegasus.Web/Pages/Cases/Index.cshtml.cs` — change: render `files·custody` in `ImageRow` and `ref·reg` / `provider·assignee` in `TriageRow`; shared with CASE-042, so restrict the diff to those row-loading/building paths.

`src/Pegasus.Web/Presentation/OperatorLabels.cs` — change: add the one image-intake custody display mapping in a ticket-delimited block, if the Core contract introduces a custody value.

`src/Pegasus.Web/Pages/Search/Index.cshtml.cs` — change: preserve the new custody member when its exact-reference path reconstructs `ImageIntakeSummary` at `:238-247`.

## Pegasus.IntegrationTests

`tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs` — change: extend the image row seed/assertion for custody and add seeded Triage assertions covering reference, registration, provider, and assignee.

## No additions or deletions currently justified

No new query type, page, service, migration, test project, or file is justified by the verified projection-only scope.

## No migration expected

Under the stated projection-only approach (extend the two existing Core
summaries and their EF projections over existing columns), no migration is
required. A migration becomes required only if the operator's answers to the
open questions below authorise persisting a new Triage reference or provider
value that does not already exist in `TriageEntity`.
