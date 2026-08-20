# Plan — INTK-013

## Chosen fix

Extend `CaseStageCounts.NotReady` at its one source
(`EfDashboardQueries.GetCaseStageCountsAsync`) to count image-initiated
awaiting-instruction Image Intakes in addition to instruction-initiated
`CaseWorkflows` rows, rather than recomputing the badge separately in the
Triage page. Reasons:

1. The Dashboard's Not-ready tile (`Pages/Index.cshtml.cs`) reads the same
   `GetCaseStageCountsAsync` call — fixing the shared source keeps the
   Dashboard tile and the Queues tab badge consistent with one change, which
   the ticket explicitly calls for ("keeps the Dashboard page's Not-ready
   tile consistent with the tab").
2. It is a single additional aggregate `CountAsync`, matching the class's own
   documented invariant ("none of them projects rows into memory to count
   them") — cheaper than calling `IImageIntakeQueries.ListAsync` (which loads
   full summary rows) just to `Count()` them.
3. `LoadNotReadyAsync`'s per-origin queries (used for the actual rows and the
   Instruction/Image origin sub-filter) are untouched — the fix only makes
   the *aggregate* badge agree with what those two queries return combined.

## Steps

1. **`EfImageIntakeStore.cs`**: widen
   `private static string ToCode(ImageInitiatedCaseState state)` to
   `internal static`. No behaviour change — reused as-is from
   `EfDashboardQueries`.
2. **`EfDashboardQueries.GetCaseStageCountsAsync`**: add
   `var imageInitiatedNotReady = await context.ImageIntakes.AsNoTracking()
   .CountAsync(item => item.MergedIntoCaseId == null && item.LifecycleState ==
   EfImageIntakeStore.ToCode(ImageInitiatedCaseState.AwaitingInstruction),
   cancellationToken);` and return
   `new(For(notReady) + imageInitiatedNotReady, For(review), For(held))`.
   This exactly mirrors the filter `LoadNotReadyAsync` already applies
   (`IImageIntakeQueries.ListAsync(false, ...)` → `AssociatedCaseId is null`,
   i.e. `MergedIntoCaseId == null`, then `.Where(state ==
   AwaitingInstruction)`), so the two queries agree by construction.
3. **`DashboardCounts.cs`**: update the XML doc comment on `CaseStageCounts`
   to note `NotReady` spans both case origins. No shape/interface change.
4. **Tests** — `tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs`:
   add `NotReadyBadgeCountMatchesRowsAcrossBothOrigins`, reusing
   `SeedNotReadyCaseAsync` for one instruction-initiated NotReady case and the
   existing `IRegisterImageIntake`/`IImageIntakeOriginResolver` flow (copied
   from `NotReadyOriginFilterReturnsOnlyTheMatchingOriginsRows`) for one
   image-initiated awaiting-instruction Image Intake. Assert:
   - `/Triage?queue=not_ready` badge (`<span class="count">2</span>` inside
     the Not ready tab link) equals 2.
   - Both the instruction case reference and the image intake reference are
     present in the rendered rows (row count == badge).
   - `GET /` (Dashboard) reports the same NotReady figure (2), proving the
     tile and the tab agree.
5. Run focused build + tests (below). No migration, no new table, no DI
   change beyond the one visibility widen.

## Verification commands

- `dotnet build ./Pegasus.slnx -c Release --no-restore`
- `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --filter "FullyQualifiedName~TriageQueuesWebTests"`
- `dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --filter "FullyQualifiedName~DashboardBoundaryTests"` (regression only — untouched contract)

## Simplification pass

To be recorded after implementation, before PR, under a dated heading.
