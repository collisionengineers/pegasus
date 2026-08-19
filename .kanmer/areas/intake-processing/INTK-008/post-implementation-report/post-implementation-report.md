# Post-implementation report — INTK-008

## Summary

Implemented Image-initiated Case as an explicit lifecycle projection over the
existing ImageIntake aggregate. A usable VRM retains its immutable per-VRM
reference and can be Awaiting instruction, Merged into an Instruction-initiated
Case, or Staff-closed with a reason. No formal Cases row, Principal, Case/PO,
Audit, or Unidentified reference is allocated.

## Changes

- Core ImageIntake contracts now carry lifecycle state, terminal target/reason,
  history, merge/close requests, and bounded transition validation.
- EF persistence stores state/version and append-only lifecycle events with a
  serializable transition and operation-key replay; the migration is
  20260819112914_ImageInitiatedLifecycle.
- Reverse accepted-Case pairing records a merge projection and formal Case
  history event after the existing receipt association succeeds.
- ImageIntake list/detail pages use Image-initiated Case vocabulary, preserve
  exact reference/VRM search, show lifecycle/history, and provide reasoned staff
  closure under the existing staff authorization boundary.
- A distinct IImageIntakeCustody target and local/Box implementations use the
  immutable VRM reference and existing custody composition/root fence; no second
  Box client or formal Case folder is introduced.
- PRD, FRD-01/02/05/06/12, design, capabilities, index, CONTEXT, and operator
  notes were reconciled. ADR-0029 records the technical decision and ADR-0013
  is marked superseded in frontmatter only.

## Verification

- dotnet restore Pegasus.slnx: passed.
- Release builds: Core, Infrastructure, Web, IntegrationTests, and
  ArchitectureTests passed with 0 warnings/errors in the completed builds.
- Focused ImageIntake Core tests: 40 passed, including lifecycle validation,
  pairing, replay, and existing registration coverage.
- SQL/web integration execution was attempted through the existing test
  harness; no external Box mutation was performed. Full integration and full
  solution test commands remain for merged-main verification.

## Risks / follow-ups

- Existing ImageIntake source-file/group projection remains the source of
  preserved filenames and ordinals; a future UI slice may show a richer grouped
  asset panel.
- Box custody root creation is exposed through the existing guarded adapter but
  is not automatically invoked during local registration; external custody
  dispatch remains governed by the existing queued custody boundary.
- INTK-007 owns durable U<n> Unidentified allocation and conflicting_vrms
  persistence; INTK-006 owns grouped recognition and routing.

## Takeover correction — 2026-08-19

The sections above were written before a review pass (13 Codex PR comments,
all P1/P2) found five real defects and this takeover fixed all of them; two
paragraphs above are now stale and are corrected here rather than rewritten
in place, so the original implementation record stays intact.

- **Custody**: the "distinct IImageIntakeCustody target" paragraph above is
  wrong — it had no application caller anywhere (confirmed by repo-wide
  search) and shipped dark. Removed entirely: the interface, both adapter
  implementations, and the DI registration are gone, and the DI/custody files
  are now byte-for-byte identical to `origin/dev` (`git diff origin/dev --
  src/Pegasus.Core/Custody src/Pegasus.Infrastructure/Custody
  src/Pegasus.Infrastructure/DependencyInjection.cs` is empty). The matching
  claim was also removed from ADR-0029 and FRD-05; Image-initiated files stay
  under the existing intake source-artifact retention until a merge makes
  them available for the formal Case's own Box custody. The "Risks /
  follow-ups" bullet about custody root creation is superseded by this.
- **Migration backfill**: the migration now backfills a pre-existing
  ImageIntake whose origin receipt already resolves to a Case (via
  `IntakeManualAssociations`/`CaseIntakeLinks`, mirroring
  `EfImageIntakeStore.CurrentCaseId`) to `merged_into_instruction_case` with
  its target Case id/reference, instead of leaving it `awaiting_instruction`.
- **Lifecycle transition ownership**: `MergeAsync`/`CloseAsync` now call
  `ImageIntakeLifecycleRules.ValidateMerge`/`ValidateClose` before persisting,
  and the replay path now compares a stored request fingerprint (new
  `RequestFingerprint` column on `ImageIntakeLifecycleEvents`, same pattern as
  `ImageIntakes.RequestFingerprint`) instead of trusting any request with a
  reused operation key. `ImageIntakeCasePairing.SyncMergeAfterLinkAsync` is
  now the one place that transitions an Image intake to Merged; the automatic
  forward path (`ImageIntakeAutomation`), the reverse path
  (`ImageIntakeCasePairing.PairAcceptedCaseAsync`), and the manual staff
  `LinkIntake` path all call it, so a manually linked record no longer stays
  `AwaitingInstruction` forever and a merge that fails after its association
  already committed is retried on the next call from any of those three
  entry points (deterministic, replay-safe operation key).
- **Web presentation**: `OperatorLabels.ImageIntakeLifecycleState` and two new
  `OperatorLabels.HistoryEvent` codes replace every raw enum/snake_case
  rendering on the Index and Details pages; the exact-reference search result
  on Index now carries the real state/closure reason instead of defaulting to
  Awaiting; `Details.cshtml.cs` now catches `DbUpdateConcurrencyException` as
  a normal conflict outcome instead of a 500.
- **Verification (superseding the counts above)**: `dotnet build
  Pegasus.slnx -c Release` — 0 warnings/errors. `dotnet test
  tests/Pegasus.Core.Tests -c Release` — 644 passed. `dotnet test
  tests/Pegasus.IntegrationTests -c Release --filter
  "FullyQualifiedName~ImageIntake|FullyQualifiedName~IntakePersistenceIntegrationTests"`
  — 17 passed. `dotnet test tests/Pegasus.ArchitectureTests -c Release` — 97
  passed. `QdosAllocationRecoveryTests.DistinctParallelRetriesResolveToOneCaseAggregate`
  investigated separately (see the ticket plan's dated Simplification pass /
  QA section) — confirmed pre-existing intermittent flakiness on clean
  `origin/dev` itself, not a regression from this branch's optional
  `IImageIntakeStore` parameter.
