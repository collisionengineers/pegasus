# Post-implementation report — ENG-004

PR: https://github.com/collisionengineers/pegasus/pull/437 (base `dev`, branch `task/eng-004-mot-row-pollution`, commit 06bfbda1). Worktree `../pegasus-worktrees/eng-004`.

## What changed

- `src/Pegasus.Core/Intake/InstructionFieldExtraction.cs`
  - Label matching now requires a plausible label position: line start, or after `|`, `;`, tab, or a run of 2+ spaces (was: after any single whitespace anywhere on the line).
  - Labelled values are truncated at the first flattened-column boundary (`TruncateAtColumnBoundary`: tab, `|`, 2+ spaces, or whitespace-preceded `:`), applied after the existing leading-separator trim and to the next-line fallback too.
  - `FieldDefinition` gains an optional `AcceptsValue` candidate predicate; a rejected candidate is never suggested.
  - New `IsPlausibleVehicleMakeModel` beside the existing typed-value helpers: rejects wheel-position tokens (NSF/OSF/NSR/OSR), MOT/brake test-result vocabulary (SATISFACTORY/ADVISORY/DANGEROUS/FOOTBRAKE/HANDBRAKE/PASS/FAIL/MOT, word-bounded), and any character outside letters/digits/space/`- . ' & / ( ) +`.
- `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosInstructionExtractionPolicy.cs` — wires the validator onto `Vehicle make` and `Vehicle model`.
- `tests/Pegasus.Core.Tests/Intake/Qdos/QdosInstructionExtractionPolicyTests.cs` — 4 new fixtures.

## Evidence

- Red first: `FlattenedMotTableRowsAreNeverOfferedAsMakeOrModel` failed before the fix with the literal production value `"AUDI NSF : Footbrake : SATISFACTORY"` (4 new tests failing, 5 passing).
- Green: focused extraction classes 103/103; full `Pegasus.Core.Tests` 695/695; `dotnet build ./Pegasus.slnx -c Release` 0 warnings, 0 errors.
- Pinned behaviours preserved (overlong-candidate retention, `"Claim Number | X"` trim, next-line fallback, genuine-date conflict) — all pass unchanged.

## Ticket verification checklist state

- Fixture test with an MOT-history/brake-table proving the exclusion: DONE.
- QDOS26002 re-extracted in production: NOT done here — the real PDF is not accessible to this task and re-extraction is a production action; belongs to verify/closeout after merge. The fixture reproduces the exact flattened line shape recorded in the production CaseDataFields suggestion rows.

## Deliberately left out (owned by [[INTK-017]])

Section awareness (instruction fragment vs appended report), conflict resolution (identical values / validated-beats-unvalidated), the absent VRM extraction, and single-space multi-field line segmentation.
