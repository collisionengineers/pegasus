# Post-implementation report — DOCS-017 (2026-09-03)

Branch `task/docs-017-report-signatory`, worktree `.worktrees/docs-017`,
head `ddf739fbc210be1df08eaa0cf62580edb37f6c46`. PR:
https://github.com/collisionengineers/pegasus/pull/651 (target `dev`).

Resumed lane: this execution resumed an interrupted run of the same ticket
(claim had expired; resumed with `take_ticket`/`get_execution_packet resume`
against the exact recorded branch and worktree). All implementation work
(Codex `gpt-5.6-sol` medium) was already complete and uncommitted in the
worktree when this run started; it was reviewed against the plan and
checklist, kept as correct, then committed. Only the simplification pass and
delivery steps (commit, push, PR, report, gates) were newly performed.

## What changed

- `src/Pegasus.Core/Reports/AssessmentReportRendering.cs`,
  `src/Pegasus.Core/Reports/AssessmentReportProjection.cs` — replaced the D18
  fixed accepted-signatory dictionary/key with a supplied
  `ReportSignatory(PrintedName, Qualifications?, SignatureContent,
  SignatureContentType)` snapshot tuple; `Prepare` names the `Sign-off
  Engineer` readiness item when absent/incomplete; `Project` no longer reads
  assessment `engineer.*` fields; `AssessmentReportContract.TemplateVersion`
  bumped to `rendererref1-v2`.
- `src/Pegasus.Infrastructure/Persistence/EfAssessmentReportProjectionSource.cs`
  — deliberately supplies no signatory until CASE-040/PLAT-068 land; reuses
  Core's `ReportImageEvidence.IsAcceptedContentType` instead of a second
  media-type list (simplification-pass finding, applied).
- `src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs`,
  `src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj` — one shared
  `ImageDataUri` helper renders both photos and the supplied signature;
  removed the embedded Andy signature resource registration.
- `docs/design/assets/report-renderer/templates/assessment_report.scriban` —
  qualifications separator emitted only when qualifications are present.
- `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` —
  reconciled D18-era signatory/readiness wording with D31.
- `docs/design/test-ui/pages/case-assessment--default.html` — regenerated for
  the new `Sign-off Engineer` readiness sentence (only retained diff).
- Six test files (`tests/Pegasus.Core.Tests/Reports/*`,
  `tests/Pegasus.IntegrationTests/AssessmentPersistenceIntegrationTests.cs`,
  `tests/Pegasus.IntegrationTests/Browser/AssessmentReadinessSummaryBrowserTests.cs`,
  `tests/Pegasus.IntegrationTests/Reports/AssessmentReportDraftWebTests.cs`,
  `tests/Pegasus.IntegrationTests/Reports/AssessmentReportRendererTests.cs`) —
  Ed/Neil rendering, boundary/rejection cases, `rendererref1-v1` rejection,
  and the interim production not-ready result.

All changes are inside DOCS-017's owned paths and the supporting files named
in `files/files.md`; `git status --porcelain` after the final commit is
clean, and no file outside the authorized table was touched.

## Proof allocation (ticket verification bullets)

- "A report for a Case whose sign-off is Ed renders Ed's tuple; missing
  qualifications print the name alone" — proven here at the snapshot/renderer
  level with a supplied tuple (Ed with qualifications, Neil without).
- "An unflagged Engineer cannot be chosen as sign-off" — **not proven by this
  ticket.** Selection/eligibility is entirely [[CASE-040]] / [[PLAT-068]]
  behaviour; DOCS-017 owns no selection code.

## Accepted risk

Between this PR's merge and the CASE-040 + PLAT-068 merges, no report draft
can be generated on `dev`: the sole production projection source supplies no
signatory, so every draft attempt returns the `Sign-off Engineer` readiness
item rather than a PDF. This is deliberate (conduct rule 6 forbids retaining
the D18 fixed tuple as a fallback); DOCS-017 `blocks` both dependencies so it
merges first, and EPIC-012 ships one production release after all PRs, so no
released environment sees the interim state. Full reasoning is in
`plan.md` § "Verified basis and boundary" and the plan-review disposition for
finding 2.

## Deviations from the plan

None in scope or design. The plan's own "Simplification pass (2026-09-02)"
placeholder heading was superseded by the real 2026-09-03 record (below); no
other deviation.

## Simplification pass (2026-09-03)

Recorded in full in `plan.md`. Summary: Sonnet-wrapped Codex (`gpt-5.6-sol`,
low effort) reviewed the branch diff against `origin/dev` across four lenses.
One finding applied (Infrastructure reuses Core's
`ReportImageEvidence.IsAcceptedContentType` instead of a duplicate media-type
`HashSet`); one finding reviewed and rejected with a recorded reason
(explicit `Signatory: null` at interim call sites documents the fail-closed
boundary deliberately). No correctness finding, no weakened assertion.

## Commands and exit codes

Run directly in the task worktree by this session (not from Codex's
self-report), after both the main implementation and the simplification
pass's one applied change:

| Command | Exit |
| --- | ---: |
| `dotnet restore ./Pegasus.slnx --locked-mode` | 0 |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | 0 |
| `dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build` | 0 (1,191 passed) |
| `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build` | 0 (100 passed) |
| `./scripts/Update-TestUiSnapshots.ps1` | 0 |
| `./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture` | 0 |
| `./scripts/Test-UiCatalogue.ps1` | 0 (54 routes, 58 prototypes, 0 broken references) |

Re-run after the simplification pass's applied change:

| Command | Exit |
| --- | ---: |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | 0 |
| `dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build` | 0 (1,191 passed) |
| `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build` | 0 (100 passed) |

The full-solution `Category!=Corpus` filter (including
`Pegasus.IntegrationTests`, ~26 minutes) and a direct
`Pegasus.IntegrationTests` run were **not** run locally, per this project's
standing instruction that GitHub CI runs that suite sharded on the PR and the
reviewer blocks the merge on it. `AssessmentReportRendererTests` carries
`[Trait("Category", "Browser")]`; its PDF-text assertions and the browser
readiness fixture are proven by CI, not by this local run.

No migration exists in this ticket, so `Test-MigrationGrants.ps1` was not
run.

## Named external follow-ups (not this ticket's scope)

- [[CASE-040]] and [[PLAT-068]]: persist/select the Case sign-off Engineer,
  enforce eligibility and the D31 default, load the account tuple, and pass
  it to both readiness and production projection.
- ENG-035 / assessment signatory vocabulary retirement: inventory remaining
  consumers of `AssessmentVocabulary.Engineer*` and remove them.
- EPIC-012 documentation lane: replace the stale embedded-Andy statement in
  `docs/design/README.md` (~line 620).

## PR

https://github.com/collisionengineers/pegasus/pull/651
