# Review record — ENG-035 (PR https://github.com/collisionengineers/pegasus/pull/648)

Reviewer family: Claude (Opus) with gpt-5.6-terra xhigh as the independent
reader. The PR was built by Codex, so the reading family differs from the
implementing family.

Head SHA reviewed: `551959b94ff36a037b8eb27b9613cef03f21d2c5`
Base: `origin/dev` (`07ac7f1b`). Review checkout:
`.worktrees/eng-035-review` (detached at the PR head).

## Verdict

**Code review: APPROVE.** No blocker survives dispositioning against the
code. The diff matches the plan's pinned vocabulary table exactly, stays
inside the owned paths, keeps every policy decision in `Pegasus.Core`, and
proves each acceptance claim with a named test.

**Merge: BLOCKED on a red required-lane check that ENG-035 neither caused
nor owns.** The `documentation` job of `repository-check` fails on a broken
relative Markdown link in `.opencode/skills/kanmer-setup/SKILL.md:169`
(`../../../../docs/manual/greenfield.md`). It is pre-existing on `origin/dev`
and outside this ticket's owned paths, so this reviewer will not merge over
it without an explicit merge-authority decision. See "Merge decision
required" below.

## What was reviewed

Ticket body, `plan/plan.md` (including its plan-review and Simplification
pass sections), `checklist/checklist.md`,
`post-implementation-report/post-implementation-report.md`,
`open-questions/open-questions.md`, and EPIC-012 `context.md` (D29–D50).

`git diff --name-only origin/dev...HEAD` returns 17 files, every one inside
the plan's Expected-files table. `AssessmentMcpTools.cs`, Case Razor pages and
partials, `OperatorLabels.cs`, estimate/valuation files, report-image
curation, D31 sign-off files, the governing FRDs, `docs/operator-notes.md`
and `corpus/` are all untouched.

`tests/Pegasus.IntegrationTests/Reports/AssessmentReportDraftWebTests.cs`
appears in the plan's Expected files but not in the diff. Verified benign:
it does not construct `AssessmentReportSnapshot` positionally, so the two new
members required no edit there. The plan over-predicted; the build proves it.

## Independent verification (this review checkout, at the PR head)

The full suite was deliberately not re-run locally — GitHub CI runs the
canonical `Category!=Corpus` filter sharded on this exact head SHA, and the
merge is gated on it below. Locally I ran the build rails plus every test
project that owns a changed type, and the migration gate.

| Command | Exit |
| --- | --- |
| `dotnet restore ./Pegasus.slnx --locked-mode` | `RESTORE_EXIT=0` |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | `BUILD_EXIT=0` |
| `dotnet test ./tests/Pegasus.Core.Tests/... --configuration Release --no-build` | `CORETEST_EXIT=0` — 1200/1200 passed, 0 failed |
| `dotnet test ./tests/Pegasus.ArchitectureTests/... --configuration Release --no-build` | `ARCHTEST_EXIT=0` — 100/100 passed |
| `./scripts/Test-MigrationGrants.ps1` | `GRANTS_EXIT=0` |

Why that scope covers the change. Every changed production type is either a
Core type (`AssessmentVocabulary`, `AssessmentPolicy`,
`AssessmentReportProjection`, `AssessmentReportContract`, `ReportVehicle`,
`ReportDamage`, `ReportSettlement`, `AssessmentReportPresentation`) — covered
by `Pegasus.Core.Tests`, which carries the new vocabulary, normalizer,
derivation, save-bound, equity, payload-version and D45-member assertions —
or an Infrastructure type whose contract is enforced by
`Pegasus.ArchitectureTests` (Core-owns-policy, dependency direction) and by
the migration-grant script (the one new migration). The SQL-Server and
Browser-category proofs (`AssessmentPersistenceIntegrationTests`,
`AutomationAssessmentIngressTests`, `IntakePersistenceIntegrationTests`
census, `AssessmentReportRendererTests` PDF text) need LocalDB and Playwright
and are proven by CI on this same SHA: `unit`, `sql-integration (1..3)`,
`sql-integration-coverage`, `browser` and `test-ui` all conclude SUCCESS.
`./scripts/Update-TestUiSnapshots.ps1 -Verify` was not run — no routed Razor
page changed and `docs/design/test-ui/**` is not in the diff.

CI on `551959b9`: 11 of 12 checks SUCCESS, `infrastructure` SKIPPED,
`documentation` FAILURE (finding 4). `mergeable: MERGEABLE`,
`mergeStateStatus: UNSTABLE`.

## Findings and dispositions

| # | Sev | Finding | Disposition |
| --- | --- | --- | --- |
| 1 | blocker (claimed) | `AssessmentPolicy.cs:377-381` — a whitespace-only `damage.impacts` value is turned into `null` by `NormalizeValue` before the JSON normalizer runs, so `"   "` clears the impacts and both derived rows instead of failing closed. | **Rejected with reason.** The blank-is-a-clear rule is `NormalizeValue`'s first statement and has always applied to *every* field type in the vocabulary — Text, Enumerated, Money, WholeNumber, Date and now Json alike. Special-casing Json would create a second clearing convention for one path ("the existing convention wins"; one list per concept). Nothing fails open: malformed JSON that is not blank still throws — proven by `DamageImpactsFailClosed` over `not-json`, `{}`, a missing member, an unknown zone, an unknown severity, an extra member and a duplicate zone. Clearing is itself a legitimate, lease-held, reason-carrying operation whose derived-row cleanup is proven by `DamageImpactsPersistAndClearTheirCoreDerivedHeadlineRows`, so a blank submission reaches exactly the state an explicit `null` reaches — no data is lost that an authorised clear would not also remove. |
| 2 | nit (regraded from blocker) | Codex asked for extra `damage.impacts` cases: a `null` array element, a nested array, a non-string member. | **Accept risk / no change.** All three are already rejected by the same `element.ValueKind != JsonValueKind.Object \|\| … ValueKind != JsonValueKind.String` guard in `ReadImpacts` (`AssessmentPolicy.cs:502-517`), and that guard's branch is exercised by the existing `DamageImpactsFailClosed` rows. The gap is test breadth over one already-covered branch, not behaviour; it does not justify another round trip. |
| 3 | blocker (claimed) | `AssessmentPolicy.cs:460` and `AssessmentReportProjection.cs` `ParseDate` use the culture-sensitive `DateOnly.TryParseExact(value, "yyyy-MM-dd", out _)` overload, so a canonical date can misparse under a non-Gregorian default calendar; ENG-035's four new `Date` paths widen the blast radius. | **Deferred — disposition upheld.** Confirmed genuine and confirmed pre-existing: neither call site appears as a changed line in `git diff origin/dev...HEAD`, and both exist verbatim on `origin/dev`. ENG-035 widens the exposure but did not introduce it, and neither call site is a step in this ticket's plan. Already filed as [[ENG-037]] ("Parse assessment and report dates with the invariant culture"), linked from ENG-035, added to EPIC-012 and placed at the top of the `engineering-assessment` backlog. The implementer's defer is honest and correctly recorded; this reviewer endorses it rather than re-opening scope. |
| 4 | blocker (claimed) | `PlaywrightAssessmentReportRenderer.cs:176` still applies Infrastructure's generic `Display()` title-caser to `snapshot.ImpactSeverity` and `snapshot.ImpactLocation`, contrary to "report code display text lives in Core". | **Rejected with reason.** The cited line is unchanged context, not a changed line: it appears with a leading space in `git diff origin/dev...HEAD` and is byte-identical on `origin/dev` (`PlaywrightAssessmentReportRenderer.cs:163` there). It also operates on an unchanged code set — `ImpactLocation`'s accepted codes are now derived as `DamageZones.Values.Select(z => z.ImpactLocation).Distinct()` plus `multiple`, which yields exactly the fourteen codes the previous literal list held, so no new code reaches `Display()` and no `Wheel Rf` can be produced (the four wheel zones all derive the existing `wheel`). The plan's actual requirement — that the *new* zone and severity codes not be left to title-casing — is met: every per-impact row takes its text from Core via `AssessmentReportPresentation.DamageZone` / `DamageSeverity`. Rewriting a pre-existing line is out of scope (rule 1). |
| 5 | nit (this reviewer) | Core's new `AssessmentReportPresentation.AssessmentCode` and Infrastructure's existing `Display()` are two title-casing helpers for assessment codes. | **Accept risk.** They cover disjoint code sets (`AssessmentCode` the new enumerations with their explicit overrides `OK`, `CVT`, `Semi-automatic`, `Repair kit`, `Not fitted`; `Display` the pre-existing scalars), and merging them means editing the pre-existing helper and its callers — scope this ticket does not own. Worth folding into ENG-037's neighbourhood or a later report-presentation ticket; not worth a round trip now. |
| 6 | blocker | The `documentation` job of `repository-check` is FAILURE on this head: `Test-DocumentationLinks.ps1` reports "1 broken relative Markdown link(s)" for `.opencode/skills/kanmer-setup/SKILL.md:169` → `../../../../docs/manual/greenfield.md`. | **Upheld as a merge blocker, not as an ENG-035 defect.** Independently confirmed pre-existing and unrelated: `git log --oneline origin/dev..551959b9 -- .opencode/` is empty (no commit on this branch touches the directory), and the identical link is present on `origin/dev`. It is outside ENG-035's owned paths, so the implementer correctly did not fix it and filed [[KANMER-011]]. The reviewer's own rail is nevertheless "a red CI check blocks the merge", and this reviewer is not the merge authority for a bypass. |

## Answers to the review questions

- **Every new field has a named production caller.** Yes. Each new path is
  admitted by `AssessmentPolicy.ValidateAndNormalize`, persisted by
  `EfCaseAssessmentStore.SaveAsync` behind `ISaveAssessment`, reachable over
  the generic `pegasus_assessment_update` MCP tool (proven end-to-end by
  `AutomationAssessmentIngressTests`, which now round-trips `vehicle.colour`
  and `damage.impacts` through the real HTTP tool call), and projected into
  the report by `AssessmentReportProjection.Project`, whose production caller
  is `GenerateAssessmentReportDraft`. No stub, TODO, mock or test-only path.
- **No explanatory copy.** The template gains three section headings
  (`Damage`, `Tyres and Seat Belts`) and label/value rows only. No hints, no
  how-it-works prose, no empty-state panel — an absent value renders `—`.
- **Labels.** `OperatorLabels.cs` is untouched. Report display text for the
  new codes lives in Core beside `AssessmentReportPresentation`. Infrastructure
  gained no label catalogue; the renderer only formats what the snapshot
  carries. The zone and severity code lists exist exactly once, in
  `AssessmentVocabulary.DamageZones` / `DamageSeverities`, and the vocabulary
  definitions for `impact_severity` and `impact_location` are now *derived
  from* those dictionaries rather than restated.
- **Core owns policy.** All JSON parsing, member validation, zone/severity
  admission, uniqueness, the note bound, canonical re-serialization, the
  derivation rules and the D41 equity calculation are in
  `Pegasus.Core`. Infrastructure asks Core for derived values and writes them
  through the existing writer; the Scriban template does no arithmetic and no
  parsing. `Pegasus.ArchitectureTests` 100/100 green.
- **Equity.** `engineerValue - (costs.Total - betterment) - salvage`, with
  excess kept as its own field and excluded. Checked against the binding
  mockup source `Pegasus_UI_v2_src/src/05-state.js:124-129`, where
  `repairCost = totals.total` (VAT-inclusive) and
  `equity = engineerValue - (repairCost - betterment) - salvage` — the
  implementation matches, including the VAT-inclusive reading of "repair
  cost". Proven by `EquitySubtractsRepairAfterBettermentAndSalvageButNotExcess`.
- **D45.** No damage `type` appears in the contract, the normalizer, the
  projection, the snapshot, the template or any fixture.
  `ExpandedSnapshotUsesVersionTwoAndImpactHasOnlyD45Members` asserts
  `ReportImpact` has exactly `Zone`, `Severity`, `Note`, and the report table
  has exactly those three columns.
- **D44 / D46 / D50.** No review flag, checkbox, dialog or history line; no
  crop or image-curation change; no case-creation change. Confirmed by the
  file list.
- **D49 coordination.** No field is retired from the Assessment vocabulary
  and re-homed on the case vehicle record, so no production edit route is
  stranded ahead of [[CASE-043]]. The diff is purely additive to the
  vocabulary; the only removal is the *direct writability* of the two derived
  impact scalars, which is the ticket's own intended behaviour and is
  replaced by derivation from `damage.impacts`.
- **Tests prove the claim; nothing weakened.** Two existing assertions
  changed, both legitimately and both strengthened rather than loosened:
  `UnknownPathRejected` moved its probe from `vehicle.colour` to
  `vehicle.not_a_field` because `vehicle.colour` is now a real path, and
  `EveryEnumeratedCodeFromTheScreenRoundTrips` now skips the two derived
  paths — because they can no longer be written at all, a stronger rule
  proven by `DerivedImpactFieldsCannotBeWrittenDirectly`. Coverage added for:
  canonical round trip of every writable path
  (`EveryWritableVocabularyPathRoundTripsThroughItsCoreNormalizer`), canonical
  re-serialization with member reordering and note trimming, multi-zone
  `multiple` and wheel-zone `wheel` derivation, highest-severity derivation,
  seven fail-closed impact shapes, the 200-character note bound and the
  4000-character serialized bound, direct derived-write rejection at both the
  Core and the live MCP boundary, persistence and clear of derived rows with
  Automation provenance, the raised save bound
  (`SaveBoundCoversTheWholeVocabulary`: 76 definitions ≤ 80), every new
  projection field, equity, the bumped payload version, and representative
  PDF text.
- **Migration.** One migration, drop-and-re-add of
  `CK_CaseAssessmentFields_FieldPath` only. No table, entity, `DbSet`,
  package or backfill, so no new grant is required —
  `Test-MigrationGrants.ps1` exits 0 over all 88 migration files. The model
  snapshot matches the migration's constraint text exactly, and the migration
  name is appended to the applied-migration census in
  `IntakePersistenceIntegrationTests.cs`. `Down` re-adds the narrower
  constraint, which SQL Server refuses transactionally if any new-path row
  exists — evidence is never deleted, as the plan requires.
- **Simplification pass.** The single applied fix is present in the diff:
  `AssessmentReportProjection.Project` now builds one ordinal
  `Dictionary<string,string?>` and every read goes through it instead of
  `CaseAssessmentProjection.Field`'s linear scan. Behaviour-preserving —
  `TryAdd` keeps the first occurrence for a duplicated path, which is what
  `SingleOrDefault`-style first-match reading returned before. The four
  "no change needed" claims were spot-checked and hold; the reported-but-
  unapplied double-parse finding (#2 in the plan's table) is named with a
  reason rather than silently dropped, which is an honest disposition.

## Merge decision required

This is a controller/merge-authority call, not the reviewer's, and it is the
only thing standing between this PR and `dev`:

- **(a) Land [[KANMER-011]] first** (a one-line link repair, no coupling to
  ENG-035), then `git merge --no-edit origin/dev` on this branch, let CI go
  fully green, and merge. This is the recommended path and matches the
  implementer's own recommendation.
- **(b) Merge over the red `documentation` check.** Defensible on the
  evidence — the failure is provably pre-existing on `origin/dev`, provably
  untouched by this diff, and GitHub reports the PR `MERGEABLE` with
  `mergeStateStatus: UNSTABLE` (the check is not branch-protection-required).
  It needs an explicit instruction; this reviewer will not take it unilaterally.

Until one of those happens ENG-035 stays in Review at
`551959b94ff36a037b8eb27b9613cef03f21d2c5`. Nothing in the ticket's own code
is asked to change.
