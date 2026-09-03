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

## Review-finding disposition pass (2026-09-03)

PR #648 review returned two findings; both are dispositioned as reasoned
rejection/defer, not fixed on this branch. Full reasoning and evidence in
the ticket plan's "Review disposition (2026-09-03)" section.

1. `documentation` CI check red on a broken link in
   `.opencode/skills/kanmer-setup/SKILL.md:169` — confirmed pre-existing on
   `origin/dev` and outside ENG-035's owned paths; not fixed here. Filed as
   [[KANMER-011]], linked from ENG-035. Merge-timing recommendation recorded
   for the reviewer/merge authority: refresh after KANMER-011 lands rather
   than merge over the red check, though the final call is theirs, not this
   session's.
2. Confirmed culture-sensitive `DateOnly.TryParseExact` defect in
   `AssessmentPolicy.NormalizeValue` and `AssessmentReportProjection.ParseDate`
   — pre-existing on `dev`, widened (not introduced) by this ticket's four
   new `Date` paths; deferred to [[ENG-037]], already filed, linked, added to
   EPIC-012, and moved to the top of the `engineering-assessment` backlog.

No file in the branch's diff changed during this pass (`git status --short`
clean throughout); the branch head stays `551959b94ff36a037b8eb27b9613cef03f21d2c5`
— nothing to commit or push.

A Codex verification pass (`gpt-5.6-sol`, medium effort, per the standing
model-allocation convention) was attempted twice to independently confirm
the worktree remained unchanged, but Codex's backend returned `404 Not
Found` on both the WebSocket and HTTPS transports for every attempt
(`wss://chatgpt.com/backend-api/codex/responses`), including a bare
read-only "PONG" probe against the same model — confirming an external
service outage unrelated to this ticket, not a task-specific failure. Since
neither finding required any ENG-035 code change, this session performed the
equivalent verification directly instead: `git log --oneline
origin/dev..HEAD -- .opencode/skills/kanmer-setup/SKILL.md` (empty) and
`git merge-base --is-ancestor c5c7a874 origin/dev` (true) for finding 1, and
a direct `grep -n "TryParseExact"` read of both flagged call sites for
finding 2 (unchanged, matching the finding's own citation).

Re-ran the delivery commands against the unchanged tree:

- `dotnet restore ./Pegasus.slnx --locked-mode` — exit 0.
- `dotnet build ./Pegasus.slnx --configuration Release --no-restore` — exit 0
  (0 warnings, 0 errors).
- `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter
  "Category!=Corpus"` — Core.Tests 1200/1200 and ArchitectureTests 100/100
  passed early in the run; the full run (including the longer
  sql-integration and Browser-category assertions) was still executing
  against the LocalDB/Playwright harness when this report was appended. This
  is a redundant local confirmation, not new risk: PR #648's own CI already
  ran this identical filter against this identical head SHA
  (`551959b9`) and every check passed except `documentation`
  (the check this pass is dispositioning) — see the GitHub Checks tab on
  PR #648 for the authoritative sharded run (unit, sql-integration ×3,
  sql-integration-coverage, browser, test-ui all green,
  completed 2026-09-03T13:13–13:42Z).

## PR

https://github.com/collisionengineers/pegasus/pull/648 (head unchanged:
`551959b94ff36a037b8eb27b9613cef03f21d2c5`)
