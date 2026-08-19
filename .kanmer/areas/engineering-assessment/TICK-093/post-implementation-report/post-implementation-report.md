# Post-implementation report — TICK-093

## Summary

PR #420 now implements one case-scoped canonical accepted repair specification, reconciled to the later operator correction in [[TICK-205]] and the [[PR-011]] review blocker. The rejected Audit purpose/role pair and uplift premise are absent from Core, persistence, migration, tests, and FRD-06. Authorized immutable versioning, source provenance, Engineer acceptance, correction/supersession, exact-version retrieval, replay/history/concurrency, one display mapping, and fail-closed legacy migration remain.

## Changes

| File | Change | Why |
|---|---|---|
| `src/Pegasus.Core/Assessment/RepairSpecifications.cs` | Added the versioned aggregate/source/calculation/display contracts, then removed all purpose/role branching; centralized Engineer authorization | One Core policy owner and one shared canonical contract |
| `src/Pegasus.Core/Assessment/AssessmentPolicy.cs` | Exposed reuse of existing estimate-line normalization | Avoid a second line vocabulary/normalizer |
| `src/Pegasus.Infrastructure/Persistence/AssessmentEntities.cs` | Added case-scoped specification persistence without purpose/role fields | Persist immutable versions and provenance at the existing boundary |
| `src/Pegasus.Infrastructure/Persistence/AssessmentModelConfiguration.cs` | Added state/source/acceptance constraints, unique case/version and one-current-accepted indexes | Enforce the corrected singleton contract in SQL |
| `src/Pegasus.Infrastructure/Persistence/EfRepairSpecificationStore.cs` | Added draft/accept/correct/replay/exact/current operations; consolidated authorization and line construction | Reuse existing workflow guards/history and prevent policy drift |
| `src/Pegasus.Infrastructure/Persistence/EfCaseAssessmentStore.cs` | Adapted estimate lines to specification versions; accepted canonical projection takes precedence over a correction draft | Keep editing state from silently replacing accepted canonical data |
| `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` | Registered the aggregate | Existing EF composition point |
| `src/Pegasus.Infrastructure/Persistence/Migrations/20260819112640_VersionedRepairSpecifications.*` and snapshot | Added the final unmerged migration atop current shared migrations, with no purpose/role columns and explicit legacy backfill | Safe pre-merge amendment without rewriting shared history |
| `tests/Pegasus.Core.Tests/Assessment/RepairSpecificationPolicyTests.cs` | Added source/authority/calculation/mapping tests; removed dual-role tests | Prove only authorized policy |
| `tests/Pegasus.IntegrationTests/AssessmentPersistenceIntegrationTests.cs` | Added singleton accept/replay/correction/supersession/exact-query lifecycle and competing-draft rejection | Prove one canonical accepted version |
| `tests/Pegasus.IntegrationTests/RepairSpecificationMigrationTests.cs` | Added pre-migration legacy fixture | Prove retention without fabricated authority |
| `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs` | Extended committed migration/table manifest | Prove combined current schema |
| `docs/frd/frd-06-vehicle-and-engineering-evidence.md` | Records one purpose-neutral canonical version per Case and its provenance/correction/display behavior | Reconcile governing behavior to the operator correction |

## Governing docs

FRD-06 is met: professional authority stays with Core and an authorized Engineer; imported routes are provenance rather than authority; accepted versions are immutable and corrected by reasoned supersession; absent/legacy authority fails closed. The updated section is purpose-neutral and introduces no Audit-only specification semantics. ADR-0025 remains satisfied because rendering stays downstream of accepted Core data and no project, store boundary, runtime, or deployment unit was added.

The later [[TICK-205]] Outcome/open questions are treated as binding. Its older plan/PIR/proof are explicitly stale. [[PR-011]] is resolved by removing the rejected vocabulary from every implementation layer.

## Risks / follow-ups

- [[TICK-092]] consumes the exact accepted-version query for render snapshot binding.
- Provider-specific Glass's, Audatex, and approved-AI extraction/mapping remain with their owning capabilities.
- Audit workflow/reference identity remains owned elsewhere and does not alter this aggregate.
- Local full integration is intrinsically long. The proportional isolated-artifacts run was stopped after about ten minutes with no failed assertions after Core 640/640 and architecture 97/97 completed; PR CI is the authoritative isolated completion gate.
- Not deployed; no cloud or `main` write.

## Verification hand-off

On merged `dev`:

1. Run `dotnet restore --locked-mode`.
2. Run `dotnet build --configuration Release --no-restore`; expect zero warnings/errors.
3. Run `dotnet test tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~Assessment"`; expect 45 passing.
4. Run the focused SQL filter for `RepairSpecificationAcceptanceCorrectionAndExactVersionPersist`, `RepairSpecificationMigrationTests`, and `CommittedMigrationCreatesTheSqlServerSchema`; expect 3 passing.
5. Run architecture tests; expect 97 passing at this branch baseline.
6. Run the full Release test profile or use the green isolated PR CI shards.
7. Search the repair-specification Core/persistence/migration/tests and FRD section for Audit-purpose/role, Conservative, Maximised, dual-specification, and uplift vocabulary; expect none attributable to this contract.
8. Verify exact accepted-version retrieval, predecessor Superseded state, legacy `LegacyUnresolved` draft, no fabricated acceptance/source, and no Reports/renderer/FRD-11/package-lock diff.
