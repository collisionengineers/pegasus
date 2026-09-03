# Review record — PLAT-070 (PR https://github.com/collisionengineers/pegasus/pull/649)

Reviewer family: Claude (Opus) dispositioning, over an independent
`gpt-5.6-terra` xhigh whole-diff read in a detached read-only checkout — the
other family from the `gpt-5.6-terra`/`sol` pair that built the branch, per
EPIC-012's model allocation.

Head SHA reviewed: `8a749f53405535c234eda23206d24ae67ff5f891`
Branch: `task/plat-070-remove-review-flags`
Base: `origin/dev`
Review checkout: `.worktrees/plat-070-review` (detached, read-only)
Date: 2026-09-03
Round: 2 (round 1 reviewed `12423adc` and requested changes; commit
`8a749f53` applied findings 1-7)

## Verdict

**APPROVED on substance — merge held on a repository-level CI failure that is
not this PR's.**

All seven round-1 findings are correctly applied, nothing was weakened to make
them pass, and no new defect was introduced. Local verification is green
(exit codes below), and every CI lane at this SHA passes except
`documentation`, which fails on a broken link in `.opencode/skills/kanmer-setup/SKILL.md`
that is already on `origin/dev` and is outside PLAT-070's owned paths. That
lane is the only thing between this PR and merge; the decision to merge over
it, or to land the link fix on `dev` first, is a controller/administrator call
(see "Blocking item outside this ticket").

## Verification of the round-1 fixes

| # | Fix | Verified at `8a749f53` |
| --- | --- | --- |
| 1 | Migration list | `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs:117-118` now ends `…GrantProviderSubmissionAcceptRecovery", "20260903153134_RemoveStaffReviewFlags"`. CI `sql-integration` shards all pass. |
| 2 | Stale readiness assertion | `CaseDataCompletenessPersistenceTests.cs:26-27` asserts `CaseLifecycleState.Review` and `SatisfiesPolicy = true` — the ticket's own acceptance condition, asserted deliberately rather than relaxed. |
| 3 | Retained review-shaped rows | `CaseMutationPageModel.cs:84-98`: `instructionConfirmedByStaff`/`imagesConfirmedByStaff` are gone from both `RetainableFormFields` and `BooleanFormFields`; the hidden pass-through inputs in `_CaseWorkflow.cshtml:139-140` are untouched, so `OnPostConfirmCompletenessAsync` still receives the case's current values and cannot rewrite them. No `Humanize` fallback label is reachable for a field the operator never proposed. |
| 4 | Duplicate predicate | `CaseDataOperations.cs:73` is now `var satisfiesPolicy = completeness.IsReadyForReview(automaticallyDefinitive);` — one Core owner (`CaseContracts.cs:132-133`), no second copy. |
| 5 | Lost store coverage | `AdministrationPolicyPersistenceTests.cs:33-65` restores `WorkflowConfigurationUpdateIsVersionedAuditedAndReplaySafe` against the reduced 4-arg `UpdateWorkflowConfigurationRequest`: version bump, idempotent replay (`Assert.Equal(updated, replay)`), stale-version conflict, and exactly one `workflow_configuration` `ActionHistory` row. The new read-only identity test is kept alongside. |
| 6 | Vacuous preservation assertion | `CaseDataHarness.CreateAsync` gained optional `instructionConfirmedByStaff`/`imagesConfirmedByStaff` (default `false`, every other call site unaffected); `ConfirmAndSaveUseSharedVersionLeaseReplayAndImmutableHistory` now seeds both `true`, so the unchanged preservation assertions can actually fail if a stored `true` were reset. |
| 7 | Dead status markup | The `TempData["AdministrationStatus"]` block is gone from `Pages/Administration/Configuration.cshtml`; `grep -c AdministrationStatus` on that file returns 0, and the regenerated `administration-configuration--default.html` snapshot matches. |

Findings 8 and 9 remain deferred to [[PLAT-072]] and finding 10 remains
rejected, exactly as round 1 recorded — this pass changed neither.

## What the branch gets right (re-checked at this SHA)

- `CaseLifecycleRules.ValidateReadiness` delegates to the pre-existing
  `ValidateReviewReadiness` (`CaseLifecycle.cs:551-573`); `EvidenceReference`
  is still required and completeness is still enforced. Policy stays in Core.
- The two configuration flags and the two evidence values are deleted, so
  "no code path reads them" is a compile-time guarantee.
- Migration `20260903153134_RemoveStaffReviewFlags` drops exactly the two
  `WorkflowConfigurations` columns, edits no historical migration, leaves
  `Cases` alone, and its `Down` re-adds both as `bit NOT NULL DEFAULT 1` with
  the seed row restored. `Test-MigrationGrants.ps1` passes; a column-only drop
  on an existing table needs no grant change.
- `WorkflowConfigurationSnapshot` losing two fields is safe against historical
  `ActionHistory.AfterJson`: `System.Text.Json` ignores the extra properties on
  replay, and the replay guard still checks `PolicyVersion`.
- `OperatorLabels.WorkflowConfiguration` loses six dead constants and keeps
  `.Meta`; no operator-visible string is hard-coded and no explanatory copy is
  added anywhere in the diff.
- `git grep -i "ReviewedByStaff\|RequireStaffImageReview\|RequireStaffInstructionReview\|staff-reviewed"`
  outside `Persistence/Migrations` returns only the two negative assertions in
  `WorkflowConfigurationWebTests.cs` — the ticket's own verification line.
- D44/D45 are recorded in `frd-01`, `frd-06`, `frd-12`, `docs/design/README.md`
  and both group `context.md` documents.

## Findings and dispositions (round 2)

| # | Severity | Finding | File | Disposition |
| --- | --- | --- | --- | --- |
| 1 | Minor | FRD-01 and FRD-12 now state that Send to EVA "moves the Case to With Engineer", which is D47's transition — implemented by [[CASE-040]], not by this PR. | `docs/frd/frd-01-case-identity-and-lifecycle.md:66-72`, `docs/frd/frd-12-operator-experience.md:235` | **reject** — PLAT-070's own ticket body states the rule ("Not ready → Review is decided by completeness only; Review → With Engineer happens through Send to EVA"), and an FRD states required behaviour, not as-built state (as-built lives in `docs/current-architecture.md`), so this is not a delivery claim. CASE-040 still owns FRD-07's correction and the code. Accepted risk: a small textual overlap with CASE-040's PR. |
| 2 | Minor | Removing the dead notice leaves an empty `<div class="panel-body stack"></div>`, i.e. 15px of blank panel body under the head (`site.css:214`). | `src/Pegasus.Web/Pages/Administration/Configuration.cshtml:22-23` | **defer to [[PLAT-062]]** — that ticket refills this exact panel body with the completeness rules and chase interval (D23, and the resolved open question's option (b)). The element renders no control, no copy and no operator-visible state, so a third remediation round for one empty wrapper is disproportionate. |

No blocker or major finding survived verification.

### Note on the independent read

The `gpt-5.6-terra` xhigh pass produced the two findings above. An earlier
output file from the round-1 run was present in the scratch directory and was
**not** used; it restated round-1 findings 3-8 that this SHA has already fixed,
and each of those was checked against the file contents at `8a749f53` before
being discarded (`Configuration.cshtml` has no `AdministrationStatus`;
`CaseMutationPageModel.cs` has no `*ConfirmedByStaff` entries;
`CaseDataOperations.cs:73` calls `IsReadyForReview`;
`CaseDataCompletenessPersistenceTests.cs:104` seeds `true`).

## Simplification pass and review-response honesty

The plan's "Review response" table matches the diff finding by finding — every
"fixed" claim is verifiable in `git diff 12423adc..HEAD`, which touches exactly
the seven files those fixes require and nothing else. The post-implementation
report states its deviations openly (resumed run, two extra mechanical test
fixes, the corrected `Down` default, the narrowed form regex) and does not
claim the deferred `Create.cshtml` residue as done. The one remaining unapplied
simplification — the now-unused `automaticallyDefinitive` parameter on
`CaseCompleteness.IsReadyForReview` — names its real reason (the unowned
`Intake/AcceptIntake.cs` caller) and is carried by [[PLAT-072]]. Honest.

## Commands run in the review checkout, with exit codes

Scope rationale: the full filtered suite is not re-run locally — GitHub CI runs
it sharded on this PR and the merge is blocked on it. Locally I ran the two
suites owning every changed Core type, plus the eight integration classes the
round-1 fixes live in (which is where findings 1, 2, 5 and 6 land), plus the
migration gate this diff needs.

| Command | Exit | Why this scope covers the change |
| --- | --- | --- |
| `dotnet restore ./Pegasus.slnx --locked-mode` | `RESTORE_EXIT=0` | Locks unchanged; no package added. |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | `BUILD_EXIT=0` | 0 warnings, 0 errors — every positional caller of the two reduced Core records compiles at this SHA. |
| `dotnet test tests/Pegasus.Core.Tests --configuration Release --no-build` | `CORETESTS_EXIT=0` | 1182/1182. Owns `CaseContracts`, `CaseDataOperations`, `CaseLifecycle`, `CaseWorkflowContracts`, `DefaultCaseWorkflowConfiguration`, `WorkflowConfigurationAdministration`. |
| `dotnet test tests/Pegasus.ArchitectureTests --configuration Release --no-build` | `ARCHTESTS_EXIT=0` | 100/100. Dependency direction after the Core narrowing and the Web deletions. |
| `dotnet test tests/Pegasus.IntegrationTests … --filter` (AdministrationPolicyPersistence, CaseDataCompletenessPersistence, IntakePersistence, WorkflowConfigurationWeb, CaseWorkflowWeb, CaseClosureWeb, CaseWorkflowPersistence, CaseDetailsWeb) | `INTEGRATION_SUBSET_EXIT=0` | 111/111 against LocalDB — the direct proof of fixes 1, 2, 5 and 6, and of the removed panel/evidence fields. |
| `./scripts/Test-MigrationGrants.ps1` | `GRANTS_EXIT=0` | The PR adds a migration; this is its grant gate. |

`Update-TestUiSnapshots.ps1 -Verify` was not re-run locally this round: the
only snapshot change since the round-1 verified capture is the removal of one
blank line in `administration-configuration--default.html`, and CI's `test-ui`
lane passes at this SHA (25m47s), as does `browser`.

## CI at the reviewed SHA (run 33784992066)

First attempt: `sql-integration (1)` was cancelled at the 20-minute job limit
("The job has exceeded the maximum execution time of 20m0s") — an
infrastructure timeout, not a test failure; shards 2 and 3 passed. Re-run with
`gh run rerun --failed`: **`sql-integration (1)` passed in 16m38s**, and
`unit`, `sql-integration (2)`, `sql-integration (3)`, `sql-integration-coverage`,
`test-ui`, `browser`, `changes`, `local-development-scripts` and
`reference-data` all pass; `infrastructure` skips.

### Blocking item outside this ticket

`documentation` fails, and it is not this PR's defect. Its only error is
`BROKEN .opencode/skills/kanmer-setup/SKILL.md: ../../../../docs/manual/greenfield.md`
— a file this PR does not touch (`git diff origin/dev...HEAD -- .opencode/` is
empty) pointing at a `docs/manual/` directory that does not exist on `dev`
either. It failed identically in round 1 and on PR #648, so it will fail every
PR in EPIC-012 until it is fixed. It is outside PLAT-070's owned paths and
outside a reviewer's allowed operations. It also suppresses the lane's Test UI
catalogue step, which round 1 therefore ran locally (`CATALOGUE_EXIT=0`).

## Stop condition

Not merged. PLAT-070 stays in Review with a clean review: no blocker, no major,
two minor findings dispositioned (one rejected, one deferred to [[PLAT-062]]).
The merge needs a controller/administrator decision on the pre-existing
`documentation` lane — either merge over it explicitly, or land the
`.opencode/skills/kanmer-setup/SKILL.md` link fix on `dev` first and re-run.
