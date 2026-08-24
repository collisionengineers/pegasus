# Files

| Path | Change |
| --- | --- |
| `src/Pegasus.Core/Intake/IntakeAllocation.cs` | Delete the `AutomaticCompleteness` constant (`:224-228`); build the record at the call site (`:269`) with `ImagesComplete` observed from the receipt's own assets. Keep the CASE-013 warning in the doc comment and say which half is now observed. |
| `tests/Pegasus.Core.Tests/Cases/AutomaticCaseReadinessTests.cs` | The CASE-013 regression guard. Add the observed-images cases beside it: no photographs → not ready; one photograph → ready; a letterhead banner only → not ready. |
| `tests/Pegasus.IntegrationTests/` (existing acceptance suite) | One end-to-end assertion that an automatic allocation from a receipt with no photographs lands in `NotReady` with its chase scheduled, not `Review`. Core tests do not reach `EfCaseAcceptanceStore`. |

**Reused, not written:** `InstructionEvidenceImages.Select` — already the single Core
owner of "which retained assets are this case's photographs", already what custody
uses to decide which assets become `DocumentSemanticRole.Image` documents, which is
exactly the population the export then counts. Asking it at allocation makes Review
agree with export by construction rather than by a second rule.

**No document change.** FRD-01 and `operator-notes.md` already state the behaviour;
this makes the code match them.

## Deliberately not in this diff

`EfQueuedCustodyProcessor.cs:309-312` files every `image/*` attachment as
`DocumentSemanticRole.Image` using `IsImage` alone, without `IsPhotographShaped`.
So a letterhead banner becomes export-eligible while the gallery excludes it. After
this fix a receipt carrying only a banner would be `ImagesComplete: false` yet still
export — a narrower disagreement in the opposite direction. Real, but folding it in
widens the diff and risks the export side. Filed separately.
