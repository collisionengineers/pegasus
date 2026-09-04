# Research — CASE-032 (2026-09-04, gpt-5.6-terra medium)

Audited read-only at checkout `80f0ca262b0fe2ca354a5dfb18933dc3f105b917`.

## Verified findings

- EPIC-011 §1.4 requires image rows `ref·reg, files·custody` and Triage
  rows `ref·reg, provider·assignee`.
- `ImageIntakeSummary` is
  `Id, OriginReceiptId, ImageIntakeReference, NormalizedVehicleRegistration,
  AssociatedCaseId, AssociatedCaseReference, RegisteredAtUtc, State,
  ClosureReason` at
  `src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs:100-109`.
- `EfImageIntakeStore.ProjectAsync` is the shared projection used by
  `ListAsync`, `ListByOriginReceiptsAsync`, `ListForCaseAsync`, and
  `SearchByRegistrationAsync`. Its EF select at
  `src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs:857-920`
  does not select `ImageIntakeEntity.CustodyState`.
- The persisted custody vocabulary is currently Infrastructure-internal:
  nullable state plus `pending`, `confirmed`, `merged`, and `failed` at
  `src/Pegasus.Infrastructure/Persistence/ImageIntakeEntities.cs:41-65`.
  There is no Core custody enum/value for image intakes.
- `ImageRow` renders only the file count at
  `src/Pegasus.Web/Pages/Cases/Index.cshtml.cs:543-558`. It is called from
  `LoadNotReadyAsync` at `:379-414`.
- `TriageSummary` is currently
  `Id, NormalizedVehicleRegistration, State, AssigneeId, LinkedCaseId,
  CreatedAtUtc, Version` at
  `src/Pegasus.Core/Triage/TriageContracts.cs:271-278`.
- `EfTriageStore.ListAsync` reads `TriageEntity` and constructs that summary
  at `src/Pegasus.Infrastructure/Persistence/EfTriageStore.cs:458-481`.
  `TriageEntity` itself has no reference or provider field
  (`src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs:1259-1279`).
- `TriageRow` titles with registration and uses assignee as its meta at
  `src/Pegasus.Web/Pages/Cases/Index.cshtml.cs:560-575`; `LoadTriageAsync`
  supplies only the assignee at `:417-430`.
- The closest existing Core provider vocabulary is
  `MailRouteSelection.WorkProviderCode`
  (`src/Pegasus.Core/Intake/IntakeContracts.cs:189-203`). It is available only
  when the origin receipt has a selected mail route. It is a code, not a
  provider display name, and it is not carried by Triage.
- This matches the already-recorded INTK-046 finding: Core has no Triage
  reference/provider; registration and source channel were its only available
  identity facts. CASE-032 is correctly the queue-projection owner; INTK-046
  did not own it.

## Query-count baseline and risk

The page has no fixed SQL-query count: it always starts three independent
count reads (`stage`, `triage`, `unidentified`) at
`Pages/Cases/Index.cshtml.cs:307-315`, then adds reads for the selected tab.

For Not ready, the row path adds one case query, one image-summary projection,
and one `ListImagesAsync` call per displayed image row
(`:381-414`). This is already an image-row-dependent N+1 pattern for file
counts. Each image call performs multiple persistence reads
(`EfImageIntakeStore.cs:765-810`), so a static source audit cannot truthfully
state one exact SQL total.

For Triage, the tab adds a list read plus one staff-account read per distinct
assignee: `ActorDisplayNames.ResolveStaffNamesAsync` loops through IDs at
`src/Pegasus.Core/Actors/ActorDisplayNames.cs:26-45`. Therefore the required
"unchanged/no new N+1" criterion must mean that custody, reference, and
provider are folded into the existing summary reads; it cannot mean the page
is presently free of all N+1 behavior.

## Reuse candidates

- Image custody: extend `ImageIntakeSummary`; extend
  `EfImageIntakeStore.ProjectAsync`; extend `ImageRow`. No new query type fits
  the requirement.
- Triage: extend `TriageSummary`; extend `EfTriageStore.ListAsync`; extend
  `TriageRow`. No existing Triage projection supplies the requested values.
- Presentation: `OperatorLabels` owns Triage state and document-custody labels,
  but has no image-intake custody label. Do not emit persistence literals from
  the page.
- Tests: `TriageQueuesWebTests.NotReadyImageRowRendersRetainedImageCountAndChaseState`
  (`:163-183`) is the existing image-row assertion. This class has no current
  Triage-row test. `ImageIntakeCasePairingTests` and
  `DashboardBoundaryTests` construct summaries in Core tests, but do not assert
  these display fields.

## Scope and risks

The image half is straightforward once a Core-facing custody value and its
operator label are settled. The Triage half is not implementable from the
currently projected/persisted Triage data without either deriving values from
the origin receipt under defined rules or introducing new identity data. A
new Triage reference allocation or provider-name persistence would expand the
ticket beyond its stated "existing vocabulary" approach and may require schema
work; neither should be assumed.

`Pages/Cases/Index.cshtml.cs` is shared with CASE-042. CASE-032 should limit
itself to `LoadNotReadyAsync`/`ImageRow` and `LoadTriageAsync`/`TriageRow`;
do not restructure selection, tabs, or quick detail.

## Assumptions

- None about the semantics of a Triage reference or provider were made.
- No query-count instrumentation was found or run; the count analysis above is
  source-derived.
- No code, ticket state, or board documents were modified.
