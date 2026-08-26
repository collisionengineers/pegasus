# Files — INTK-043

## Implementation

| Path | Change and constraint |
| --- | --- |
| `src/Pegasus.Worker/IntakeFunctions.cs` | Replace separate normal-path intake/custody queue handlers with one typed queue dispatcher. Retain timer recovery outside the successful route. |
| `src/Pegasus.Core/Intake/DurableIntake.cs` | Run normal case allocation and custody directly after a successful intake attempt; publish only retry/recovery work. Add stage timing without changing lease/idempotency rules. |
| `src/Pegasus.Core/Intake/ProcessIntake.cs` | Time identity, source read, retention, assessment and persistence; retain asset integrity while removing proven repeated work. |
| `src/Pegasus.Infrastructure/Intake/MimeKitPdfPigOpenXmlIntakeSourceReader.cs` | Remove measured unnecessary copies/duplicate decoding while preserving traversal limits, ordering, sender evidence and all supported extraction. |
| `src/Pegasus.Infrastructure/Persistence/EfQueuedCustodyProcessor.cs` | Create the Box folder once and upload independent assets with bounded concurrency, preserving deterministic names and idempotency. |
| `src/Pegasus.Worker/Program.cs` and image-automation composition | Preload the existing ONNX models when the warm worker starts; reuse current DI ownership. |
| `infra/modules/platform.bicep` and parameters | Configure one 2 GB always-ready instance for the unified queue function. No cloud change occurs in this ticket without separate approval. |
| `docs/adr/` and intake FRD/PRD | Record the evidenced warm-route decision and the five-second target/attribution behaviour. |
| focused Core/Infrastructure/Integration tests | Cover dispatch, retry, timing, concurrency, all supported formats, custody equivalence and sender/state regression boundaries. |

## Context

- `docs/frd/frd-02-intake-and-source-identity.md` — durable, shared, fail-closed intake rules.
- `docs/frd/frd-05-documents-extraction-and-custody.md` — custody completeness and evidence rules.
- `src/Pegasus.Core/Intake/IntakeContracts.cs` — existing ports and result contracts; no duplicate business path.
- `src/Pegasus.Infrastructure/DependencyInjection.cs` — composition of reader, queue and custody services.
- `MAIL-013` and `INTK-001` — Graph ingress and UI projection are separate linked work.

## Deliberately out of scope

Graph subscription/webhook implementation, UI layout/state implementation, new runtime/store/queue technology, SQL tier changes, compatibility routes, fabricated inputs, and any cloud write or deployment.
