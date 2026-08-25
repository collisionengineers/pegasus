# Plan — PR-056: Make Review completeness mandatory in every configuration

## Approach

Remove the two obsolete “require complete instructions/images” switches and make the existing `CaseCompletenessPolicy` and lifecycle validation always require both facts. This is smaller and clearer than retaining switches that can no longer be false, adding a second Export policy, or adding compatibility behaviour. Keep the supported staff-review switches and CASE-013 automatic-intake exception unchanged: automatic intake may waive staff confirmation, never the underlying completeness evidence.

## Governing docs

- **Meets — `docs/frd/frd-01-case-identity-and-lifecycle.md`:** steps 1–4 enforce its unconditional instruction- and image-completeness gates at initial acceptance, later promotion/return to `Review`, and Engineer assignment. Provider/staff-review policy may still define review requirements but cannot remove completeness.
- **Meets — `docs/frd/frd-07-eva-and-external-engineering-handoff.md`:** steps 1 and 4 preserve `Review` as the single Export-readiness owner. Export receives no duplicate field, custody, or evidence-status validation.
- Neither governing document needs modification and no new ADR is justified: this removes an obsolete configuration option from the existing lifecycle policy rather than choosing a new architecture.

## Steps

1. In Core, remove the two completeness-toggle members from `CaseWorkflowConfiguration` and its update/default contracts. Change the existing completeness and assignment validation so instructions and images are always required, while retaining the two staff-review switches and the automatically-definitive intake exception. Reuse `CaseCompletenessPolicy`, `CaseCompleteness.IsReadyForReview`, and the existing lifecycle validation; add no new readiness owner.
2. In persistence, remove the obsolete entity properties, EF model configuration, seed values, read/write mapping, replay snapshot fields, and constructor arguments. Generate the normal EF migration and snapshot update that drop the two unused columns. Do not add data conversion, dual-read/write, a feature flag, or rollback/compatibility machinery for unreleased development state.
3. Remove the two administrator form controls and bound/update properties so the UI can configure only the still-supported staff-review rules. Mechanically update remaining compile-time callers and test fixtures to the smaller configuration/request shape; do not refactor unrelated workflow code.
4. Strengthen the existing tests at their current owners: a Core matrix varies both completeness facts and the remaining staff-review configuration; CASE-013 tests prove automatic intake still requires both facts; lifecycle tests cover return/assignment; persistence tests cover initial acceptance and later confirmation; administration tests prove only supported settings are stored and submitted. Reuse existing test classes and fakes.
5. Run a simplification pass over this ticket's diff, remove stale dead paths found within scope, and record dispositions in the plan/checklist report. Then write the post-implementation report with the exact migration, changed callers, and evidence.

## Verification

Run from the ticket worktree with build servers disabled where needed:

1. `dotnet restore`
2. `dotnet build --configuration Release --no-restore --disable-build-servers`
3. `dotnet test tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-restore --disable-build-servers --filter "FullyQualifiedName~CaseDataOperationsTests|FullyQualifiedName~AutomaticCaseReadinessTests|FullyQualifiedName~CaseReviewReadinessTests|FullyQualifiedName~AssignCaseEngineerTests"`
4. `dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-restore --disable-build-servers --filter "FullyQualifiedName~CaseDataCompletenessPersistenceTests|FullyQualifiedName~AdministrationPolicyPersistenceTests|FullyQualifiedName~CommittedMigrationCreatesTheSqlServerSchema"`
5. Run the full Core and Architecture test projects, then the repository's normal SQL integration shards or full integration profile before re-review.

Proof must show that no combination with incomplete instructions or images reaches `Review`, including automatically-definitive intake and stored administrator configurations, and that the administration surface no longer exposes either waiver. The later merged-main verification records final commands/results in `proof.md`.

## Risks / open questions

- **Constructor ripple:** removing two positional members touches many fixtures. Mitigation: keep changes mechanical and let the compiler identify every caller; do not turn this into a workflow refactor.
- **Migration drift:** entity, model snapshot, migration census and SQL schema must agree. Mitigation: use the normal EF migration path and run the committed-schema test.
- **CASE-013 regression:** an over-broad change could accidentally require staff confirmation for automatically definitive intake. Mitigation: retain that predicate and explicitly test complete automatic evidence versus each incomplete combination.
- **Duplicate Export validation:** adding checks in `EvaHandoffStore` would create the duplicate policy the operator rejected. Mitigation: make no Export change in this ticket and test the lifecycle owner directly.
- No unresolved operator questions.
