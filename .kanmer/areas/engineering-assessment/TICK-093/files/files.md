# Files — canonical repair specification

| Path/module | Correction | Risk |
| --- | --- | --- |
| `src/Pegasus.Core/Assessment/RepairSpecifications.cs` | Remove purpose/role vocabulary and parameters; retain one case-scoped immutable aggregate | Contract callers |
| `src/Pegasus.Infrastructure/Persistence/AssessmentEntities.cs` | Remove persisted purpose/role fields | EF model |
| `src/Pegasus.Infrastructure/Persistence/AssessmentModelConfiguration.cs` | Replace role-scoped uniqueness with case/version and one-current-accepted indexes | SQL uniqueness |
| `src/Pegasus.Infrastructure/Persistence/EfRepairSpecificationStore.cs` | Make draft/version/current queries case-scoped | Idempotency/concurrency |
| `src/Pegasus.Infrastructure/Persistence/EfCaseAssessmentStore.cs` | Remove ordinary-purpose filters from compatibility draft selection | Legacy surface |
| `src/Pegasus.Infrastructure/Persistence/Migrations/20260819100144_VersionedRepairSpecifications.*` and snapshot | Amend only this unmerged branch migration to omit purpose/role columns and constraints | Migration ordering/model match |
| `tests/Pegasus.Core.Tests/Assessment/RepairSpecificationPolicyTests.cs` | Remove dual-role tests; prove one canonical policy | Coverage accuracy |
| `tests/Pegasus.IntegrationTests/AssessmentPersistenceIntegrationTests.cs` | Remove Audit-pair lifecycle; prove singleton acceptance/correction/exact query | SQL lifecycle |
| `tests/Pegasus.IntegrationTests/RepairSpecificationMigrationTests.cs` | Assert one legacy-unresolved canonical draft | Upgrade evidence |
| `docs/frd/frd-06-vehicle-and-engineering-evidence.md` | Remove dual-spec/Audit role wording and state one shared canonical specification | Governing truth |

## Context

- [[TICK-205]] Outcome and open questions — corrected operator authority.
- [[PR-011]] — review blocker and required correction.
- `reference/rendererref1` — three shared names-only display sections.
- Existing Assessment policy, edit lease, history, replay, and EF conventions — reuse targets.

## Out of scope

- Audit-specific specification data, conservative/maximised roles, uplift, wording, or presentation.
- Glass's/Audatex/AI extraction implementations.
- Reports/renderer/FRD-11/package-lock changes.
