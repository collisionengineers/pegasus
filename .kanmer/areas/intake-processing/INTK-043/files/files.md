# Files — INTK-043

## Where the change lands

| Path | Why |
|---|---|
| src/Pegasus.Core/Intake/DurableIntake.cs | Add correlated timings around queued stages that currently surround and obscure the reader: staged download, integrity, promotion, completion and association/allocation. Telemetry must not change lease, retry, idempotency or ordering. |
| src/Pegasus.Core/Intake/ProcessIntake.cs | Split the existing whole-operation Activity around identity lookup, source reading, candidate retention, assessment and receipt persistence. Apply optimization here only if measurement proves it. |
| src/Pegasus.Infrastructure/Intake/MimeKitPdfPigOpenXmlIntakeSourceReader.cs | Instrument format/MIME/PDF work and remove only measured avoidable ordinary-path work. Sender evidence, attachment order, bounds, PDF image custody and fail-closed results must remain equivalent. |
| src/Pegasus.Infrastructure/Intake/AzureBlobIntakeArtifactStore.cs | Candidate only if traces prove staged download/promotion or immutable verification dominates. Preserve content-addressed immutability and integrity. |
| tests/Pegasus.IntegrationTests/QdosEmailCohortTests.cs or a focused latency test beside it | Reuse immutable repository-provided QDOS fixtures to produce p50/p95/worst-case stage evidence without fabricated domain material. |
| tests/Pegasus.IntegrationTests/MailboxIntakeIntegrationTests.cs | Prove mailbox intake still reaches the shared durable processor and truthful terminal outcome. |
| tests/Pegasus.IntegrationTests/UploadConfirmationWebTests.cs and focused durable-intake tests | Prove manual upload uses the same route and truthful state, including large-input Processing behavior. |
| docs/operations.md | Record shipped telemetry dimensions, measurement query/baseline and operational interpretation after deployment evidence exists. |
| docs/current-architecture.md | Refresh the as-built stage/telemetry path if implementation changes it. |

## Context files

| Path | What it tells the implementer |
|---|---|
| docs/frd/frd-02-intake-and-source-identity.md | Governing durable acceptance, shared processing, fail-closed allocation and truthful state. |
| AGENTS.md | Core is the single policy owner; corpus is immutable; cloud writes need exact approval. |
| src/Pegasus.Core/Intake/IntakeContracts.cs | Existing source-reader result, assets, evidence and port contracts to preserve. |
| src/Pegasus.Infrastructure/DependencyInjection.cs | One IIntakeSourceReader composition is shared by the document surface. |
| src/Pegasus.Worker/Program.cs | Existing Application Insights export to reuse. |
| src/Pegasus.Worker/IntakeFunctions.cs | Queue-trigger/correlation boundary; after [[INTK-042]], timer wait must not be confused with processing. |
| tests/Pegasus.IntegrationTests/QdosExtractionCoverageTests.cs and QdosMappingExtractionTests.cs | Genuine-corpus extraction contracts likely to catch a semantics-changing optimization. |
| tests/Pegasus.Core.Tests/Intake/ProcessIntakeTests.cs | Existing failure, duplicate and telemetry behavior around the processor. |
| src/Pegasus.Infrastructure/Intake/MimeKitPdfPigOpenXmlIntakeSourceReader.DocMsg.cs | DOC/MSG shares the reader class; common changes affect formats outside ordinary QDOS EML/PDF. |
| [[AUTO-008]] research/files | Prior queue-wait versus processing separation. |
| [[INTK-041]] and [[INTK-042]] | Blocking architecture/publication work that must land before the final baseline is representative. |

## Ripple effects

Activity names/tags become an operational query contract and need correlation/cardinality tests. Reader changes ripple into QDOS classification, sender identity, extraction, custody, Audit/inspection routing, case matching and Unidentified outcomes. Blob changes ripple into staging reconciliation and integrity tests. Evidence must cover mailbox and manual upload after blocking dispatch work; correctness suites remain mandatory.

## Out of scope

Graph notification/subscription work, pending publication/recovery ([[INTK-041]], [[INTK-042]], [[INTK-003]]), UI state honesty ([[INTK-001]]), always-ready cost/configuration, retry-delay changes, a second intake implementation, another runtime/store/queue, corpus changes, fabricated fixtures, and production deployment/cloud writes.
