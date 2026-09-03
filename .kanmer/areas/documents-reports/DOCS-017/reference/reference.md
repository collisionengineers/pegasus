# Review record — DOCS-017 (PR https://github.com/collisionengineers/pegasus/pull/651)

Reviewer family: Claude (Opus 5), dispositioning an independent
`gpt-5.6-terra` xhigh read run in a fresh detached checkout. Built by Codex,
so the other family reviews, per the EPIC-012 model allocation.

Head SHA reviewed: `ddf739fbc210be1df08eaa0cf62580edb37f6c46`
(branch `task/docs-017-report-signatory`, target `dev`).
Review checkout: detached worktree `.worktrees/docs-017-review` at that SHA,
clean (`git status --porcelain` empty) before and after.
Date: 2026-09-03. This is review round 2; it supersedes the round-1 record on
the same head SHA, whose findings it re-derived independently and confirms.

## Verdict

**Approved on content; merge withheld on a red required CI lane.**

The change matches the accepted plan and D31, every owned-path rule holds, and
every check that exercises this change is green (unit, browser,
sql-integration 1–3, sql-integration-coverage, test-ui, changes,
local-development-scripts, reference-data). The `documentation` lane is red
for a defect that already exists on `dev` and lies outside this ticket's owned
paths (finding 3). Per the controller's standing rule that a red CI check
blocks the merge, the merge decision is referred back rather than taken here.

## What was reviewed

`git diff origin/dev...HEAD` — 14 files: two Core report files, one
Infrastructure persistence source, the Playwright renderer, the Scriban
template, `Pegasus.Infrastructure.csproj`, FRD-11, one Test UI snapshot, and
six test files. Read alongside the ticket body, `plan/plan.md` (including its
2026-09-03 Simplification pass, the nine-finding plan review and the PR review
response), `checklist/checklist.md`,
`post-implementation-report/post-implementation-report.md`, and EPIC-012
`context.md` (D29–D50).

Independently confirmed in the review checkout, not taken from the report:

- **Plan followed.** `ReportEngineer`, `AcceptedEngineers`,
  `TryResolveAcceptedEngineer`, `SignatureKey` and every key/tuple match are
  gone (`grep` over `src/` and `tests/` returns no hit).
  `ReportSignatory(PrintedName, Qualifications?, SignatureContent,
  SignatureContentType)` replaces them; `Prepare` gained the optional trailing
  `ReportSignatory?`; `AssessmentReportContract.TemplateVersion` is
  `rendererref1-v2` and `rendererref1-v1` still fails closed
  (`AssessmentReportRenderingTests.PreviousPayloadVersionFailsBeforeAdapter`).
- **One list per concept.** The signature media type is validated through
  `ReportImageEvidence.IsAcceptedContentType`, and
  `EfAssessmentReportProjectionSource` now calls that same Core method instead
  of its former private `PhotoMediaTypes` `HashSet` — the simplification
  pass's one applied finding, verified in the diff.
- **Core owns policy.** Tuple completeness, the accepted media types and the
  payload version live only in `src/Pegasus.Core/Reports`; Infrastructure
  carries no signatory rule. The single `Prepare` production caller is
  `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs:459`, unmodified,
  still compiling against the optional parameter, and — because it supplies no
  signatory exactly as `EfAssessmentReportProjectionSource` does — still
  agreeing with what generating would decide, as its own comment requires.
  The regenerated snapshot
  (`docs/design/test-ui/pages/case-assessment--default.html:188`: "Report
  draft not ready: Sign-off Engineer; Current estimate required.") is the
  proof of that agreement.
- **Owned paths only.** No file outside the authorised table is touched.
  `AssessmentContracts.cs`, `AssessmentPolicy.cs`, `DependencyInjection.cs`,
  `OperatorLabels.cs`, FRD-01, FRD-04, `docs/design/README.md`, `.opencode/**`
  and every Razor page are untouched (`git diff origin/dev...HEAD --
  .opencode scripts` is empty). No package added: the `.csproj` change is only
  the *removal* of the `andy_patterson.png` embedded resource, and no
  `PackageReference` line changed. No migration, so
  `Test-MigrationGrants.ps1` correctly does not apply. `git diff --check` is
  clean.
- **No explanatory copy.** The only operator-visible string change is the
  readiness requirement `Sign-off Engineer`, which
  `Pages/Cases/Assessment/Index.cshtml:98` renders verbatim exactly as it
  already renders `Current estimate required`. Keeping it in Core matches the
  existing convention rather than opening a second label list — the plan
  review's finding-8 rejection is sound. The Scriban change only *suppresses*
  a separator; it adds no copy.
- **Tests prove the claim, none weakened.** Every removed assertion asserted a
  contract that D31 deletes (the fixed Andy tuple, the mismatch rejection, the
  byte-for-byte embedded signature) and is replaced by an equal-or-stronger
  D31 assertion: `IncompleteSignatoryFailsBeforeAdapter` and
  `IncompleteSignOffEngineerIsNotReady` cover missing name, missing bytes and
  an unsupported `image/gif`; `NoSignatoryResourceIsEmbedded` asserts the
  stronger claim that *no* `brand.signatures` resource is embedded;
  `MissingQualificationsRenderTheSignatoryNameAlone` asserts the rendered PDF
  contains `Neil O'Reilly` and *not* `Neil O'Reilly —`, and correctly carries
  `[Trait("Category", "Browser")]` so it runs in the browser lane;
  `AssessmentPersistenceIntegrationTests.ReportProjectionReadsPhotographsAndFailsClosedWithoutSignatory`
  proves the interim production source returns `Sign-off Engineer`.
- **Fixtures are not fabricated.** `Ed Mawdsley` / `ATA VDA AQP` and
  `Neil O'Reilly` with empty qualifications were read directly from the
  mockup's own `Pegasus_UI_v2_src/src/04-fixtures.js` (`DATA.staff` entries
  `s3` and `s4`) and are permitted verbatim by D43.
- **D44–D50 respected.** No review action or flag (D44), no damage type (D45),
  no crop (D46), no EVA state change (D47), no vehicle-record extension (D49)
  and no Create-Case route (D50) is introduced.
- **FRD-11 is clean of D18.** No `andy_patterson`, `M.Inst.IAEA`, exact-tuple,
  signature-key, embedded-"signature resource" or "issuing Engineer's
  identity" wording remains; the one surviving `D18` mention is the explicit
  supersession sentence. The wording matches EPIC-012 D31 as written.
- **Accepted interim regression is honest.**
  `EfAssessmentReportProjectionSource` passes `Signatory: null`, so no report
  draft can be generated on `dev` until CASE-040 and PLAT-068 land. This is
  recorded as an accepted risk in the plan and the report, is the only option
  compatible with conduct rule 6 (no D18 fallback), DOCS-017 `blocks` both
  dependencies so it must merge first, and EPIC-012 ships one production
  release after all PRs, so no released environment sees the interim state.
- **Simplification pass is honest.** Both findings are named, one applied and
  verified in the diff, one rejected with a stated reason (explicit
  `Signatory: null` documents the fail-closed boundary at each call site). No
  unapplied finding is silently dropped.

## Findings and dispositions

| # | Severity | Source | Finding | Disposition |
| --- | --- | --- | --- | --- |
| 1 | blocker (as raised) | terra xhigh | `AssessmentPolicy.EvaluatePostReviewReadiness` (`src/Pegasus.Core/Assessment/AssessmentPolicy.cs:250-255`) still requires the D18 `EngineerName`, `EngineerQualifications` and `EngineerSignature` assessment fields, and `Prepare` (`AssessmentReportProjection.cs:108`) seeds its reasons from that rail — so a complete D31 tuple alone does not yet make a report ready, and the new ready fixtures still carry all three legacy values. | **Deferred to the linked ticket [[ENG-038]]**, "Retire the D18 assessment signatory fields from post-Review report readiness" (already filed and linked to DOCS-017, CASE-040, PLAT-068 and ENG-035, with the sequencing constraint that it lands after the tuple is supplied). Confirmed real by reading `AssessmentPolicy.cs:250-255`. It is not a DOCS-017 defect: `AssessmentPolicy.cs` and `AssessmentContracts.cs` are explicitly outside this ticket's owned paths, and absorbing them would breach conduct rules 1 and 2. The coupling is unchanged by this diff — those fields are still collected on the assessment form, so no case is dead-ended by it. D31 is not true end to end until ENG-038 lands, which ENG-038 records. |
| 2 | blocker (merge) | this review | The required `documentation` CI lane fails: `Test-DocumentationLinks.ps1` reports `BROKEN .opencode/skills/kanmer-setup/SKILL.md: ../../../../docs/manual/greenfield.md`, `1 broken relative Markdown link(s)`. | **Not this ticket's defect; merge referred to the controller.** Proven pre-existing, not merely asserted: `git rev-parse origin/dev:.opencode/skills/kanmer-setup/SKILL.md` and `HEAD:...` both resolve to blob `0d8a66b5`, and `origin/dev:scripts/Test-DocumentationLinks.ps1` and `HEAD:...` both resolve to `e3944b91` — the failing input and the checker are byte-identical on both sides, so the lane fails identically on `dev`. `docs/manual/greenfield.md` does not exist on `origin/dev` either. `git diff origin/dev...HEAD -- .opencode scripts` is empty. PR #651's own job log (run 33791263169, job 100768149463) names exactly this one link and no file DOCS-017 touched. `.opencode/**` is outside every owned and supporting path, so the lane cannot be made green from this branch. [[PR-071]] is filed to add `.opencode` to the checker's existing vendored-tree exclusion list. |
| 3 | should-fix | this review | `docs/design/README.md:620` still states "Andy Patterson's approved exact tuple is embedded by Infrastructure", which this diff makes false — the embedded resource is removed and `NoSignatoryResourceIsEmbedded` asserts no `brand.signatures` resource exists. The plan and report named this as a follow-up "for the EPIC-012 docs lane", but no board ticket existed, so the follow-up would have been lost when DOCS-017 closes. | **Deferred to a linked ticket, [[DOCS-019]]**, created by this review and linked from DOCS-017. Correctly not fixed in this branch: `docs/design/README.md` is the design authority, is outside DOCS-017's owned doc paths, and is a capacity-one shared-lock path. |
| 4 | nit | terra xhigh | `tests/Pegasus.Core.Tests/Reports/AssessmentReportProjectionTests.cs:305` — the private `ReplaceField` helper is now dead: its only caller was the deleted `MismatchedEngineerSignatureIsNotReady`. Confirmed by grep (one hit, the declaration). | **Accepted, with reason.** Test-only dead code with no behavioural effect; it compiles clean at Release with 0 warnings. Not worth a remediation round trip on a ticket that `blocks` CASE-040 and PLAT-068. Recorded here so it is not silenced; ENG-035 and [[ENG-038]] both edit this file next and should remove it. |
| 5 | nit | this review | `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md:85` — the reworded sentence ending "…required value fails closed. No custom" is 84 columns, past the ~78-column Markdown convention. | **Accepted, with reason.** The convention says "near 78"; the same file already carries 79- and 86-column prose lines in untouched paragraphs (lines 86, 114, 132), and no lane enforces wrapping. Not worth a round trip; fold it in whenever FRD-11 is next edited. |

Nothing else was found. Specifically checked and clear: the `Signatory is
null` guard in `AssessmentReportSnapshot.Validate` is harmless defence on a
non-nullable positional parameter; `IsComplete` and `Validate` agree (the
latter delegates to the former); `Project` defensively copies the signature
bytes with `.ToArray()` before they enter the immutable snapshot; blank
qualifications become `null` in both Core and the renderer, and the Scriban
`{{ if qualifications }}` guard is proven by a browser test rather than by
inspection; `ImageDataUri` is the one small extraction the plan authorised and
has two real callers; the three other `.scriban` templates are inactive
reference surfaces and are untouched; `byte[]` on `ReportSignatory` follows
the existing `ReportImageEvidence` convention rather than introducing a new
one.

## Command exit codes

Run by this reviewer in the detached review checkout
`.worktrees/docs-017-review` at `ddf739fb`, Windows + PowerShell 7.

| Command | Exit |
| --- | ---: |
| `dotnet restore ./Pegasus.slnx --locked-mode` | `RESTORE_EXIT=0` |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | `BUILD_EXIT=0` (0 warnings, 0 errors) |
| `dotnet test ./tests/Pegasus.Core.Tests/… --filter "FullyQualifiedName~Reports"` | `CORE_REPORTS_EXIT=0` (43 passed) |
| `dotnet test ./tests/Pegasus.ArchitectureTests/…` | `ARCH_EXIT=0` (100 passed) |
| `./scripts/Update-TestUiSnapshots.ps1 -Verify` | `UIVERIFY_EXIT=0` (capture phase ran 296 non-browser `Pegasus.IntegrationTests` green; snapshot verify 1 passed) |
| `./scripts/Test-UiCatalogue.ps1` | `UICAT_EXIT=0` (54 routed sources, 58 prototypes, 0 broken local references) |
| `./scripts/Test-DocumentationLinks.ps1` (via the PR's own CI job log) | `1` — finding 2, pre-existing on `dev` |
| `./scripts/Test-MigrationGrants.ps1` | not run — no migration in the diff |

**Why that scope covers the change.** `git diff --name-only origin/dev...HEAD`
names exactly two changed Core types (`AssessmentReportProjection`,
`AssessmentReportRendering`/`ReportSignatory`), one persistence source, one
renderer, one template, one FRD and one snapshot.
`FullyQualifiedName~Reports` in `Pegasus.Core.Tests` runs every test over both
changed Core types, including all the new signatory boundary cases;
`Pegasus.ArchitectureTests` proves the Core/Infrastructure dependency
direction the new contract sits on; `Update-TestUiSnapshots.ps1 -Verify` is
required because `docs/design/test-ui/` changed, and its capture phase
additionally ran the whole 296-test non-browser `Pegasus.IntegrationTests`
assembly green — which covers the two changed integration test classes that
are not browser-tagged (`AssessmentPersistenceIntegrationTests`,
`AssessmentReportDraftWebTests`) plus `NoSignatoryResourceIsEmbedded`. The
full `Category!=Corpus` suite was not re-run locally by design: GitHub CI runs
it sharded on this PR (`unit`, `browser`, `sql-integration` 1–3,
`sql-integration-coverage`, `test-ui`) and all of those are green at this head
SHA, and the browser-tagged PDF assertions can only be proven there.

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
| documentation | **fail** — finding 2, pre-existing on `dev` |

Not merged by this review, and `move_item` to `verifying` is deliberately not
performed while the PR is unmerged. The one open question for the controller
is whether to merge over the known pre-existing red `documentation` lane, or
to hold DOCS-017 until [[PR-071]] lands on `dev` — noting that holding it also
holds CASE-040 and PLAT-068, which it `blocks`.
