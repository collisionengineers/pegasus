# ENG-041 correction file scope

This is the proposed post-merge correction scope only, pending root approval.
The original implementation map `files/files.md`@`9eb56be81f06f9a0` and
PR683's delivered diff remain historical evidence, not future edit authority.

| Path | Responsibility |
| --- | --- |
| `src/Pegasus.Infrastructure/Custody/EfCaseArtifactCustody.cs` | Keep confirmed-source staleness atomic; remove automatic Case version increment and lease clearing, including the shared reconciliation caller. |
| `src/Pegasus.Infrastructure/Persistence/EfAssessmentReportProjectionSource.cs` | Extract its one confirmed-document query and existing source/readiness mappings as internal static helpers used by projection and freeze. |
| `src/Pegasus.Infrastructure/Persistence/EfCaseReportGenerationStore.cs` | Recheck the complete captured source census inside the existing serializable freeze transaction before creating or reusing a generation. |
| `tests/Pegasus.IntegrationTests/GlassRepairEstimateCallbackWebTests.cs` | Preserve both actual callback success/replay assertions; assert unchanged authority through the session's own custody stage where appropriate. |
| `tests/Pegasus.IntegrationTests/CaseArtifactCustodyRecoveryTests.cs` | Actual immediate/recovered custody and replay preserve an existing live lease and Case version. |
| `tests/Pegasus.IntegrationTests/Reports/CaseReportGenerationPersistenceTests.cs` | Complete real-shaped fixture census; pre-freeze source races, exact metadata, currentness, staleness, generated-output exemption and runtime role evidence. |
| `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` | Only the existing snapshot-freshness paragraph: source recheck and automatic custody preserving staff authority. |

## Existing callers and unchanged boundaries

Glass ExportAsync retains XML/PDF through the registered EfCaseArtifactCustody;
FinishAsync uses the original protected authority with ImportRawEstimate.
ReconcilePendingArtifactCustody calls the same confirmation helper.
EfAssessmentReportProjectionSource and EfCaseReportGenerationStore are the two
concrete production callers of the extracted query/mappings. No new interface,
store, DTO, migration, grants, package, runtime, UI handler or provider API.

Do not edit explicit staff Add/Remove document commands in EfDocumentCustodyStore,
CaseMutationGuard, Glass gateway/import authority, Case Details, shared labels,
estimate save/replay, CASE-049 handoff/access files, or generated UI snapshots.
CASE-049's live file map @4689efb462748ddf intersects this proposed scope only at
FRD-11; reserve the freshness paragraph explicitly before concurrent execution.
DOCS-020 is Done with claim released; retain its earlier proof/history.

## Execution boundary

Root alone approves and assigns the correction after reading the whole plan.
Reuse the recorded ENG-041-glass-recovery branch and .worktrees/eng-041
worktree; no second claim/worktree. Root prepared an origin/dev merge-base
refresh at 19e6f523bf6760cab39104b4dca3674b0ac8a512; author must validate the
fresh resume packet and exact owned root before integrating it. No source
edits, build/test, snapshot, cloud or provider calls were performed by planning.
