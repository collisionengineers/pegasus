2026-09-02 research started (kanmer-research wrapper; gpt-5.6-terra xhigh in .worktrees/research at cad00be9).

2026-09-02 planning started (kanmer-plan wrapper; gpt-5.6-terra xhigh in .worktrees/research at 897db953 = origin/dev after DELIV-041 #647). No source diff since research SHA cad00be9; FRD-06 §Damage record / §Settlement now carry D39/D41 but define neither the severity aggregation nor the equity formula, so both operator questions stay open.

## Lane interrupted and resumed (2026-09-03)

The first Build wave-1 run (`wf_82493dad-cf6`) was stopped deliberately by the
controller to switch the wave to per-wave verification. ENG-035 was the only
lane in flight. Its worktree `.worktrees/eng-035` on
`task/eng-035-assessment-vocabulary` was left with uncommitted work from the
gpt-5.6-sol run: fifteen modified files and a new migration
`20260903110926_ExtendAssessmentVocabulary`. Nothing was committed or pushed.

The ticket keeps its taken record, branch and worktree; the replacement run
(`wf_63d90843-641`) resumes that exact worktree under the repository's
resumed-execution-packet rule — no second worktree, no second take — reads the
inherited diff against this plan and continues from it.

Cause of the interruption being possible at all: the lane was mis-classified
as non-serial because its owned-path text said "migration" in lower case and
the shared-lock test was case-sensitive. Fixed in the build script; ENG-035 is
serial, as its migration requires.

## Integration merge (2026-09-03)

Merged `origin/dev` into `task/eng-035-assessment-vocabulary` to pick up two
sibling tickets that landed after ENG-035's content review passed:
PLAT-070 (migration `20260903153134_RemoveStaffReviewFlags`) and DOCS-017
(migration-free `ReportSignatory` replacing the fixed `ReportEngineer`
tuple, plus `Field(assessment, ...)` accessor changes).

Conflicts and resolution:

- `src/Pegasus.Core/Reports/AssessmentReportProjection.cs` — dropped the
  `engineerSignature`/`engineerName`/`engineerQualifications` locals and the
  `Engineer: new ReportEngineer(...)` snapshot member (DOCS-017 supersedes
  them); kept `var signatory = input.Signatory!;` and
  `Signatory: new ReportSignatory(...)` from dev. Kept ENG-035's
  `Damage: BuildDamage(fields)` and `Settlement: BuildSettlement(fields, ...)`
  members. For `HistoryCheck`, `EngineerComments`, `AgreedFee`, and
  `FeeDescriptionLines` — the four fields DOCS-017's diff touched — kept
  dev's `Field(assessment, ...)` accessor rather than ENG-035's
  `Field(fields, ...)` dictionary lookup; every other field (Outcome,
  LegalStatus, vehicle fields, damage/settlement fields, etc.) keeps
  ENG-035's `Field(fields, ...)`.
- `tests/Pegasus.Core.Tests/Reports/AssessmentReportProjectionTests.cs` —
  merged both assertion blocks in
  `CompleteInputProjectsToARenderableSnapshot`: all of ENG-035's new
  Vehicle/Damage/Settlement assertions, followed by DOCS-017's
  `snapshot.Signatory.PrintedName`/`Qualifications` assertions.
- `tests/Pegasus.IntegrationTests/Reports/AssessmentReportRendererTests.cs` —
  in the `Snapshot(...)` test builder, kept ENG-035's `Damage:`/`Settlement:`
  record arguments and replaced `Engineer: new ReportEngineer(...)` with
  dev's `Signatory: new ReportSignatory(...)`.
- `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs` did
  not conflict (already carried both migrations,
  `20260903110926_ExtendAssessmentVocabulary` then
  `20260903153134_RemoveStaffReviewFlags`, in order) — no edit needed.

Files that auto-merged cleanly and were spot-checked for coherence (no
manual resolution needed): `AssessmentReportRendering.cs` (both
`ReportDamage`/`ReportSettlement` and `ReportSignatory` records present, no
`ReportEngineer`), `PlaywrightAssessmentReportRenderer.cs` (binds
`snapshot.Signatory.*` for the signature block alongside ENG-035's
Damage/Settlement rows), `EfAssessmentReportProjectionSource.cs`
(`Signatory: null` is DOCS-017's own pre-existing placeholder, not
introduced by this merge — out of ENG-035's scope), and the scriban
template/FRD-11/test-UI snapshot.

`AssessmentVocabulary.EngineerName`/`EngineerQualifications`/
`EngineerSignature` constants and their `AssessmentPolicy` readiness
requirements are untouched — they remain assessment-field vocabulary
independent of the report snapshot's `ReportEngineer`→`ReportSignatory`
change, so left alone.

Verification (from `.worktrees/eng-035`):
- `dotnet restore ./Pegasus.slnx --locked-mode` — RESTORE_EXIT=0
- `dotnet build ./Pegasus.slnx --configuration Release --no-restore` —
  BUILD_EXIT=0 (0 warnings, 0 errors)
- `dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj
  --configuration Release --no-build` — CORETEST_EXIT=0 (1203 passed)
- `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj
  --configuration Release --no-build` — ARCHTEST_EXIT=0 (100 passed)

Integration suite not run locally per instruction; GitHub CI covers it on
PR #648.

Merge commit `d515bb8c` pushed to
`origin/task/eng-035-assessment-vocabulary` (was `a53f35bc`). PR not
merged, ticket stage unchanged.
