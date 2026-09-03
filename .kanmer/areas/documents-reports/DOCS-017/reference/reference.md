# Review record — DOCS-017 (PR https://github.com/collisionengineers/pegasus/pull/651)

Reviewer family: Claude (Opus 5), dispositioning an independent
`gpt-5.6-terra` xhigh read. Built by Codex, so the other family reviews, per
the EPIC-012 model allocation.

Head SHA reviewed: `ddf739fbc210be1df08eaa0cf62580edb37f6c46`
(branch `task/docs-017-report-signatory`, target `dev`).
Review checkout: detached worktree `.worktrees/docs-017-review` at that SHA.
Date: 2026-09-03.

## Verdict

**Approved on content; merge withheld on a pre-existing red CI lane.**

The change matches the accepted plan and D31, every owned-path rule holds, and
every check that exercises this change is green (unit, browser,
sql-integration 1–3, sql-integration-coverage, test-ui, changes,
local-development-scripts, reference-data). The `documentation` lane is red,
but for a defect that already exists on `dev` and lies outside this ticket's
owned paths (finding 3). The merge decision is referred to the controller.

## What was reviewed

`git diff origin/dev...HEAD` — 14 files: two Core report files, one
Infrastructure persistence source, the Playwright renderer, the Scriban
template, `Pegasus.Infrastructure.csproj`, FRD-11, one Test UI snapshot, and
six test files. Read alongside the ticket body, `plan/plan.md` (including its
2026-09-03 Simplification pass and the nine-finding plan review),
`checklist/checklist.md`,
`post-implementation-report/post-implementation-report.md`,
`research/research.md`, `files/files.md` and EPIC-012 `context.md` (D29–D50).

Independently confirmed, not taken from the report:

- **Plan followed.** `ReportEngineer`, `AcceptedEngineers`,
  `TryResolveAcceptedEngineer` and every key/tuple match are gone;
  `ReportSignatory(PrintedName, Qualifications?, SignatureContent,
  SignatureContentType)` replaces them; `Prepare` gained the optional trailing
  `ReportSignatory?`; `AssessmentReportContract.TemplateVersion` is
  `rendererref1-v2` and `rendererref1-v1` still fails closed
  (`AssessmentReportRenderingTests.PreviousPayloadVersionFailsBeforeAdapter`).
- **One list per concept.** The signature media type is validated through
  `ReportImageEvidence.IsAcceptedContentType`, and
  `EfAssessmentReportProjectionSource` now calls that same Core method instead
  of its former private `HashSet` — the simplification pass's one applied
  finding, verified in the diff.
- **Core owns policy.** `grep -rn "ReportSignatory\|ReportEngineer" src/`
  shows tuple completeness, accepted media types and payload version live only
  in `Pegasus.Core/Reports`; Infrastructure carries no signatory rule. The
  single `Prepare` production caller is
  `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs:459`, unmodified and
  still compiling against the optional parameter.
- **Owned paths only.** No file outside the authorised table is touched;
  `AssessmentContracts.cs`, `AssessmentPolicy.cs`, `DependencyInjection.cs`,
  `OperatorLabels.cs`, FRD-01, FRD-04, `docs/design/README.md` and every Razor
  page are untouched. No package added (the `.csproj` change is the *removal*
  of the `andy_patterson.png` embedded resource). No migration, so
  `Test-MigrationGrants.ps1` correctly does not apply.
- **No explanatory copy.** The only operator-visible string change is the
  readiness requirement `Sign-off Engineer`, which
  `Pages/Cases/Assessment/Index.cshtml:98` renders verbatim exactly as it
  already renders `Current estimate required`. Keeping it in Core matches the
  existing convention rather than opening a second label list — the plan
  review's finding 8 rejection is sound.
- **Tests prove the claim, none weakened.** Every removed assertion asserted a
  contract that D31 deletes (the fixed Andy tuple, the mismatch rejection, the
  byte-for-byte embedded signature) and is replaced by an equal-or-stronger
  D31 assertion: `IncompleteSignatoryFailsBeforeAdapter` and
  `IncompleteSignOffEngineerIsNotReady` cover missing name, missing bytes and
  an unsupported `image/gif`; `NoSignatoryResourceIsEmbedded` asserts the
  stronger claim that *no* `brand.signatures` resource is embedded;
  `MissingQualificationsRenderTheSignatoryNameAlone` asserts the rendered PDF
  contains `Neil O'Reilly` and *not* `Neil O'Reilly —`;
  `AssessmentPersistenceIntegrationTests` proves the interim production source
  returns `Sign-off Engineer`. Fixture values are the mockup's own
  (`04-fixtures.js`), permitted by D43 — not fabricated domain data.
- **D44–D50 respected.** No review action, no damage type, no crop, EVA,
  vehicle-record or Create-Case behaviour is introduced.
- **Accepted interim regression is honest.**
  `EfAssessmentReportProjectionSource` passes `Signatory: null`, so no report
  draft can be generated on `dev` until CASE-040 and PLAT-068 land. This is
  recorded as an accepted risk in the plan and the report, is the only option
  compatible with conduct rule 6 (no D18 fallback), and EPIC-012 ships one
  production release after all PRs, so no released environment sees the
  interim state.

## Findings and dispositions

| # | Severity | Source | Finding | Disposition |
| --- | --- | --- | --- | --- |
| 1 | blocker (as raised) | terra xhigh | `AssessmentPolicy.EvaluatePostReviewReadiness` (`src/Pegasus.Core/Assessment/AssessmentPolicy.cs:250-255`) still requires the D18 `EngineerName`, `EngineerQualifications` and `EngineerSignature` assessment fields, and `Prepare` (`AssessmentReportProjection.cs:108`) seeds its reasons from that rail — so a complete D31 tuple alone does not make a report ready. | **Deferred to a linked ticket, [[ENG-038]]** (created by this review, linked from DOCS-017). Confirmed real by reading the code. It is not a DOCS-017 defect: `AssessmentPolicy.cs` and `AssessmentContracts.cs` are explicitly outside this ticket's owned paths, the plan and the plan review both recorded the retirement as an external follow-up, and the coupling is unchanged by this diff (those fields are still collected on the assessment form today, so no case is dead-ended). It must land before D31 is true end to end, which is why it is now a ticket rather than a note. |
| 2 | nit | terra xhigh | The plan's simplification record says "No test assertion was weakened or removed", while the diff removes D18 assertions. | **Rejected, with reason.** The sentence sits inside the "Simplification pass (2026-09-03)" section and describes that pass, which applied exactly one behaviour-preserving change (Infrastructure reusing `ReportImageEvidence.IsAcceptedContentType`) and removed no assertion. The D18 assertion removals belong to the implementation and are described in the post-implementation report. The record is accurate in its scope. |
| 3 | blocker (merge) | this review | The `documentation` CI lane fails: `Test-DocumentationLinks.ps1` reports `BROKEN .opencode/skills/kanmer-setup/SKILL.md: ../../../../docs/manual/greenfield.md`. | **Not this ticket's defect; merge referred to the controller.** Reproduced locally in the review checkout (exit 1). The link was introduced by `c5c7a874` ("chore(kanmer): add OpenCode skills…"), an ancestor of `origin/dev`; `docs/manual/` does not exist; `git diff --name-only origin/dev...HEAD -- .opencode/` is empty; and the same `documentation` job fails on the `dev` branch run at `9eec6dc2` for the same reason. `.opencode/**` is outside DOCS-017's owned paths, so this lane cannot be made green from this branch. |
| 4 | nit | this review | `tests/Pegasus.Core.Tests/Reports/AssessmentReportProjectionTests.cs:305` — the private `ReplaceField` helper is now dead: its only caller was the deleted `MismatchedEngineerSignatureIsNotReady`. | **Accepted risk / not blocking.** Test-only dead code, no behavioural effect, and it compiles clean at Release. Worth removing whenever ENG-035 or [[ENG-038]] next edits this file; not worth a round trip on a merge-ready branch. |
| 5 | nit | this review | `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` — the reworded sentence ending "…required value fails closed. No custom" is 82 columns, past the ~78-column Markdown convention. | **Accepted risk / not blocking.** The file already carries many 79–81 column prose lines and much longer table rows; the convention says "near 78", and the `documentation` lane does not enforce wrapping. |

Nothing else was found. Specifically checked and clear: the `Signatory is
null` guard in `AssessmentReportSnapshot.Validate` is harmless defence on a
non-nullable positional parameter; `byte[]` in `ReportSignatory` follows the
existing `ReportImageEvidence` convention rather than introducing a new one;
the Scriban `{{ if qualifications }}` guard is proven by the browser test, not
merely by inspection; the renderer's `ImageDataUri` extraction is the one
small extraction the plan authorised, with two real callers.

## Command exit codes

Run by this reviewer in the detached review checkout
`.worktrees/docs-017-review` at `ddf739fb`, Windows + PowerShell 7.

| Command | Exit |
| --- | ---: |
| `dotnet restore ./Pegasus.slnx --locked-mode` | `RESTORE_EXIT=0` |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | `BUILD_EXIT=0` |
| `dotnet test ./tests/Pegasus.Core.Tests/… --filter "FullyQualifiedName~Reports"` | `CORE_REPORTS_EXIT=0` (43 passed) |
| `dotnet test ./tests/Pegasus.ArchitectureTests/…` | `ARCH_EXIT=0` (100 passed) |
| `./scripts/Update-TestUiSnapshots.ps1 -Verify` | `TESTUI_VERIFY_EXIT=0` (120 integration tests passed in capture; snapshot verify clean) |
| `./scripts/Test-TestMarkdownPlacement.ps1` | `PLACEMENT_EXIT=0` |
| `./scripts/Test-DocumentationLinks.ps1` | `1` — finding 3, pre-existing on `dev` |
| `./scripts/Test-MigrationGrants.ps1` | not run — no migration in the diff |

**Why that scope covers the change.** `git diff --name-only origin/dev...HEAD`
names exactly two changed Core types (`AssessmentReportProjection`,
`AssessmentReportRendering`/`ReportSignatory`), one persistence source, one
renderer, one template and one FRD. `FullyQualifiedName~Reports` in
`Pegasus.Core.Tests` runs every test over both changed Core types, including
all the new signatory boundary cases; `Pegasus.ArchitectureTests` proves the
Core/Infrastructure dependency direction the new `ReportSignatory` contract
sits on; `Update-TestUiSnapshots.ps1 -Verify` is required because
`docs/design/test-ui/` changed, and its capture phase additionally ran the
whole 120-test `Pegasus.IntegrationTests` assembly green. The full
`Category!=Corpus` suite was not re-run locally by design — GitHub CI runs it
sharded on this PR (`unit`, `browser`, `sql-integration` 1–3,
`sql-integration-coverage`, `test-ui`) and all of those are green at this head
SHA.

## CI at `ddf739fb` (run 33791263169)

| Check | Result |
| --- | --- |
| changes | pass |
| local-development-scripts | pass |
| reference-data | pass |
| unit | pass |
| browser | pass |
| sql-integration (1) (2) (3) | pass |
| sql-integration-coverage | pass |
| test-ui | pass |
| infrastructure | skipping |
| documentation | **fail** — finding 3, pre-existing on `dev` |

Not merged by this review. `move_item` to `verifying` is deliberately not
performed while the PR is unmerged.
