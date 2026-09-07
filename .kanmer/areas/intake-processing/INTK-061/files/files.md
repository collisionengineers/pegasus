# Files — INTK-061

## Where the change lands

| Path | Why |
| --- | --- |
| `src/Pegasus.Core/Intake/RetainIncomingArtifact.cs` | Route claims using the complete operation identity. |
| `src/Pegasus.Core/Intake/DurableIntake.cs` | Persist evaluation separately from successful destination completion; retain retry ownership. |
| `src/Pegasus.Core/Intake/IntakeAllocation.cs` | Refuse new allocation for a recorded unique match. |
| `src/Pegasus.Core/Intake/IntakeContracts.cs` | Existing intake query/group contracts only where needed for eligible reconciliation. |
| `src/Pegasus.Core/Intake/ProcessIntake.cs` | Reuse group registration/reason construction; no new provider routing. |
| `src/Pegasus.Core/Intake/ReconcileGroupedImageIntake.cs` | Recover eligible oldest groups once and retain group outcome. |
| `src/Pegasus.Core/Intake/IntakeOcr.cs` | Resume analysis from retained completed OCR output. |
| `src/Pegasus.Core/Intake/GroupedIntakeSubmission.cs` | Existing submission-group query contract if required. |
| `src/Pegasus.Core/ImageIntake/ImageIntakeAutomation.cs` | Emit one group-level Unidentified registration with canonical reason. |
| `src/Pegasus.Infrastructure/Persistence/EfDocumentRequestStore.cs` | Claim intake assets directly without probing Web-owned tables. |
| `src/Pegasus.Infrastructure/Persistence/EfIntakeWorkStore.cs` | Separate evaluation persistence from terminal work completion using current columns. |
| `src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs` | Eligible recovery query if the existing group store cannot carry it. |
| `src/Pegasus.Infrastructure/Persistence/EfIntakeSubmissionGroupStore.cs` | Bound oldest eligible groups before paging. |
| `src/Pegasus.Infrastructure/Persistence/EfIntakeOcrOperationStore.cs` | Keep external work retryable until analysis follows stored OCR output. |
| `src/Pegasus.Worker/IntakeFunctions.cs` | Ensure processing outcomes correspond to durable scheduling and log defects. |
| `tests/Pegasus.Core.Tests/Intake/*.cs` | Focused policy/store-contract fakes and intake/OCR regressions only. |
| `tests/Pegasus.IntegrationTests/*.cs` | Actual Worker-role custody, queued routing failure, group and OCR regressions plus affected interface callers only. |
| `docs/frd/frd-02-intake-and-source-identity.md` | Clarify durable routing completion and single group outcome. |
| `docs/frd/frd-05-documents-extraction-and-custody.md` | Clarify custody claim identity and recoverable OCR analysis completion. |

## Context files

| Path | What it tells the implementer |
| --- | --- |
| `src/Pegasus.Infrastructure/Persistence/Migrations/20260906054658_V1PlatformFoundation.cs` | Worker must not access PublicUploadOccurrences; IntakeAssets UPDATE already exists. |
| `src/Pegasus.Core/Intake/Unidentified/UnidentifiedContracts.cs` | Existing group origin and canonical reasons. |
| `src/Pegasus.Infrastructure/Persistence/IntakeEntities.cs` | Current evaluation/work fields and FK constraints. |
| `src/Pegasus.Infrastructure/Persistence/EfUnidentifiedStore.cs` | Stable group registration replay identity. |
| `src/Pegasus.Core/Custody/ExternalWorkProcessing.cs` | Shared external work routing, no new queue. |
| `tests/Pegasus.IntegrationTests/LocalDbTestDatabase.cs` | Existing migrated restricted-role SQL fixture; root owns execution. |

## Ripple effects

Update every affected interface implementation/test decorator. No new package,
project, schema/migration or generated UI artifact is planned. Scope-test patterns
permit only files whose existing fixtures or contract callers are affected.

## Out of scope

Principal rollout, automatic Triage linking, null CaseType ticket INTK-057,
mailbox wipe policy, admin/Case pages, deployment, provider writes, new grants.
