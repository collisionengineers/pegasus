# B05 — Immutable full report and fee-note generation (integrated 606631707)

Delivered from `b-work/b05` (11 commits, squashed) on the B head; verified
before fast-forward: solution build 0 errors, Core 1440/1440, Architecture
100/100, report persistence 9/9, renderer/draft/approval 28/28.

## Files

- `src/Pegasus.Core/Reports/CaseReportGeneration.cs` — contracts,
  `CaseReportReadiness.Evaluate`, `GenerateCaseReport`.
- `src/Pegasus.Core/Reports/AssessmentReportRendering.cs`,
  `AssessmentReportProjection.cs` — canonical cost block, source-aware
  statement of truth, prepared-image projection with Box identities.
- `src/Pegasus.Infrastructure/Persistence/EfCaseReportGenerationStore.cs`
  (also `EfCaseReportContentSource`),
  `EfAssessmentReportProjectionSource.cs`,
  `src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs`.
- Templates `docs/design/assets/report-renderer/templates/assessment_report.scriban`,
  `report.css` (embedded renderer assets, not site CSS).
- Tests: new `tests/Pegasus.IntegrationTests/Reports/CaseReportGenerationPersistenceTests.cs`
  (9); Core report tests extended; two renderer-test defects corrected
  (`OnlyTheRequestedKindIsRendered` used a non-discriminating marker,
  `TheRendererPublishesItsEngineVersionWithoutRendering` disposed an
  `IAsyncDisposable` synchronously).

## Behaviour

- Freeze (transaction 1, serializable) → render under the 2-minute
  `AssessmentReportRenderPolicy.RenderTimeout` → `ICaseArtifactCustody.RetainAsync`
  outside any transaction → confirm (transaction 2). A Pending/Unknown artifact
  retries through `ICaseArtifactCustodyStatus.GetAsync` first; no observer.
- Ready row written inside confirm, at most once, only when every artifact is
  Confirmed and the generation is not Stale: `EventKind=case_report_generation_ready`,
  `Outcome=Succeeded`, `AggregateType=case`, `AggregateId=<case id D>`,
  `CorrelationId=<operation key>`, `AfterJson` root `generationId` string.
  Custody confirmation alone is never ready.
- `MarkStaleAsync(caseId, reasonCode)` moves only the current generation.
- EVA never gates readiness (H3); Glass's clause split verbatim from the
  approved `StatementOfTruth3` and printed only when disclosed and used —
  otherwise omitted, never substituted (H5).
- Custody occurrence identity `case-report:{generationId:D}:{Kind}`.

## Owed to the wiring wave (B08) and to A

- No production caller yet for `GenerateCaseReport`, `MarkStaleAsync`,
  `CaseReportArtifactKind.FeeNote`; Details handlers and fee-note preview are
  wiring work. `MarkStaleAsync` must be called from the workspace, estimate,
  valuation and preparation store transactions.
- Preview path still gates on EVA `CanOpen`; retarget in wiring (H3).
- v3 §F readiness order not pinned to the merged list.
- DI lines for A published on PR 672 (B06 store, B05 stack, `IGetCaseHeader`).
- Inherited failing B tests (not B05's): `AssessmentPersistenceIntegrationTests`
  `AutomationSaveIsUnconfirmedAttributedAndParityLoggedWithAStaffSave` and
  `NamedEstimatesSaveDuplicateDiscardSetCurrentAndListWithOneCurrentPerCase`.

## Simplification pass — 2026-09-06

Ten behaviour-preserving findings applied (duplicate image bound removed,
LINQ join over a throwaway dictionary, one fewer round trip in
`GetCurrentAsync`, named tuple members, dedent, hoisted locals). Not applied:
Details `FeeNote`/alias cleanup (wiring), unregistered stack (A's DI),
hand-rolled `ActionHistoryEntity` in `MarkStaleAsync` (rejected: sibling
stores use the same convention because `Succeeded` sets no `PolicyVersion`),
report-date derived twice (deferred: cross-stream contract), field accessor
copies in `CaseReportReadiness` (deferred), renderer `Register` never disposed
(review item, not simplification).
