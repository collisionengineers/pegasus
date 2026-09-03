# Review record — PLAT-070 (PR https://github.com/collisionengineers/pegasus/pull/649)

Reviewer family: Claude (Opus) dispositioning, over an independent
`gpt-5.6-terra` xhigh whole-diff read — the other family from the
`gpt-5.6-terra`/`sol` pair that built the branch, per EPIC-012's model
allocation.

Head SHA reviewed: `12423adc921e6c3015d3d365964419e71b1044b9`
Branch: `task/plat-070-remove-review-flags`
Base: `origin/dev`
Review checkout: `.worktrees/plat-070-review` (detached, read-only)
Date: 2026-09-03

## Verdict

**REQUEST CHANGES.** The ticket's substance is delivered — D44's readiness
gate, evidence values, configuration flags, persisted columns, Case-page
controls and the Administration review panel are genuinely gone, D45 is
recorded, and the tests were updated honestly rather than weakened. But CI is
red on two of this branch's own test failures, and five further findings
remain, four of them inside PLAT-070's owned paths, one of which puts the
retired concept back in front of an operator.

The post-implementation report's decision not to run the full filtered suite
locally — relying on CI instead — is what let both CI failures through. That
was a permitted trade, but it means the checklist's last unticked line is
genuinely unmet, not merely deferred.

## What the branch gets right

- The readiness rule is narrowed in `Pegasus.Core` alone; nothing is
  re-implemented in Web or Infrastructure. `ValidateReadiness` now delegates to
  the pre-existing `ValidateReviewReadiness`
  (`src/Pegasus.Core/Lifecycle/CaseLifecycle.cs:551-573`) — reuse, not a second
  copy — and `EvidenceReference` is still required.
- The two `CaseWorkflowConfiguration` flags and the two
  `CaseReadinessEvidence` values are deleted outright, so "no code path reads
  them" is a compile-time guarantee rather than an assertion.
- Migration `20260903153134_RemoveStaffReviewFlags` drops exactly the two
  `WorkflowConfigurations` columns, edits no historical migration, does not
  touch `Cases`, and its `Down` re-adds both as `bit NOT NULL DEFAULT 1` with
  the seed row restored — the PR-by-PR revert EPIC-012's rollout rule needs.
  No grant change is required.
- The hidden pass-through in `_CaseWorkflow.cshtml:139-140` is correct: the
  names bind to `OnPostConfirmCompletenessAsync`'s parameters and carry the
  case's current values, so confirming completeness cannot silently rewrite the
  intake-time confirmation. Every other changed form
  (`_ReadinessHiddenFields.cshtml`, `Workflow.cshtml.cs`, `Closure.cshtml.cs`,
  the "Return to Review" dialog) rebuilds only `CaseReadinessEvidence`, never
  `CaseCompleteness`, so none carries the same hazard.
- Tests were updated, not weakened. Retired behaviour's tests were deleted with
  the behaviour they covered and replaced by absence proofs; no surviving
  assertion was loosened. The narrowed
  `Assert.DoesNotMatch(ConfigurationFormRegex(), html)` is a scope correction,
  not a weakening — the shared layout's unrelated `<form class="utility-search">`
  is on every page, and the retired POST form's absence is still proven.
- The three regenerated Test UI snapshots match the changed Razor exactly.
- No new operator-visible string is hard-coded; `OperatorLabels.cs` loses six
  now-dead constants and keeps `.Meta`. No explanatory copy is added.
- Every changed file is an owned path or declared mechanical compile-breakage
  from the owned Core contract change.
- `git grep -i "ReviewedByStaff|RequireStaffImageReview|RequireStaffInstructionReview|staff-reviewed"`
  returns only historical migrations (correctly untouched) and the negative
  test assertions.

## Findings and dispositions

| # | Severity | Finding | File | Disposition |
| --- | --- | --- | --- | --- |
| 1 | **Blocker** | The new migration was never added to the committed-migration list, so `IntakePersistenceIntegrationTests.CommittedMigrationCreatesTheSqlServerSchema` fails on CI shard 2: `Assert.Equal() Failure: Collections differ … Actual: [… , "20260903153134_RemoveStaffReviewFlags"]`. Every migration-adding PR must extend this list; it currently ends at `20260829212237_GrantProviderSubmissionAcceptRecovery`. | `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs:117` | **fix** — append `"20260903153134_RemoveStaffReviewFlags"` after line 117. Mechanical, required, the same class of unowned-test breakage the report already declares handling twice. |
| 2 | **Blocker** | `CaseDataCompletenessPersistenceTests.AcceptanceSnapshotsTypedSourceProvenanceWithAutoAddedValues` fails on CI shard 3: `Expected: NotReady / Actual: Review`. This is D44's intended behaviour change reaching an assertion nobody updated — the harness case has complete instruction and images and, without the staff-confirmation gate, now correctly satisfies the policy and sits in Review. The PR edits this very file for other reasons and missed it. | `tests/Pegasus.IntegrationTests/CaseDataCompletenessPersistenceTests.cs:26-27` | **fix** — assert `CaseLifecycleState.Review` and `Assert.True(projection.Completeness.Evaluation.SatisfiesPolicy)` deliberately. Do not weaken it away: updated this way it becomes the ticket's own checklist proof that "a case with complete instruction and images reaches Review with no review flag". |
| 3 | Major | A refused case mutation puts the retired concept back on screen. `instructionConfirmedByStaff` / `imagesConfirmedByStaff` remain in `RetainableFormFields` and `BooleanFormFields`, so `RetainProposedValues` still retains the pass-through hidden values — but `FieldLabel`'s and `CurrentValue`'s cases for them were deleted. The "Your change was not applied" table (`_CaseWorkflow.cshtml:63-77`) therefore renders two rows the operator never proposed, labelled by the `Humanize` fallback as "Instruction confirmed by staff" / "Images confirmed by staff", with an em-dash in "The case now holds". That is an operator-visible label outside `OperatorLabels` and a review-shaped row D44 bars. No test catches it: `CaseDetailsWebTests.cs:838` posts both fields but asserts only that no raw `>true<`/`>false<` appears. | `src/Pegasus.Web/Pages/Cases/CaseMutationPageModel.cs:88,99` | **fix** — remove the two names from both frozen sets; they are no longer operator-proposed, so there is nothing to retain or compare. Keep the hidden pass-through inputs. Found independently by both reviewers. |
| 4 | Major | The completeness predicate now exists twice. `CaseCompleteness.IsReadyForReview` returns `InstructionComplete && ImagesComplete`, and `CaseCompletenessPolicy.EvaluateAcceptanceCommand` independently computes the same expression. The test that used to assert the two agree (`TheReadinessRuleAndTheAcceptancePolicyAgreeOnTheWaiver`) was deleted by this PR, so nothing holds them together. One list per concept / one Core owner. | `src/Pegasus.Core/Cases/CaseDataOperations.cs:73-75` vs `src/Pegasus.Core/Cases/CaseContracts.cs:132-133` | **fix** — `var satisfiesPolicy = completeness.IsReadyForReview(automaticallyDefinitive);` after the policy-identity validation. One line, behaviour-preserving, and it retires the simplification pass's unapplied finding 3 without touching the unowned `AcceptIntake.cs`. |
| 5 | Major | Retained production behaviour lost all its integration coverage. `WorkflowConfigurationUpdateIsVersionedAuditedAndReplaySafe` was replaced by a read-only GET assertion, but `EfWorkflowConfigurationStore.UpdateAsync`/`ReplayAsync` still implement versioning, idempotent replay, conflict handling and audit persistence, and `UpdateWorkflowConfiguration` is still registered (`src/Pegasus.Infrastructure/DependencyInjection.cs:278`) and deliberately retained for PLAT-062. The surviving `Pegasus.Core.Tests` test uses a fake store and covers authorization and trimming only. | `tests/Pegasus.IntegrationTests/AdministrationPolicyPersistenceTests.cs:12-30` | **fix** — keep the new read-only identity test and restore focused update / version / replay / conflict / audit coverage against the reduced 4-arg `UpdateWorkflowConfigurationRequest`. Deleting a store's only proof while keeping the store is not the same as retiring behaviour. |
| 6 | Minor | The new preservation assertion cannot detect the regression it exists for. The harness seeds both `*ConfirmedByStaff` values `false` (proved by the sibling `Assert.False(...)` at line 167) and the confirmation posts those same seeded values, so `Assert.Equal(false, false)` passes whether or not a stored `true` is preserved. | `tests/Pegasus.IntegrationTests/CaseDataCompletenessPersistenceTests.cs:112-135` | **fix** — seed at least one stored confirmation `true`, confirm completeness, then assert both persisted values are unchanged. It is the only proof the plan claims for the hidden pass-through. |
| 7 | Minor | Dead markup with no producer. The `TempData["AdministrationStatus"]` success notice survived the removal of the page's only POST handler and its `RedirectToPage`; no Configuration handler can set it now, leaving `panel-body stack` holding nothing but an unreachable block. Rule 21 — delete a gate that gates nothing. | `src/Pegasus.Web/Pages/Administration/Configuration.cshtml:23-29` | **fix** — remove the block. Found independently by both reviewers. |
| 8 | Major (deferred) | D44's "no review checkbox anywhere" is not literally met: `Create.cshtml` still renders "I have confirmed the instruction evidence" / "I have confirmed the image evidence", writing `CaseCompleteness.*ConfirmedByStaff` values that after this PR gate nothing. | `src/Pegasus.Web/Pages/Cases/Create.cshtml:242,250` | **defer to [[PLAT-072]]** (created and linked at review). `Create.cshtml(.cs)` is outside PLAT-070's owned paths, is not named in the ticket, and roughly fifteen unowned test files — including raw-SQL `INSERT INTO Cases` fixtures — name the two columns, a runtime blast radius no `dotnet build` catches. Rule 2: link it, do not absorb it. The plan and the report both declared this openly instead of claiming completion, which is the right conduct. |
| 9 | Nit | The CASE-013 comment still describes a staff-confirmation waiver `IsReadyForReview` no longer has; its counterpart in `CaseDataOperations.cs` was correctly deleted with the clause it explained. | `src/Pegasus.Infrastructure/Persistence/EfQueuedCustodyProcessor.cs:582-587` | **defer to [[PLAT-072]]** — outside PLAT-070's owned paths, and PLAT-072 already carries it. |
| 10 | Nit | `docs/design/README.md:1115` still ends the Workflow configuration entry with "Save configuration" though the shipped page now has none. | `docs/design/README.md:1115` | **reject** — that line describes the designed target page, which PLAT-062 / D23 refills with the completeness rules, chase interval and labour-rate cards; the Save it names belongs to that page, not to the interim read-only one. |

## Simplification pass assessment

Honest. All three dispositions check out against the diff: `.Reason`/`.Save`
are gone from `OperatorLabels.cs`; the three private helpers and three orphaned
`GeneratedRegex` members are gone from `WorkflowConfigurationWebTests.cs`, with
`ConfigurationFormRegex` correctly retained and reused; and the unapplied
`automaticallyDefinitive` finding names its real reason (the unowned
`AcceptIntake.cs` caller) rather than being silenced.

Finding 4 supersedes that third disposition — reusing `IsReadyForReview` inside
the policy makes the parameter live again without touching any unowned file.
The pass missed two pieces of the dead weight it was looking for: finding 3's
orphaned frozen-set entries and finding 7's unreachable status block.

## Commands run in the review checkout, with exit codes

Scope rationale: the full filtered suite is not re-run locally — GitHub CI runs
it sharded on this PR and the merge is blocked on it. Locally I ran the two
suites that cover the changed types without a SQL Server dependency, plus every
script gate this diff touches.

| Command | Exit | Why this scope covers the change |
| --- | --- | --- |
| `dotnet restore ./Pegasus.slnx --locked-mode` | `RESTORE_EXIT=0` | Locks unchanged; no package added. |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | `BUILD_EXIT=0` | 0 warnings, 0 errors. Proves every positional caller of the two reduced Core records compiles at the reviewed SHA. |
| `dotnet test tests/Pegasus.Core.Tests --configuration Release --no-build` | `CORETESTS_EXIT=0` | Owns every changed Core type — `CaseContracts`, `CaseDataOperations`, `CaseLifecycle`, `CaseWorkflowContracts`, `DefaultCaseWorkflowConfiguration`, `WorkflowConfigurationAdministration` — via `AutomaticCaseReadinessTests`, `CaseDataOperationsTests`, `CaseReviewReadinessTests`, `AssignCaseEngineerTests`, `ImmediateExternalPublicationTests`, `AdministrationPolicyTests`. |
| `dotnet test tests/Pegasus.ArchitectureTests --configuration Release --no-build` | `ARCHTESTS_EXIT=0` | Proves the dependency direction still holds after the Core contract narrowing and the Web page-model deletions. |
| `./scripts/Test-MigrationGrants.ps1` | `GRANTS_EXIT=0` | The PR adds a migration; this is its grant gate. |
| `./scripts/Update-TestUiSnapshots.ps1 -Verify` | `SNAPVERIFY_EXIT=0` | Fresh capture (browser 119/119, non-browser capture, then verify) — `docs/design/test-ui/` changed and three routed pages changed. The review checkout is clean afterwards, so the committed snapshots are exactly what the branch renders. |
| `./scripts/Test-UiCatalogue.ps1` | `CATALOGUE_EXIT=0` | 54 routed sources, 58 prototypes, 0 broken local references. CI's `documentation` lane skips this step after its earlier failure, so it is only proven here. |
| `./scripts/Test-DocumentationLinks.ps1` | `DOCLINKS_EXIT=1` | See the CI note below — pre-existing, not this PR's. |

## CI at the reviewed SHA (run 33777706918)

`changes`, `local-development-scripts`, `reference-data`, `unit`,
`sql-integration (1)` and `browser` pass; `infrastructure` skips.

**`sql-integration (2)` and `sql-integration (3)` fail** on exactly two tests,
both this branch's own — findings 1 and 2 above. Evidence read from the
retained shard artifacts (`test-shard-2`, `test-shard-3`): shard 2 is
361 passed / 1 failed of 364, shard 3 is 359 passed / 1 failed of 360.
Everything else the shards enumerated passes, so both fixes are narrow.

**`documentation` fails, and it is not this PR's defect.** Its only broken link
is `.opencode/skills/kanmer-setup/SKILL.md → ../../../../docs/manual/greenfield.md`,
in a file this PR does not touch (`git diff origin/dev...HEAD -- .opencode/`
is empty) that is already on `origin/dev` at `c5c7a874`, pointing at a
`docs/manual/greenfield.md` that does not exist on `dev` either. PR #648 fails
the same lane identically. It is a repository-level breakage that will block
every PR in this epic until that link is fixed, and it suppresses the lane's
`Test UI catalogue` step — which is why that check was run locally here. It is
outside PLAT-070's owned paths and outside a reviewer's allowed operations, so
it is reported for an administrator/controller decision rather than fixed here.

## Stop condition

Not merged. PLAT-070 stays in Review. Findings 1-7 go back to the implementer
(1 and 2 are merge blockers on their own); findings 8 and 9 are carried by the
newly created and linked [[PLAT-072]]; finding 10 is rejected with its reason.
The `documentation` CI lane needs a repository-level fix that is not
PLAT-070's to make.
