# Files — TICK-011

## Where the change lands

The research found no required source change: the INT-17 implementation is already on `dev`. These are the assessed implementation surfaces, to modify only if a concrete defect is discovered.

| Path | Why |
|---|---|
| `src/Pegasus.Core/ImageIntake/VrmRecognition.cs` | Owns the recognition port, outcome taxonomy, 0.80 automatic bar, and narrowly accepted read-to-registration rules. A change risks turning abstention into a mutation. |
| `src/Pegasus.Core/ImageIntake/ImageIntakeAutomation.cs` | The post-persistence caller scans and records image outcomes, then conditionally registers and associates. A change can affect receipt state or automatic association. |
| `src/Pegasus.Infrastructure/Vision/OnnxVrmRecognitionEngine.cs` | ADR-0019 ONNX implementation and decode/model failure boundary. A change affects the no-external-upload and fail-closed guarantees. |
| `src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs` | Transactional immutable Image Intake registration and reference allocation. A change risks identity/reference reuse or an inconsistent receipt decision. |
| `tests/Pegasus.Core.Tests/ImageIntake/AutomaticImageIntakeTests.cs` | Focused unit coverage for scanning, candidate thresholds, recording, registration, and forward association. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `docs/frd/frd-06-vehicle-and-engineering-evidence.md` | Product boundary: source-bound results, recorded abstention/failure, one unambiguous automatic action at the accepted bar, never a Case allocation. |
| `docs/adr/0019-in-process-onnx-vrm-recognition.md` | Durable mechanism boundary: use the in-process ONNX engine; no external upload or new deployment unit. |
| `src/Pegasus.Core/Intake/DurableIntake.cs` | Invokes image automation after durable processing; recognition cannot become an alternate intake path. |
| `src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs` | Registration is an immutable pre-Case identity and Case association is separate derived history. |
| `tests/Pegasus.IntegrationTests/VrmRecognitionEngineTests.cs` | Integration-level engine failure and no-readable-result expectations. |

## Ripple effects

- A recognition-policy change needs Core tests, engine tests, and the local-only corpus evaluation; `corpus/` is immutable and must never be changed or uploaded.
- A model or external-adapter change is an ADR-governed architectural decision, outside this ticket's reconciled scope.
- Existing process has no requested repository source or documentation modification.

## Out of scope

- Changing the selected ONNX models, threshold, or external-upload boundary.
- Case/PO allocation, broader image/damage AI, and staff UI changes.
- Modifying the ignored local corpus or claiming that the timed-out integration subset passed.
