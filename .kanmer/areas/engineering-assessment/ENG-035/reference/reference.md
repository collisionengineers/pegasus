# Review record — ENG-035 (PR https://github.com/collisionengineers/pegasus/pull/648)

- Reviewer family: Claude (Opus), independent of the implementer (codex).
- Head SHA reviewed: `551959b94ff36a037b8eb27b9613cef03f21d2c5`
- Branch: `task/eng-035-assessment-vocabulary`; base `origin/dev` @ `07ac7f1b`
- Review checkout: `.worktrees/eng-035-review` (detached)
- Second reader: gpt-5.6-terra, `model_reasoning_effort=xhigh`, run over the
  same checkout with the plan, checklist, D-decisions and owned-path list.
- Date: 2026-09-03

## Verdict

**Approved on content. Merge blocked** by a red `documentation` CI check that
is pre-existing on `origin/dev` and outside this ticket's owned paths (see
finding 4). No change is required of ENG-035.

## Findings and dispositions

| # | Sev | Finding | Disposition |
| --- | --- | --- | --- |
| 1 | blocker (claimed) | `AssessmentPolicy.cs` — a whitespace-only `damage.impacts` clears the field instead of failing closed as malformed JSON, taking both derived rows with it. | **Rejected.** `NormalizeValue` (`AssessmentPolicy.cs:442-446`) has always treated a blank value as a clear for *every* field type; `Json` is not special-cased, so a blank clears `damage.impacts` exactly as a blank clears a Money or Date path. That is the vocabulary's single clear convention (one list per concept), and the intended clear path is proven by `AssessmentPersistenceIntegrationTests.DamageImpactsPersistAndClearTheirCoreDerivedHeadlineRows`, which asserts both derived rows are removed. Special-casing `Json` would introduce a second clear semantic. |
| 1b | nit | Null array elements, nested arrays and non-string members have no explicit `InlineData` row. | **Accepted risk.** All three land on the single `element.ValueKind != JsonValueKind.Object` / member-kind guard in `ReadImpacts` (`AssessmentPolicy.cs:512-524`), which is already exercised by the `{}`, missing-member, extra-member and unknown-code rows of `DamageImpactsFailClosed`. Additional rows would add coverage of the same branch. |
| 2 | blocker (claimed) | `DateOnly.TryParseExact(value, "yyyy-MM-dd", out var date)` resolves against the current culture; under `th-TH` `2027-01-02` parses as Gregorian 1484. Same overload in `AssessmentReportProjection.ParseDate`. | **Confirmed, deferred to [[ENG-037]].** The defect is real but **pre-existing on `origin/dev`**: `git show origin/dev:…AssessmentPolicy.cs` carries the same line (unchanged context in this diff), and `origin/dev`'s `AssessmentReportProjection.cs:292` carries the other. ENG-035 adds four `Date` paths that inherit it but introduces no new bug. Fixing it here is scope creep (rule 1). |
| 3 | blocker (claimed) | `PlaywrightAssessmentReportRenderer.cs:46,176` still title-cases impact severity/location through Infrastructure's `Display()`, against the plan's Core-owned-display-text requirement. | **Rejected — not evidenced.** Line 176 is the pre-existing, unchanged `Impact Magnitude` row, rendering the *headline* codes whose closed list is unchanged (`ImpactLocation`'s codes are `DamageZones.Values.Select(ImpactLocation).Distinct()` + `multiple`, reproducing the previous list exactly). All four new `wheel_*` zone codes derive to the existing `wheel` code (`AssessmentContracts.cs:120-124`), so no new code ever reaches `Display()` and no `Wheel Rf` can be produced. Line 46 is `assessment["impact_rows"] = ImpactRows(snapshot.Damage.Impacts)`, which consumes Core display text produced by `AssessmentReportPresentation.DamageZone`/`DamageSeverity` in `AssessmentReportProjection.BuildDamage` — exactly what the plan required. |
| 4 | blocker | The PR's `documentation` check is red: `BROKEN .opencode/skills/kanmer-setup/SKILL.md: ../../../../docs/manual/greenfield.md` — 1 broken relative Markdown link. | **Confirmed; outside scope, merge blocked.** Verified pre-existing at `origin/dev` (`git show 07ac7f1b:.opencode/skills/kanmer-setup/SKILL.md:169`); `git diff --name-only origin/dev...HEAD` touches no `.opencode/` file. `.opencode/skills/` is a Kanmer-managed skill tree, not an ENG-035 owned path, and rule 1 forbids repairing it in this PR. Reported to the controller for a base-branch fix or an explicit merge decision. |
| 5 | nit | The canonical-length check in `NormalizeImpacts` (`AssessmentPolicy.cs:487-491`) is unreachable — `NormalizeValue` already rejects a raw value over 4000, and the canonical form is never longer than its input (same keys and values, `note` only trimmed). `DamageImpactNoteAndSerializedValueBoundsFailClosed`'s 4003-character case is caught by the generic bound, not by this check. | **Accepted risk.** Harmless defensive guard; removing it is a behaviour-preserving tidy with no benefit worth another round. |
| 6 | nit | `AssessmentReportPresentation.AssessmentCode`'s fallback duplicates the `en-GB` title-casing expression in `PlaywrightAssessmentReportRenderer.Display()`. | **Accepted risk.** A duplicated two-line formatting expression, not a duplicated list or business rule; both layers are permitted to format, and Core owns every code list. |
| 7 | nit | `PlaywrightAssessmentReportRenderer.RestraintRows` uses `damage.SpareTyre ?? "—"`, but `AssessmentCode(null)` already returns `"—"`, so the coalesce is dead. | **Accepted risk.** Dead but harmless; consistent with the surrounding row helpers. |

## What I verified independently

- **Owned paths only.** The 17 changed files match the plan's Expected-files
  table exactly. `AssessmentMcpTools.cs`, every Case Razor page and partial,
  `OperatorLabels.cs`, the damage-diagram assets, valuation/estimate files,
  report-image curation, D31 sign-off files, the governing FRDs and
  `docs/operator-notes.md` are all untouched.
- **D45.** No `type` member appears in `AssessmentImpact`, `ReportImpact`, the
  normalizer, the Scriban table (`<th>Zone</th><th>Severity</th><th>Note</th>`)
  or any fixture. `AssessmentReportRenderingTests.ExpandedSnapshotUsesVersionTwoAndImpactHasOnlyD45Members`
  asserts it by reflection.
- **D44 / D46 / D47 / D50.** No review state, flag, checkbox, dialog, history
  line or gate; no crop or image-curation change; no case-state transition; no
  Create-Case route.
- **D49 — no stranded edit route.** Nothing is retired from the vocabulary.
  `assessment.impact_location` / `assessment.impact_severity` become
  Core-derived-only, which removes a *write* route; `grep` over `src/` finds no
  Razor page, page model or Web source that ever wrote either path (only the
  compiled binaries match), so no production edit route was lost. The generic
  `pegasus_assessment_update` MCP route remains the production caller and now
  carries `damage.impacts` instead — proven end-to-end by
  `AutomationAssessmentIngressTests`.
- **Core owns policy.** Vocabulary, zone/severity catalogues, the JSON
  normalizer, the derivation rules and the D41 equity calculation all live in
  `Pegasus.Core`. Infrastructure only persists Core-derived rows
  (`EfCaseAssessmentStore.cs:155-170`) and formats snapshot values; the Scriban
  template carries no arithmetic and no parsing. 100/100 architecture tests pass.
- **Equity.** `engineerValue - (costs.Total - (betterment ?? 0)) - (salvage ?? 0)`
  matches the plan, and `EquitySubtractsRepairAfterBettermentAndSalvageButNotExcess`
  asserts excess is excluded.
- **Save bound.** 76 definitions, `MaximumFieldsPerSave` raised 60 → 80, and
  `SaveBoundCoversTheWholeVocabulary` asserts the invariant rather than the
  literal.
- **Migration.** A pure `CK_CaseAssessmentFields_FieldPath` swap — no table,
  entity, `DbSet`, package or backfill, so no new grant is owed; the `Down`
  re-adds the prior constraint and fails transactionally rather than deleting
  rows. The generated name is appended to the exact census in
  `IntakePersistenceIntegrationTests.cs`.
- **No weakened assertions.** The one edited existing test swapped
  `vehicle.colour` for `vehicle.not_a_field` as the unknown-path probe, because
  `vehicle.colour` is now a real path — the assertion is unchanged in strength.
  `EveryEnumeratedCodeFromTheScreenRoundTrips` excludes the two derived paths,
  which is required now that they fail closed, and the new
  `EveryWritableVocabularyPathRoundTripsThroughItsCoreNormalizer` covers the
  whole writable vocabulary in exchange.
- **Simplification pass honest.** The single applied fix — the projection-local
  ordinal `Dictionary` with `TryAdd` replacing the per-field
  `CaseAssessmentProjection.Field` linear scan — is present at
  `AssessmentReportProjection.cs:161-165` and preserves first-match semantics
  for duplicate paths. The four reported-not-applied findings each carry a
  reason.
- **No explanatory copy.** The new report rows are label/value pairs only; the
  new sections add three `<h2>` headings and no prose.

## Commands and exit codes (this review's own runs, in `.worktrees/eng-035-review`)

| Command | Exit |
| --- | --- |
| `dotnet restore ./Pegasus.slnx --locked-mode` | `RESTORE_EXIT=0` |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | `BUILD_EXIT=0` (0 warnings, 0 errors) |
| `dotnet test ./tests/Pegasus.Core.Tests/…csproj --configuration Release --no-build` | `CORE_EXIT=0` — 1200 passed, 0 failed |
| `dotnet test ./tests/Pegasus.ArchitectureTests/…csproj --configuration Release --no-build` | `ARCH_EXIT=0` — 100 passed, 0 failed |
| `pwsh -File ./scripts/Test-MigrationGrants.ps1` | `GRANTS_EXIT=0` — 88 migration files checked |

Scope rationale: the full suite was deliberately not re-run locally because
GitHub CI runs it sharded on this PR. The local scope covers the changed types
directly — `Pegasus.Core.Tests` owns every changed Core type
(`AssessmentVocabulary`, `AssessmentPolicy`, `AssessmentReportProjection`,
`AssessmentReportRendering`), `Pegasus.ArchitectureTests` proves the
Core/Infrastructure dependency direction the new Core-owned presentation code
depends on, and `Test-MigrationGrants.ps1` covers the added migration. The
SQL-integration and Browser assertions (`EfCaseAssessmentStore`, the MCP
ingress, the rendered PDF) are proven by CI, which is green on all of them.
`docs/design/test-ui` is unchanged and no routed Razor page was touched, so the
Test UI snapshot commands do not apply.

## CI at the reviewed head

`changes` SUCCESS · `local-development-scripts` SUCCESS · `reference-data`
SUCCESS · `unit` SUCCESS · `sql-integration (1..3)` SUCCESS · `browser` SUCCESS
· `test-ui` SUCCESS · `sql-integration-coverage` SUCCESS · `infrastructure`
SKIPPED · **`documentation` FAILURE** (finding 4, inherited from `origin/dev`).
`mergeStateStatus` is `UNSTABLE`.

## Outstanding for the controller

1. Finding 4 — the red `documentation` check. Either land the one-line
   `.opencode/skills/kanmer-setup/SKILL.md` link repair on `dev` and refresh
   this branch, or make an explicit decision to merge over a base-branch
   failure that ENG-035 neither caused nor can fix within its scope. Until then
   ENG-035 stays in Review and is not merged.
2. Finding 2 is deferred to [[ENG-037]] and needs scheduling.
