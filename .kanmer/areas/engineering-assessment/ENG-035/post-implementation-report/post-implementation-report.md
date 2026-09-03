# Post-implementation report — ENG-035 (2026-09-03)

## Summary

Extended the closed Core assessment field-map vocabulary with the full
vehicle-extra, tyre/belt, unrelated-damage, material-transfer and
settlement paths, the canonical D45 `damage.impacts` JSON structure
(`zone`, `severity`, `note` — no `type`), Core-derived `impact_location`
and `impact_severity`, and projected the expanded record into the
assessment report and its Scriban template. `pegasus_assessment_update`
(the MCP tool) required no change: it delegates to `ISaveAssessment` and
enumerates no paths, so the new vocabulary is admitted and derived-field
writes are rejected by the same generic route.

## Resumed-lane note

This ticket's worktree (`task/eng-035-assessment-vocabulary`,
`.worktrees/eng-035`) already carried uncommitted work from a wave-1 run
the controller stopped deliberately (`scratch/notes.md`). The inherited
diff — 15 modified files plus the generated migration — matched the plan's
"Expected files" table exactly and the plan document already carried a
completed, reviewed simplification-pass record with a prior full-suite
run (`Category!=Corpus`: Core 1200, Architecture 100, Integration 1227
passed/2 skipped, exit 0). Rather than re-implementing, this session
independently re-verified the inherited tree (fresh restore, build, the
two fast local test projects, migration grants, a `git diff --check`, and
a spot read of the diff against the plan's pinned vocabulary table and
file-ownership exclusions) and found it correct, so it was committed as
delivered rather than redone.

## Files changed

- `src/Pegasus.Core/Assessment/AssessmentContracts.cs`
- `src/Pegasus.Core/Assessment/AssessmentPolicy.cs`
- `src/Pegasus.Infrastructure/Persistence/EfCaseAssessmentStore.cs`
- `src/Pegasus.Infrastructure/Persistence/Migrations/20260903110926_ExtendAssessmentVocabulary.cs` (new)
- `src/Pegasus.Infrastructure/Persistence/Migrations/20260903110926_ExtendAssessmentVocabulary.Designer.cs` (new)
- `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs`
- `src/Pegasus.Core/Reports/AssessmentReportProjection.cs`
- `src/Pegasus.Core/Reports/AssessmentReportRendering.cs`
- `src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs`
- `docs/design/assets/report-renderer/templates/assessment_report.scriban`
- `tests/Pegasus.Core.Tests/Assessment/AssessmentPolicyTests.cs`
- `tests/Pegasus.Core.Tests/Reports/AssessmentReportProjectionTests.cs`
- `tests/Pegasus.Core.Tests/Reports/AssessmentReportRenderingTests.cs`
- `tests/Pegasus.IntegrationTests/AssessmentPersistenceIntegrationTests.cs`
- `tests/Pegasus.IntegrationTests/AutomationAssessmentIngressTests.cs`
- `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs` (migration census)
- `tests/Pegasus.IntegrationTests/Reports/AssessmentReportRendererTests.cs`

No file outside the ticket's owned paths was touched. `AssessmentMcpTools.cs`,
Case Razor pages/partials, `OperatorLabels.cs`, damage-diagram assets,
valuation/estimate files, report-image curation files, D31 sign-off files,
and the governing FRDs were confirmed untouched.

## Commands and exit codes (this session's own runs)

- `dotnet restore ./Pegasus.slnx --locked-mode` — exit 0.
- `dotnet build ./Pegasus.slnx --configuration Release --no-restore` — exit 0
  (0 warnings, 0 errors).
- `dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build` — exit 0, 1200/1200 passed.
- `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build` — exit 0, 100/100 passed.
- `./scripts/Test-MigrationGrants.ps1` — exit 0, 88 migration files checked,
  every created table granted or exempted.
- `git diff --check origin/dev` — exit 0 (only CRLF/LF line-ending notices,
  no whitespace errors).

The canonical full-suite gate (`dotnet test ./Pegasus.slnx --configuration
Release --no-build --filter "Category!=Corpus"`, including the Browser-
category rendered-PDF assertion) was not re-run in this session per the
orchestrator's directive — GitHub CI runs it sharded on the PR, and it was
already run to green (Core 1200, Architecture 100, Integration 1227
passed/2 skipped, exit 0) during the inherited session and recorded in the
ticket plan's simplification-pass entry.

## Simplification pass

Already run and recorded in the ticket plan under "Simplification pass
(2026-09-03, gpt-5.6-sol low, reviewed by Claude Opus)": one efficiency
fix applied (an ordinal field-lookup replacing a per-field linear scan in
`AssessmentReportProjection.cs`); four other findings reported with no
change needed, each individually justified. No assertion was weakened.

## Deviations from the plan

None. The `git merge --no-edit origin/dev` refresh step was not needed:
the branch base (`07ac7f1b`) already matched `origin/dev` at the time of
this session's work, so DOCS-017 and PLAT-068 had not landed concurrently.

## PR

https://github.com/collisionengineers/pegasus/pull/648

Head SHA: `551959b94ff36a037b8eb27b9613cef03f21d2c5`
