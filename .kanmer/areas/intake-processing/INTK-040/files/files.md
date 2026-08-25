# Files — INTK-040

## Where the change lands

| Path | Why |
|---|---|
| src/Pegasus.Core/Intake/GroupedIntake.cs | Generalize grouped submission to preserve a supported source channel and carry parent-receipt provenance while retaining the one grouped intake implementation. |
| src/Pegasus.Core/Intake/ProcessIntake.cs | Defer parent Unidentified registration only for eligible mailbox receipts with direct image attachments. |
| src/Pegasus.Core/Intake/DurableIntake.cs | Submit selected attachments before completing a freshly processed work item; keep terminal submission failure visible and do not backfill completed historical mail. |
| src/Pegasus.Core/Intake/MailboxImageIntakeSubmission.cs | Own the narrow eligibility, direct-attachment selection, stable group request, artifact reads, and terminal-failure result for this external boundary. |
| src/Pegasus.Worker/WorkerDependencyInjection.cs | Compose the existing grouped submission service for mailbox processing. |
| src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs and EfIntakeSubmissionGroupStore.cs | Persist and validate the nullable parent receipt relation used for provenance and idempotency. |
| src/Pegasus.Infrastructure/Persistence/Migrations/*MailboxImageIntake* and model snapshot | Establish the intended schema and the Worker permissions needed to create grouped submissions. |
| tests/Pegasus.Core.Tests/Intake/GroupedIntakeTests.cs | Prove source-channel preservation and parent consistency without weakening manual upload behavior. |
| tests/Pegasus.Core.Tests/Intake/ProcessIntakeTests.cs and focused mailbox-image submission tests | Prove eligibility, exclusion, stable replay and failure behavior. |
| tests/Pegasus.IntegrationTests/MailboxIntakeIntegrationTests.cs or the closest queued-intake fixture | Prove a U35-shaped receipt enters grouped Image Intake and excludes EML/inline assets. |
| docs/operator-notes.md | Record the operator's binding replacement and future-mail-only ruling. |
| docs/frd/frd-02-intake-and-source-identity.md | Specify mailbox attachment group routing and its acceptance outcomes. |

## Context files

| Path | What it tells the implementer |
|---|---|
| src/Pegasus.Core/ImageIntake/ImageIntakeAutomation.cs | The existing single/group VRM aggregation and association policy must remain the sole owner. |
| src/Pegasus.Core/Intake/ReconcileGroupedImageIntake.cs | Group settlement and one group-level Unidentified result already exist; do not reproduce them for mail. |
| src/Pegasus.Infrastructure/Persistence/EfQueuedCustodyProcessor.cs | Image custody reads child receipt source assets, which is why selected attachments require child receipts. |
| src/Pegasus.Core/Intake/MailboxIntake.cs | The mailbox adapter stages the original EML and its identity metadata; it should not gain image business policy. |
| src/Pegasus.Core/Intake/InstructionEvidenceImages.cs | Shows asset-kind semantics, but its deduplication/embedded-image behavior is intentionally not the mailbox group membership rule. |
| tests/Pegasus.Core.Tests/ImageIntake/AutomaticImageIntakeTests.cs | Existing accepted/no-readable/conflicting group outcomes and test fakes to reuse. |
| docs/frd/frd-05-documents-extraction-and-custody.md | Requires source evidence custody and safe provenance. |
| docs/frd/frd-12-operator-experience.md | Requires one settled operator-facing outcome rather than split queue cards. |
| docs/adr/0029-image-initiated-case-projection.md | Keeps Image-initiated Case projection in the existing lifecycle rather than mailbox code. |

## Ripple effects

The Worker becomes a writer of intake submission groups and members, so its database grants must match its new caller. Group persistence and fakes gain nullable parent provenance. Queued-intake tests, DI composition checks, migration census/snapshot checks, grouped upload tests, and image automation tests may require constructor or schema updates. No public HTTP/API contract changes.

## Out of scope

No replay, recovery, mutation, or deletion of U35; no deployment or Azure write; no scanning of inline images or images extracted from PDFs/EMLs; no change to instruction-bearing mail, Case/Triage routing, VRM recognition, matching policy, Image-initiated Case policy, or manual upload UI.
